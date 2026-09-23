namespace Havaz.Domain;

public static class MlContract
{
    public const string ModelVersion = "1.2.0";
    public const string FeatureSchemaVersion = "environmental-v1";
}

public record EnvironmentalObservation(DateTime Timestamp,double Latitude,double Longitude,double Temperature,double ApparentTemperature,double Humidity,double Rain,double RainProbability,double PrecipitationAmount,double Snowfall,double WindSpeed,double WindDirection,int WeatherCode,double? Aqi,double? Pm25,double? Pm10,double? O3,double? No2);
public sealed record EnvironmentalSeries(string Timezone,IReadOnlyList<EnvironmentalObservation> Hourly);
public sealed record AirQualityPoint(DateTime Timestamp,double? Aqi,double? Pm25,double? Pm10,double? O3,double? No2);
public sealed record AirQualitySeries(string Timezone,IReadOnlyList<AirQualityPoint> Hourly);

public sealed record ActivityProfile(string Name,double TemperatureWeight,double HumidityWeight,double RainWeight,double SnowWeight,double WindWeight,double AirWeight,double DefaultIntensity)
{
    public static readonly IReadOnlyDictionary<string,ActivityProfile> Profiles = new Dictionary<string,ActivityProfile>(StringComparer.OrdinalIgnoreCase)
    { ["walking"]=new("walking",.22,.13,.18,.12,.08,.17,.25),["running"]=new("running",.34,.20,.15,.08,.10,.26,.80),["cycling"]=new("cycling",.25,.15,.22,.18,.18,.20,.65),["football"]=new("football",.31,.18,.20,.12,.12,.23,.85),["hiking"]=new("hiking",.28,.17,.20,.20,.16,.18,.60),["outdoor-work"]=new("outdoor-work",.30,.18,.17,.14,.14,.22,.55),["general"]=new("general",.24,.14,.17,.14,.10,.18,.35) };
    public static ActivityProfile Get(string? name)=>name is not null&&Profiles.TryGetValue(name,out var p)?p:Profiles["general"];
}

public sealed record MlFeatureVector(double Temperature,double ApparentTemperature,double Humidity,double PrecipitationAmount,double Snowfall,double WindSpeed,double Pm25,double Pm10,double Aqi,double O3,double No2,double Hour,double Month)
{
    public static readonly string[] Order=["temperature","apparentTemperature","humidity","precipitationAmount","snowfall","windSpeed","pm25","pm10","aqi","o3","no2","hour","month"];
    public Dictionary<string,double> ToDictionary()=>new(){["temperature"]=Temperature,["apparentTemperature"]=ApparentTemperature,["humidity"]=Humidity,["precipitationAmount"]=PrecipitationAmount,["snowfall"]=Snowfall,["windSpeed"]=WindSpeed,["pm25"]=Pm25,["pm10"]=Pm10,["aqi"]=Aqi,["o3"]=O3,["no2"]=No2,["hour"]=Hour,["month"]=Month};
}
public sealed record MlPrediction(string ModelVersion,string FeatureSchemaVersion,double RiskProbability,string RiskClass,double Confidence);
public sealed record RiskFactor(string Name,double Score,string Source,string Explanation);
public sealed record RiskResult(double BaselineRisk,double MlRisk,double InteractionPenalty,double Score,string Level,List<RiskFactor> Factors,bool SafetyTriggered,IReadOnlyList<string> SafetyReasons);
public sealed record HourlyEvaluation(EnvironmentalObservation Observation,RiskResult Risk,MlPrediction? Prediction);
public sealed record WindowEvaluation(DateTime Start,DateTime End,IReadOnlyList<HourlyEvaluation> Hours,double AggregatedRisk,string Level,bool SafetyBlocked,IReadOnlyList<string> SafetyReasons,IReadOnlyList<RiskFactor> Factors,MlPrediction? Prediction);
public sealed record RecommendationChoice(string Type,string Title,WindowEvaluation Candidate,string Reason);

