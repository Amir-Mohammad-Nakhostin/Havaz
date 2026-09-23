using System.ComponentModel.DataAnnotations;
using Havaz.Domain;
namespace Havaz.Application;

public sealed record TimeRange([property: Required, RegularExpression("^\\d{2}:\\d{2}$")] string Start, [property: Required, RegularExpression("^\\d{2}:\\d{2}$")] string End);
public sealed record AnalysisPreferences(bool AvoidRain = false, bool AvoidPoorAirQuality = false, bool PreferCoolerWeather = false);
public sealed record AnalysisRequest([property: Range(-90,90)] double Latitude,[property: Range(-180,180)] double Longitude,[property: Required,MinLength(2)] string Activity,[property: Required] string Intensity,[property: Range(15,720)] int DurationMinutes,[property: Required] DateOnly Date,TimeRange? PreferredTimeRange,AnalysisPreferences? Preferences);
public sealed record LocationDto(double Latitude,double Longitude,string Timezone);
public sealed record ActivityDto(string Activity,string Intensity,int DurationMinutes);
public sealed record AnalysisSummaryDto(int RiskScore,string RiskLevel,string DecisionMode,bool MlAvailable);
public sealed record PredictionDto(bool MlAvailable,double? RiskProbability,double? Confidence,string? ModelVersion,string? FeatureSchemaVersion);
public sealed record EnvironmentDto(double? Temperature,double? ApparentTemperature,double? Humidity,double? Rain,double? RainProbability,double? PrecipitationAmount,double? Snowfall,double? WindSpeed,double? WindDirection,int? WeatherCode,double? Aqi,double? Pm25,double? Pm10,double? O3,double? No2);
public sealed record FactorDto(string Name,double Score,string Source,string Explanation);
public sealed record RecommendationDto(string Type,string Title,string Start,string End,int Score,string RiskLevel,double? Temperature,double? Humidity,double? Aqi,double? RainProbability,double? WindSpeed,string Reason);
public sealed record SafetyDto(bool Triggered,bool NoSafeSlot,IReadOnlyList<string> Reasons);
public sealed record HourlyAnalysisDto(string Time,double Temperature,double Humidity,double? Aqi,double RainProbability,double WindSpeed,int RiskScore,string RiskLevel,bool IsSafe);
public sealed record DataQualityDto(bool WeatherAvailable,bool AirQualityAvailable,bool MlAvailable,string DataTimestamp);
public sealed record AnalysisDataDto(string RequestId,LocationDto Location,ActivityDto Activity,AnalysisSummaryDto Analysis,PredictionDto Prediction,EnvironmentDto Environment,IReadOnlyList<FactorDto> Factors,IReadOnlyList<RecommendationDto> Recommendations,SafetyDto Safety,IReadOnlyList<HourlyAnalysisDto> HourlyAnalysis,DataQualityDto DataQuality);
public sealed record ApiError(string Code,string Message,object? Details=null);
public sealed record ApiEnvelope<T>(bool Success,T? Data,ApiError? Error,string RequestId);
public interface IAnalysisService { Task<AnalysisDataDto> Analyze(AnalysisRequest request, Guid userId, CancellationToken ct); }
public interface IWeatherProvider { Task<EnvironmentalSeries> GetWeather(double latitude,double longitude,CancellationToken ct); }
public interface IAirQualityProvider { Task<AirQualitySeries> GetAirQuality(double latitude,double longitude,CancellationToken ct); }
public interface IMlPredictionProvider { Task<MlPrediction?> Predict(MlFeatureVector features,CancellationToken ct); }
