import json, requests, pandas as pd, numpy as np
from pathlib import Path
import sys; sys.path.insert(0, str(Path(__file__).parent))
from utci_core import compute_utci
RAW = Path(__file__).parents[1]/"datasets/pilot"; RAW.mkdir(parents=True, exist_ok=True)
W_VARS = "temperature_2m,dew_point_2m,relative_humidity_2m,wind_speed_10m,shortwave_radiation,direct_radiation,direct_normal_irradiance,terrestrial_radiation"
def get(start, end):
    p = RAW/f"pilot_{start}_{end}.json"
    if not p.exists():
        r = requests.get("https://archive-api.open-meteo.com/v1/archive", params={"latitude":31.3183,"longitude":48.6706,"start_date":start,"end_date":end,"hourly":W_VARS,"timezone":"UTC","wind_speed_unit":"kmh"}, timeout=120)
        r.raise_for_status(); p.write_text(r.text)
    d = json.loads(p.read_text()); df = pd.DataFrame(d["hourly"])
    df["timestamp_utc"] = pd.to_datetime(df.pop("time"), utc=True)
    return df
frames = [get("2025-07-14","2025-07-20"), get("2025-01-14","2025-01-20")]
df = pd.concat(frames, ignore_index=True)
df = df.rename(columns={"temperature_2m":"temperature","dew_point_2m":"dewpoint","relative_humidity_2m":"humidity","wind_speed_10m":"windSpeed"})
df["utci"], df["mrt"] = compute_utci(df, 31.3183, 48.6706)
df["hour"] = df["timestamp_utc"].dt.tz_convert("Asia/Tehran").dt.hour
df["month"] = df["timestamp_utc"].dt.tz_convert("Asia/Tehran").dt.month
g = {}
g["nan_free"] = bool(df["utci"].notna().all() and np.isfinite(df["utci"]).all())
g["range_gate"] = bool(df["utci"].between(-60,70).all())
g["correlation_gate"] = float(df["utci"].corr(df["temperature"]))
summer = df[df.month==7]; winter = df[df.month==1]
g["summer_noon_utci_mean"] = round(float(summer[summer.hour.between(11,14)]["utci"].mean()),2)
g["winter_noon_utci_mean"] = round(float(winter[winter.hour.between(11,14)]["utci"].mean()),2)
g["night_utci_mean"] = round(float(df[df.hour.between(0,4)]["utci"].mean()),2)
g["summer_gt_winter"] = g["summer_noon_utci_mean"] > g["winter_noon_utci_mean"]
g["noon_gt_night"] = g["summer_noon_utci_mean"] > g["night_utci_mean"]
g["summer_noon_mrt_gt_t"] = bool((summer[summer.hour.between(11,14)]["mrt"] > summer[summer.hour.between(11,14)]["temperature"]).mean() > 0.5)
g["utci_stats"] = {"min":round(float(df.utci.min()),2),"mean":round(float(df.utci.mean()),2),"max":round(float(df.utci.max()),2)}
g["strong_heat_hours"] = int((df.utci>32).sum())
ok = g["nan_free"] and g["range_gate"] and g["correlation_gate"]>=0.5 and g["summer_gt_winter"] and g["noon_gt_night"]
g["PILOT"] = "PASS" if ok else "FAIL"
print(json.dumps(g, indent=2))
df.to_parquet(RAW/"pilot.parquet", index=False)
sys.exit(0 if ok else 1)