public static class SafetyEvaluator
{
    public static IReadOnlyList<string> HardConstraints(EnvironmentalObservation e)
    {
        var reasons=new List<string>();
        if(e.ApparentTemperature>=42)reasons.Add("دمای محسوس بیش از ۴۲ درجه است.");
        if((e.Aqi??0)>=200)reasons.Add("کیفیت هوا در محدوده بسیار خطرناک است.");
        if(e.WindSpeed>=38)reasons.Add("سرعت باد برای فعالیت فضای باز خطرناک است.");
        if(e.WeatherCode is 95 or 96 or 99)reasons.Add("کد آب‌وهوا نشان‌دهنده توفان شدید است.");
        if(e.Snowfall>=5)reasons.Add("برف سنگین فعالیت را ناامن می‌کند.");
        return reasons;
    }
}

public static class FeatureEngineering
{
    public static double Intensity(string value)=>value.Trim().ToLowerInvariant() switch{"low" or "کم"=>.25,"moderate" or "متوسط"=>.55,"high" or "زیاد"=>.80,"very-high" or "very high" or "خیلی زیاد"=>1.0,_=>.55};
    public static MlFeatureVector From(EnvironmentalObservation e)=>new(e.Temperature,e.ApparentTemperature,e.Humidity,e.PrecipitationAmount,e.Snowfall,e.WindSpeed,e.Pm25??0,e.Pm10??0,e.Aqi??0,e.O3??0,e.No2??0,e.Timestamp.Hour,e.Timestamp.Month);
}

public sealed class RiskEngine
{
    private static double Clamp(double v)=>Math.Clamp(v,0,100);
    public RiskResult Evaluate(EnvironmentalObservation e,ActivityProfile p,double durationMinutes,double intensity,MlPrediction? prediction,bool mlAvailable)
    {
        var temperature=Clamp(Math.Abs(e.ApparentTemperature-22)*3);var humidity=Clamp(Math.Max(e.Humidity-55,0)*2);var rain=Clamp(e.RainProbability*.70+e.PrecipitationAmount*12);var snow=Clamp(e.Snowfall*25);var wind=Clamp(e.WindSpeed*2.2);var air=Clamp((e.Aqi??0)/2.2);var duration=Clamp(durationMinutes/2.4);var activity=Clamp(intensity*100);
        var factors=new List<RiskFactor>{new("دما",temperature,"RULE","فاصله دمای محسوس از محدوده آسایش"),new("رطوبت",humidity,"RULE","رطوبت بالا فشار فعالیت را افزایش می‌دهد"),new("بارش",rain,"RULE","ترکیب احتمال و مقدار بارش"),new("برف",snow,"RULE","مقدار برف و لغزندگی احتمالی"),new("باد",wind,"RULE","سرعت باد"),new("کیفیت هوا",air,"RULE",e.Aqi is null?"AQI در دسترس نبود":"AQI اروپایی نرمال‌شده"),new("مدت",duration,"RULE","افزایش مواجهه با افزایش مدت"),new("فعالیت",activity,"RULE","شدت فعالیت انتخاب‌شده")};
        var weightSum=p.TemperatureWeight+p.HumidityWeight+p.RainWeight+p.SnowWeight+p.WindWeight+p.AirWeight+.20;var baseline=Clamp((temperature*p.TemperatureWeight+humidity*p.HumidityWeight+rain*p.RainWeight+snow*p.SnowWeight+wind*p.WindWeight+air*p.AirWeight+duration*.10+activity*.10)/weightSum);
        var interaction=0.0;if(e.ApparentTemperature>32&&e.Humidity>65)interaction+=12;if(e.ApparentTemperature>34&&intensity>=.8)interaction+=8;if((e.Aqi??0)>120&&durationMinutes>90)interaction+=10;if(e.RainProbability>70&&p.Name=="cycling")interaction+=14;if(e.Snowfall>1&&p.Name=="cycling")interaction+=18;if(e.ApparentTemperature>35&&durationMinutes>120)interaction+=10;
        var safety=SafetyEvaluator.HardConstraints(e);var mlRisk=mlAvailable&&prediction is not null?prediction.RiskProbability*100:0;var hybrid=mlAvailable?baseline*.70+mlRisk*.30+interaction:baseline+interaction;var score=safety.Count>0?100:Clamp(hybrid);return new(Clamp(baseline+interaction),mlRisk,interaction,score,Level(score),factors,safety.Count>0,safety);
    }
    public static string Level(double score)=>score>80?"extreme":score>60?"high":score>40?"moderate":score>20?"low":"very-low";
}

