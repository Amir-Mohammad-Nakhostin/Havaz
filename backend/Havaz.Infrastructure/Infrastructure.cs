using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Havaz.Application;
using Havaz.Domain;
using Microsoft.Extensions.Logging;

namespace Havaz.Infrastructure;

public sealed class RetryHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        const int maxAttempts = 3; // bounded: 1 initial try + 2 retries, exponential backoff
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var response = await base.SendAsync(request, ct);
                if (response.IsSuccessStatusCode || (int)response.StatusCode < 500 || attempt == maxAttempts) return response;
                response.Dispose();
            }
            catch (HttpRequestException) when (attempt < maxAttempts && !ct.IsCancellationRequested) { }
            await Task.Delay(TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt - 1)), ct);
        }
    }
}

public sealed class OpenMeteoWeatherProvider(HttpClient http) : IWeatherProvider
{
    public async Task<EnvironmentalSeries> GetWeather(double latitude, double longitude, CancellationToken ct)
    {
        var url = $"v1/forecast?latitude={latitude:F4}&longitude={longitude:F4}&hourly=temperature_2m,apparent_temperature,relative_humidity_2m,precipitation_probability,precipitation,rain,snowfall,wind_speed_10m,wind_direction_10m,weather_code&timezone=auto&forecast_days=7";
        var raw = await http.GetFromJsonAsync<OpenMeteoWeatherResponse>(url, ct) ?? throw new InvalidOperationException("empty weather response");
        var rows = raw.Hourly.Time.Select((time, i) => new EnvironmentalObservation(
            DateTime.Parse(time), latitude, longitude,
            Value(raw.Hourly.Temperature2m, i), Value(raw.Hourly.ApparentTemperature, i), Value(raw.Hourly.RelativeHumidity2m, i),
            Value(raw.Hourly.Rain, i), Value(raw.Hourly.PrecipitationProbability, i), Value(raw.Hourly.Precipitation, i), Value(raw.Hourly.Snowfall, i),
            Value(raw.Hourly.WindSpeed10m, i), Value(raw.Hourly.WindDirection10m, i), (int)Value(raw.Hourly.WeatherCode, i),
            null, null, null, null, null)).ToList();
        return new(raw.Timezone ?? "UTC", rows);
    }
    private static double Value(IReadOnlyList<double?>? values, int index) => values is not null && index < values.Count && values[index].HasValue ? values[index]!.Value : 0;
}

public sealed class OpenMeteoAirQualityProvider(HttpClient http) : IAirQualityProvider
{
    public async Task<AirQualitySeries> GetAirQuality(double latitude, double longitude, CancellationToken ct)
    {
        var url = $"v1/air-quality?latitude={latitude:F4}&longitude={longitude:F4}&hourly=pm2_5,pm10,ozone,nitrogen_dioxide,european_aqi&timezone=auto&forecast_days=7";
        var raw = await http.GetFromJsonAsync<OpenMeteoAirResponse>(url, ct) ?? throw new InvalidOperationException("empty air quality response");
        var rows = raw.Hourly.Time.Select((time, i) => new AirQualityPoint(DateTime.Parse(time), Value(raw.Hourly.EuropeanAqi, i), Value(raw.Hourly.Pm25, i), Value(raw.Hourly.Pm10, i), Value(raw.Hourly.Ozone, i), Value(raw.Hourly.NitrogenDioxide, i))).ToList();
        return new(raw.Timezone ?? "UTC", rows);
    }
    private static double? Value(IReadOnlyList<double?>? values, int index) => values is not null && index < values.Count ? values[index] : null;
}

public sealed class PythonMlPredictionProvider(HttpClient http, ILogger<PythonMlPredictionProvider> log) : IMlPredictionProvider
{
    public async Task<MlPrediction?> Predict(MlFeatureVector features, CancellationToken ct)
    {
        try
        {
            using var response = await http.PostAsJsonAsync("/predict-risk", new { modelVersion = MlContract.ModelVersion, featureSchemaVersion = MlContract.FeatureSchemaVersion, features = features.ToDictionary() }, ct);
            if (!response.IsSuccessStatusCode) { log.LogWarning("ML returned status {Status}", response.StatusCode); return null; }
            var dto = await response.Content.ReadFromJsonAsync<MlResponseDto>(cancellationToken: ct);
            if (dto?.ModelVersion != MlContract.ModelVersion || dto.FeatureSchemaVersion != MlContract.FeatureSchemaVersion
                || dto.RiskProbability is not double probability || probability is < 0 or > 1 || dto.Confidence is < 0 or > 1)
            { log.LogWarning("ML returned an invalid prediction contract"); return null; }
            return new(dto.ModelVersion, dto.FeatureSchemaVersion, probability, dto.RiskClass ?? "unknown", dto.Confidence ?? 0);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException) { log.LogWarning(ex, "ML prediction failed"); return null; }
    }
    private sealed class MlResponseDto { public string? ModelVersion { get; set; } public string? FeatureSchemaVersion { get; set; } public double? RiskProbability { get; set; } public string? RiskClass { get; set; } public double? Confidence { get; set; } }
}

