using System.Net;
using System.Text;
using System.Text.Json;
using Havaz.Application;
using Havaz.Domain;
using Havaz.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Havaz.Backend.Tests;

public sealed class IntegrationTests
{
    [Fact]
    public async Task ControlledPipelineEvaluatesWindowsAndPersistsAnalysis()
    {
        await using var db = Db();
        var service = new AnalysisService(new FakeWeather(false), new FakeAir(), new FakeMl(available: true), db, NullLogger<AnalysisService>.Instance);
        var result = await service.Analyze(Request(), Guid.NewGuid(), CancellationToken.None);
        Assert.Equal("HYBRID", result.Analysis.DecisionMode);
        Assert.True(result.Prediction.MlAvailable);
        Assert.Equal("1.2.0", result.Prediction.ModelVersion);
        Assert.Equal("environmental-v1", result.Prediction.FeatureSchemaVersion);
        Assert.Equal(3, result.Recommendations.Count);
        Assert.Equal(3, result.Recommendations.Select(x => x.Start).Distinct().Count());
        Assert.False(result.Safety.NoSafeSlot);
        Assert.True(result.DataQuality.WeatherAvailable && result.DataQuality.AirQualityAvailable && result.DataQuality.MlAvailable);
        Assert.NotEmpty(result.HourlyAnalysis);
        var persisted = await db.PlanningRequests.SingleAsync();
        Assert.Equal(result.Analysis.RiskScore, persisted.RiskScore);
        Assert.Equal("1.2.0", persisted.ModelVersion);
        Assert.Equal(result.Recommendations.Count, JsonSerializer.Deserialize<JsonElement>(persisted.RecommendationsJson).GetArrayLength());
    }

    [Fact]
    public async Task NoSafeSlotWhenEveryWindowContainsUnsafeHour()
    {
        await using var db = Db();
        var service = new AnalysisService(new FakeWeather(true), new FakeAir(), new FakeMl(true), db, NullLogger<AnalysisService>.Instance);
        var result = await service.Analyze(Request(), Guid.NewGuid(), CancellationToken.None);
        Assert.Empty(result.Recommendations);
        Assert.True(result.Safety.NoSafeSlot);
        Assert.True(result.Safety.Triggered);
    }

    [Fact]
    public async Task MlOutageProducesRuleBasedFallbackWithoutFabricatedPrediction()
    {
        await using var db = Db();
        var service = new AnalysisService(new FakeWeather(false), new FakeAir(), new FakeMl(available: false), db, NullLogger<AnalysisService>.Instance);
        var result = await service.Analyze(Request(), Guid.NewGuid(), CancellationToken.None);
        Assert.Equal("RULE_BASED_FALLBACK", result.Analysis.DecisionMode);
        Assert.False(result.Prediction.MlAvailable);
        Assert.Null(result.Prediction.RiskProbability);
        Assert.NotEmpty(result.Recommendations);
    }

    [Theory]
    [InlineData("{\"modelVersion\":\"1.2.0\",\"featureSchemaVersion\":\"environmental-v1\",\"riskProbability\":2,\"riskClass\":\"high\",\"confidence\":0.9}")]
    [InlineData("{\"modelVersion\":\"1.2.0\",\"featureSchemaVersion\":\"environmental-v1\",\"riskProbability\":-1,\"riskClass\":\"low\",\"confidence\":0.2}")]
    [InlineData("{\"modelVersion\":\"1.2.0\",\"featureSchemaVersion\":\"environmental-v1\",\"riskProbability\":0.5,\"confidence\":9}")]
    [InlineData("{\"modelVersion\":\"1.0.0\",\"featureSchemaVersion\":\"environmental-v1\",\"riskProbability\":0.5,\"confidence\":0.5}")]
    [InlineData("not-json")]
    public async Task InvalidMlResponseIsRejectedByProvider(string body)
    {
        var provider = new PythonMlPredictionProvider(new HttpClient(new StubHandler(body)) { BaseAddress = new Uri("http://ml.test/") }, NullLogger<PythonMlPredictionProvider>.Instance);
        var result = await provider.Predict(new MlFeatureVector(25, 26, 40, 0, 0, 5, 10, 20, 30, 20, 15, 10, 9), CancellationToken.None);
        Assert.Null(result);
    }

    private static AnalysisRequest Request() => new(29.5918, 52.5837, "running", "high", 90, new DateOnly(2026, 9, 5), new TimeRange("06:00", "21:00"), new AnalysisPreferences(true, true, true));
    private static HavazDbContext Db() => new(new DbContextOptionsBuilder<HavazDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private sealed class StubHandler(string body) : HttpMessageHandler
        { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") }); }

    private sealed class FakeWeather(bool unsafeOnly) : IWeatherProvider
    {
        public Task<EnvironmentalSeries> GetWeather(double latitude, double longitude, CancellationToken ct)
            => Task.FromResult(new EnvironmentalSeries("Asia/Tehran", Enumerable.Range(6, 15).Select(h => Obs(h, unsafeOnly ? 45 : 20 + h % 5, unsafeOnly ? 45 : 20 + h % 5, unsafeOnly ? 5 : 6, 0)).ToList()));
        private static EnvironmentalObservation Obs(int hour, double t, double apparent, double wind, double aqi)
            => new(new DateTime(2026, 9, 5, hour, 0, 0), 29.5918, 52.5837, t, apparent, 40, 0, 0, 0, 0, wind, 180, 0, null, null, null, null, null);
    }
    private sealed class FakeAir : IAirQualityProvider
    {
        public Task<AirQualitySeries> GetAirQuality(double latitude, double longitude, CancellationToken ct)
            => Task.FromResult(new AirQualitySeries("Asia/Tehran", Enumerable.Range(6, 15).Select(h => new AirQualityPoint(new DateTime(2026, 9, 5, h, 0, 0), 20, 5, 10, 20, 10)).ToList()));
    }
    private sealed class FakeMl(bool available) : IMlPredictionProvider
    {
        public Task<MlPrediction?> Predict(MlFeatureVector features, CancellationToken ct)
            => Task.FromResult(available ? new MlPrediction("1.2.0", "environmental-v1", .2, "low", .9) : null);
    }
}