public static class WindowRiskEngine
{
    public static WindowEvaluation Evaluate(DateTime start,int durationMinutes,IReadOnlyList<HourlyEvaluation> hours)
    {
        var required=(int)Math.Ceiling(durationMinutes/60.0);
        var blocked=hours.Count<required||hours.Any(h=>h.Risk.SafetyTriggered);
        var reasons=hours.SelectMany(h=>h.Risk.SafetyReasons).Distinct().ToList();
        if(hours.Count<required)reasons.Add("پوشش داده ساعتی برای کل بازه فعالیت کامل نیست.");
        // Prototype aggregation: the worst covered hour determines the window; a bounded duration-exposure term accounts for more covered hours.
        var maxRisk=hours.Count==0?100:hours.Max(h=>h.Risk.Score);
        var exposure=Math.Clamp((hours.Count-1)*3,0,12);
        var risk=blocked?100:Math.Clamp(maxRisk+exposure,0,100);
        var worst=hours.OrderByDescending(h=>h.Risk.Score).FirstOrDefault();
        var factors=hours.SelectMany(h=>h.Risk.Factors).GroupBy(f=>f.Name).Select(g=>g.OrderByDescending(f=>f.Score).First()).OrderByDescending(f=>f.Score).ToList();
        var mlProbability=hours.Select(h=>h.Prediction?.RiskProbability).Where(v=>v.HasValue).DefaultIfEmpty().Max();
        var mlConfidence=hours.Select(h=>h.Prediction?.Confidence).Where(v=>v.HasValue).DefaultIfEmpty().Average();
        var prediction=mlProbability.HasValue?new MlPrediction(MlContract.ModelVersion,MlContract.FeatureSchemaVersion,mlProbability.Value,worst?.Risk.Level??"unknown",mlConfidence??0):null;
        return new(start,start.AddMinutes(durationMinutes),hours,risk,RiskEngine.Level(risk),blocked,reasons,factors,prediction);
    }
}

public sealed class RecommendationEngine
{
    public IReadOnlyList<RecommendationChoice> Build(IReadOnlyList<WindowEvaluation> valid,bool avoidRain,bool avoidPoorAirQuality,bool preferCoolerWeather)
    {
        if(valid.Count==0)return[];var used=new HashSet<DateTime>();WindowEvaluation? Pick(Func<WindowEvaluation,double> score){var c=valid.Where(v=>!used.Contains(v.Start)).OrderBy(score).FirstOrDefault();if(c is not null)used.Add(c.Start);return c;}
        var safe=Pick(v=>v.AggregatedRisk);var balanced=Pick(v=>v.AggregatedRisk+Math.Abs(v.Start.Hour-10)*1.5);var preference=Pick(v=>PreferenceDistance(v,avoidRain,avoidPoorAirQuality,preferCoolerWeather));var choices=new List<RecommendationChoice>();
        if(safe is not null)choices.Add(new("SAFE","برنامه A — ایمن‌ترین",safe,"کمترین ریسک معتبر در بازه انتخاب‌شده"));
        if(balanced is not null)choices.Add(new("BALANCED","برنامه B — متعادل",balanced,"تعادل میان ساعت مناسب روز و ریسک محیطی"));
        if(preference is not null)choices.Add(new("PREFERENCE_OPTIMIZED","برنامه C — بهینه برای ترجیحات",preference,"نزدیک‌ترین زمان معتبر به ترجیحات شما"));
        return choices;
    }
    private static double PreferenceDistance(WindowEvaluation v,bool avoidRain,bool avoidPoorAirQuality,bool preferCooler){var o=v.Hours;var score=v.AggregatedRisk;if(avoidRain)score+=o.Average(h=>h.Observation.RainProbability)*.35;if(avoidPoorAirQuality)score+=o.Average(h=>h.Observation.Aqi??0)*.15;if(preferCooler)score+=o.Average(h=>Math.Max(h.Observation.ApparentTemperature-22,0))*1.2;return score;}
}