public sealed class AnalysisService(IWeatherProvider weather, IAirQualityProvider air, IMlPredictionProvider ml, HavazDbContext db, ILogger<AnalysisService> log) : IAnalysisService
{
    private readonly RiskEngine riskEngine = new();
    private readonly RecommendationEngine recommendationEngine = new();

    public async Task<AnalysisDataDto> Analyze(AnalysisRequest request, Guid userId, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestId = Guid.NewGuid().ToString("N");
        var weatherSeries = await weather.GetWeather(request.Latitude, request.Longitude, ct); // one forecast request for the whole window set
        AirQualitySeries? airSeries = null;
        try { airSeries = await air.GetAirQuality(request.Latitude, request.Longitude, ct); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException) { log.LogWarning(ex, "AQI provider failed for {RequestId}", requestId); }

        var profile = ActivityProfile.Get(request.Activity);
        var intensity = FeatureEngineering.Intensity(request.Intensity);
        var (startHour, endHour) = ParseRange(request.PreferredTimeRange);
        var requestedDate = request.Date.ToDateTime(TimeOnly.MinValue).Date;
        var dayHours = weatherSeries.Hourly.Where(o => o.Timestamp.Date == requestedDate).Select(o => Merge(o, airSeries)).OrderBy(o => o.Timestamp).ToList();
        if (dayHours.Count == 0) throw new InvalidOperationException("NO_ENVIRONMENTAL_DATA");
        var rangeHours = dayHours.Where(o => o.Timestamp.Hour >= startHour && o.Timestamp.Hour < endHour).ToList();
        if (rangeHours.Count == 0) throw new InvalidOperationException("NO_ENVIRONMENTAL_DATA");

        // 1) Evaluate every individual hour once (ML + rules + safety per hour).
        var hourEvals = new List<HourlyEvaluation>();
        foreach (var hour in rangeHours)
        {
            var prediction = await ml.Predict(FeatureEngineering.From(hour), ct);
            var result = riskEngine.Evaluate(hour, profile, request.DurationMinutes, intensity, prediction, prediction is not null);
            hourEvals.Add(new(hour, result, prediction));
        }

        // 2) Build full activity windows: every covered hour must exist and pass safety.
        var byTimestamp = hourEvals.ToDictionary(e => e.Observation.Timestamp);
        var windows = new List<WindowEvaluation>();
        foreach (var start in rangeHours)
        {
            var covered = new List<HourlyEvaluation>();
            for (var i = 0; i < (int)Math.Ceiling(request.DurationMinutes / 60.0); i++)
                if (byTimestamp.TryGetValue(start.Timestamp.AddHours(i), out var e)) covered.Add(e);
            if (covered.Count > 0) windows.Add(WindowRiskEngine.Evaluate(start.Timestamp, request.DurationMinutes, covered));
        }

        var valid = windows.Where(w => !w.SafetyBlocked).OrderBy(w => w.AggregatedRisk).ToList();
        var choices = recommendationEngine.Build(valid, request.Preferences?.AvoidRain == true, request.Preferences?.AvoidPoorAirQuality == true, request.Preferences?.PreferCoolerWeather == true);
        var recommendations = choices.Select(choice =>
        {
            var worst = choice.Candidate.Hours.OrderByDescending(h => h.Risk.Score).First().Observation;
            return new RecommendationDto(choice.Type, choice.Title, choice.Candidate.Start.ToString("HH:mm"), choice.Candidate.End.ToString("HH:mm"),
                (int)Math.Round(choice.Candidate.AggregatedRisk), choice.Candidate.Level, worst.Temperature, worst.Humidity, worst.Aqi, worst.RainProbability, worst.WindSpeed, choice.Reason);
        }).ToList();

        var primary = valid.FirstOrDefault() ?? windows.OrderBy(w => w.AggregatedRisk).First();
        var allMl = hourEvals.Count > 0 && hourEvals.All(e => e.Prediction is not null);
        var score = (int)Math.Round(primary.AggregatedRisk);
        var env = primary.Hours.OrderByDescending(h => h.Risk.Score).First().Observation;
        var data = new AnalysisDataDto(requestId,
            new(request.Latitude, request.Longitude, weatherSeries.Timezone),
            new(request.Activity, request.Intensity, request.DurationMinutes),
            new(score, RiskEngine.Level(score), allMl ? "HYBRID" : "RULE_BASED_FALLBACK", allMl),
            new(allMl, allMl ? primary.Prediction?.RiskProbability : null, allMl ? primary.Prediction?.Confidence : null, allMl ? MlContract.ModelVersion : null, allMl ? MlContract.FeatureSchemaVersion : null),
            new(env.Temperature, env.ApparentTemperature, env.Humidity, env.Rain, env.RainProbability, env.PrecipitationAmount, env.Snowfall, env.WindSpeed, env.WindDirection, env.WeatherCode, env.Aqi, env.Pm25, env.Pm10, env.O3, env.No2),
            primary.Factors.Select(f => new FactorDto(f.Name, Math.Round(f.Score), f.Source, f.Explanation)).ToList(),
            recommendations,
            new(windows.Any(w => w.SafetyBlocked), valid.Count == 0, windows.SelectMany(w => w.SafetyReasons).Distinct().ToList()),
            hourEvals.Select(e => new HourlyAnalysisDto(e.Observation.Timestamp.ToString("HH:mm"), e.Observation.Temperature, e.Observation.Humidity, e.Observation.Aqi, e.Observation.RainProbability, e.Observation.WindSpeed, (int)Math.Round(e.Risk.Score), e.Risk.Level, !e.Risk.SafetyTriggered)).ToList(),
            new(true, airSeries is not null, allMl, dayHours.First().Timestamp.ToString("yyyy-MM-ddTHH:mm:ss")));

        await Persist(data, request, userId, ct);
        log.LogInformation("Analysis {RequestId} completed in {ElapsedMs} ms with {WindowCount} windows", requestId, stopwatch.ElapsedMilliseconds, windows.Count);
        return data;
    }

