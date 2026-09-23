using Havaz.Domain;
using Xunit;

namespace Havaz.Backend.Tests;

public sealed class DomainTests
{
    [Fact]
    public void FeatureOrderMatchesTrainingContract()
        => Assert.Equal(new[]{"temperature","apparentTemperature","humidity","precipitationAmount","snowfall","windSpeed","pm25","pm10","aqi","o3","no2","hour","month"}, MlFeatureVector.Order);

    [Fact]
    public void HardSafetyBlocksExtremeHeat()
        => Assert.Contains(SafetyEvaluator.HardConstraints(Observation(apparent: 43)), r => r.Contains("۴۲"));

    [Fact]
    public void MultiHourWindowAggregatesAsMaxPlusBoundedExposure()
    {
        var hours = new[] { Hour(17, 20), Hour(18, 50), Hour(19, 30) };
        var window = WindowRiskEngine.Evaluate(new DateTime(2026, 9, 5, 17, 0, 0), 180, hours);
        Assert.Equal(56d, window.AggregatedRisk); // max(50) + (3-1)*3
        Assert.False(window.SafetyBlocked);
    }

    [Fact]
    public void MultiHourWindowIsInvalidIfAnyCoveredHourViolatesSafety()
    {
        var hours = new[] { Hour(17, 20), Hour(18, 30, apparent: 43), Hour(19, 30) };
        var window = WindowRiskEngine.Evaluate(new DateTime(2026, 9, 5, 17, 0, 0), 180, hours);
        Assert.True(window.SafetyBlocked);
        Assert.Equal(100d, window.AggregatedRisk);
    }

    [Fact]
    public void MultiHourWindowIsInvalidWhenHourlyCoverageIsIncomplete()
    {
        var window = WindowRiskEngine.Evaluate(new DateTime(2026, 9, 5, 22, 0, 0), 180, new[] { Hour(22, 20) });
        Assert.True(window.SafetyBlocked);
    }

    [Fact]
    public void RecommendationEngineReturnsThreeDistinctValidStrategies()
    {
        var engine = new RecommendationEngine();
        var windows = new[] { Window(6, 25), Window(10, 30), Window(18, 22) };
        var result = engine.Build(windows, avoidRain: true, avoidPoorAirQuality: true, preferCoolerWeather: true);
        Assert.Equal(3, result.Count);
        Assert.Equal(3, result.Select(x => x.Candidate.Start.Hour).Distinct().Count());
        Assert.Contains(result, x => x.Type == "SAFE");
        Assert.Contains(result, x => x.Type == "BALANCED");
        Assert.Contains(result, x => x.Type == "PREFERENCE_OPTIMIZED");
    }

    private static HourlyEvaluation Hour(int h, double risk, double apparent = 22)
        => new(Observation(hour: h, apparent: apparent), new RiskResult(risk, 10, 0, risk, RiskEngine.Level(risk), [], apparent >= 42, apparent >= 42 ? new List<string> { "گرمای شدید" } : []), new MlPrediction("1.2.0", "environmental-v1", .2, "low", .9));

    private static WindowEvaluation Window(int startHour, double risk)
        => WindowRiskEngine.Evaluate(new DateTime(2026, 9, 5, startHour, 0, 0), 60, new[] { Hour(startHour, risk) });

    private static EnvironmentalObservation Observation(int hour = 8, double temperature = 22, double apparent = 22, double wind = 5, double aqi = 20)
        => new(new DateTime(2026, 9, 5, hour, 0, 0), 29.5918, 52.5837, temperature, apparent, 40, 0, 0, 0, 0, wind, 180, 0, aqi, 10, 20, 30, 15);
}