    private async Task Persist(AnalysisDataDto data, AnalysisRequest request, Guid userId, CancellationToken ct)
    {
        try
        {
            db.PlanningRequests.Add(new PlanningRequest
            {
                UserId = userId,
                RequestId = data.RequestId, Latitude = request.Latitude, Longitude = request.Longitude,
                Activity = request.Activity, Intensity = request.Intensity, DurationMinutes = request.DurationMinutes, Date = request.Date,
                PreferredStart = request.PreferredTimeRange?.Start, PreferredEnd = request.PreferredTimeRange?.End,
                RiskScore = data.Analysis.RiskScore, RiskLevel = data.Analysis.RiskLevel, DecisionMode = data.Analysis.DecisionMode,
                MlAvailable = data.Prediction.MlAvailable, ModelVersion = data.Prediction.ModelVersion, FeatureSchemaVersion = data.Prediction.FeatureSchemaVersion,
                RiskProbability = data.Prediction.RiskProbability, Confidence = data.Prediction.Confidence,
                EnvironmentJson = JsonSerializer.Serialize(data.Environment), FactorsJson = JsonSerializer.Serialize(data.Factors),
                RecommendationsJson = JsonSerializer.Serialize(data.Recommendations), SafetyJson = JsonSerializer.Serialize(data.Safety)
            });
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) { log.LogWarning(ex, "Could not persist analysis {RequestId}", data.RequestId); }
    }

    private static (int Start, int End) ParseRange(TimeRange? range)
    {
        if (range is null) return (0, 24);
        if (!TimeOnly.TryParseExact(range.Start, "HH:mm", out var start) || !TimeOnly.TryParseExact(range.End, "HH:mm", out var end) || start >= end) return (0, 24);
        return (start.Hour, end.Hour);
    }
    private static EnvironmentalObservation Merge(EnvironmentalObservation observation, AirQualitySeries? airSeries)
    {
        var point = airSeries?.Hourly.FirstOrDefault(x => x.Timestamp == observation.Timestamp);
        return point is null ? observation : observation with { Aqi = point.Aqi, Pm25 = point.Pm25, Pm10 = point.Pm10, O3 = point.O3, No2 = point.No2 };
    }
}

internal sealed class OpenMeteoWeatherResponse { public string? Timezone { get; set; } [JsonPropertyName("hourly")] public WeatherHourly Hourly { get; set; } = new(); }
internal sealed class WeatherHourly
{
    public List<string> Time { get; set; } = [];
    [JsonPropertyName("temperature_2m")] public List<double?> Temperature2m { get; set; } = [];
    [JsonPropertyName("apparent_temperature")] public List<double?> ApparentTemperature { get; set; } = [];
    [JsonPropertyName("relative_humidity_2m")] public List<double?> RelativeHumidity2m { get; set; } = [];
    [JsonPropertyName("precipitation_probability")] public List<double?> PrecipitationProbability { get; set; } = [];
    public List<double?> Precipitation { get; set; } = [];
    public List<double?> Rain { get; set; } = [];
    public List<double?> Snowfall { get; set; } = [];
    [JsonPropertyName("wind_speed_10m")] public List<double?> WindSpeed10m { get; set; } = [];
    [JsonPropertyName("wind_direction_10m")] public List<double?> WindDirection10m { get; set; } = [];
    [JsonPropertyName("weather_code")] public List<double?> WeatherCode { get; set; } = [];
}
internal sealed class OpenMeteoAirResponse { public string? Timezone { get; set; } [JsonPropertyName("hourly")] public AirHourly Hourly { get; set; } = new(); }
internal sealed class AirHourly
{
    public List<string> Time { get; set; } = [];
    [JsonPropertyName("pm2_5")] public List<double?> Pm25 { get; set; } = [];
    public List<double?> Pm10 { get; set; } = [];
    public List<double?> Ozone { get; set; } = [];
    [JsonPropertyName("nitrogen_dioxide")] public List<double?> NitrogenDioxide { get; set; } = [];
    [JsonPropertyName("european_aqi")] public List<double?> EuropeanAqi { get; set; } = [];
}
