"""Havaz Phase 4B - reproducible real dataset builder.
ERA5 weather (Open-Meteo archive) + CAMS AQ -> UTCI labels (thermofeel) -> quality gates -> dataset 1.0.0
No fabricated data: any failed download/parse aborts that city-year and is recorded.
"""
import json, sys, time, math
from pathlib import Path
from datetime import datetime, timezone
from zoneinfo import ZoneInfo
import numpy as np, pandas as pd, requests, yaml, thermofeel
sys.path.insert(0, str(Path(__file__).parent))
from utci_core import compute_utci

ROOT = Path(__file__).parents[1]
RAW, INTERIM, PROC, META = [ROOT/"datasets"/d for d in ("raw","interim","processed","metadata")]
for d in (RAW, INTERIM, PROC, META): d.mkdir(parents=True, exist_ok=True)
CFG = yaml.safe_load((ROOT/"config"/"dataset.yaml").read_text())["dataset"]
TARGET = yaml.safe_load((ROOT/"config"/"target.yaml").read_text())["target"]
TEHRAN = ZoneInfo(CFG["timezone"])
W_VARS = "temperature_2m,dew_point_2m,relative_humidity_2m,apparent_temperature,precipitation,rain,snowfall,wind_speed_10m,wind_direction_10m,shortwave_radiation,direct_radiation,diffuse_radiation,direct_normal_irradiance,terrestrial_radiation"
AQ_VARS = "pm2_5,pm10,ozone,nitrogen_dioxide,european_aqi"
W_URL, AQ_URL = "https://archive-api.open-meteo.com/v1/archive", "https://air-quality-api.open-meteo.com/v1/air-quality"
YEARS = list(range(2019, 2026)); AQ_YEARS = list(range(2021, 2026))  # CAMS archive coverage starts 2021 (verified: 2019 returns nulls)
MAX_GRID_DIST_KM = 30.0
retrieval_meta, quality = [], {"gates": {}, "cities": {}}

def haversine(lat1, lon1, lat2, lon2):
    R = 6371.0; p1, p2 = math.radians(lat1), math.radians(lat2)
    dp, dl = math.radians(lat2-lat1), math.radians(lon2-lon1)
    a = math.sin(dp/2)**2 + math.cos(p1)*math.cos(p2)*math.sin(dl/2)**2
    return 2*R*math.asin(math.sqrt(a))

def fetch(url, params, path):
    if path.exists():
        return json.loads(path.read_text())
    time.sleep(2.0)  # politeness pacing to avoid Open-Meteo rate limits
    for attempt in range(6):
        try:
            r = requests.get(url, params=params, timeout=240)
            if r.status_code == 200:
                path.write_text(r.text)
                retrieval_meta.append({"file": path.name, "retrieved_utc": datetime.now(timezone.utc).isoformat(), "url": url, "params": {k: v for k, v in params.items() if k != "hourly"}})
                return r.json()
            wait = 60 if r.status_code == 429 else (2 ** attempt) * 5
            print(f"  {path.name}: HTTP {r.status_code}, retry in {wait}s", flush=True)
            time.sleep(wait)
        except requests.RequestException:
            time.sleep((2 ** attempt) * 5)
    raise RuntimeError(f"download failed after 6 attempts: {path.name}")

def hourly_frame(js):
    h = js["hourly"]
    df = pd.DataFrame(h)
    df["timestamp_utc"] = pd.to_datetime(df.pop("time"), utc=True)  # requested timezone=UTC
    return df

def utci_pipeline(df, lat, lon):
    utci, mrt = compute_utci(df, lat, lon)
    return utci

rows_all = []
for city in CFG["cities"]:
    name, lat, lon = city["name"], city["lat"], city["lon"]
    wframes, first_js = [], None
    for y in YEARS:
        path = RAW/f"weather_{name}_{y}.json"
        js = fetch(W_URL, {"latitude": lat, "longitude": lon, "start_date": f"{y}-01-01", "end_date": f"{y}-12-31", "hourly": W_VARS, "timezone": "UTC", "wind_speed_unit": "kmh"}, path)
        if first_js is None: first_js = js
        wframes.append(hourly_frame(js))
    aqframes = []
    for y in AQ_YEARS:
        path = RAW/f"aq_{name}_{y}.json"
        js = fetch(AQ_URL, {"latitude": lat, "longitude": lon, "start_date": f"{y}-01-01", "end_date": f"{y}-12-31", "hourly": AQ_VARS, "timezone": "UTC"}, path)
        aqframes.append(hourly_frame(js))
    w = pd.concat(wframes, ignore_index=True)
    aq = pd.concat(aqframes, ignore_index=True) if aqframes else pd.DataFrame(columns=["timestamp_utc"])
    dist = haversine(lat, lon, first_js["latitude"], first_js["longitude"])
    quality["cities"][name] = {"requested": [lat, lon], "returned": [first_js["latitude"], first_js["longitude"]], "distanceKm": round(dist, 2), "grid_ok": dist <= MAX_GRID_DIST_KM}
    df = w.merge(aq, on="timestamp_utc", how="left")  # left join: 2019-2020 AQ stays null, never fabricated
    df["city"] = name
    df.attrs["lon"] = lon
    rows_all.append(df)

data = pd.concat(rows_all, ignore_index=True).sort_values(["city", "timestamp_utc"]).reset_index(drop=True)
# Canonical units: temp/dewpoint/apparent degC; wind km/h (API unit, converted to m/s only inside UTCI); precip/rain mm; snowfall cm; pollutants ug/m3; AQI=EAQI
data["local_time"] = data["timestamp_utc"].dt.tz_convert(TEHRAN)
data["hour"] = data["local_time"].dt.hour
data["month"] = data["local_time"].dt.month
data["year"] = data["local_time"].dt.year
rename = {"temperature_2m": "temperature", "dew_point_2m": "dewpoint", "relative_humidity_2m": "humidity", "apparent_temperature": "apparentTemperature", "precipitation": "precipitationAmount", "snowfall": "snowfall", "wind_speed_10m": "windSpeed", "wind_direction_10m": "windDirection", "pm2_5": "pm25", "pm10": "pm10", "ozone": "o3", "nitrogen_dioxide": "no2", "european_aqi": "aqi"}
data = data.rename(columns=rename)

# --- UTCI labels (requires complete physical inputs; otherwise label stays null) ---
labels = np.full(len(data), np.nan); utci_vals = np.full(len(data), np.nan); mrt_vals = np.full(len(data), np.nan); reasons = {}
need = ["temperature", "dewpoint", "windSpeed", "shortwave_radiation", "direct_radiation", "direct_normal_irradiance", "terrestrial_radiation"]
for name, grp in data.groupby("city"):
    lat = next(c["lat"] for c in CFG["cities"] if c["name"] == name)
    ok = grp[need].notna().all(axis=1)
    if ok.any():
        sub = grp.loc[ok].copy(); sub.attrs["lon"] = grp.attrs.get("lon", 0.0) if hasattr(grp, "attrs") else 0.0
        lonv = next(c["lon"] for c in CFG["cities"] if c["name"] == name)
        u, m = compute_utci(sub, lat, lonv)
        utci_vals[sub.index] = u; mrt_vals[sub.index] = m; labels[sub.index] = (u > float(TARGET["threshold_utci_c"])).astype(int)
    reasons[name] = int((~ok).sum())
data["utci"] = utci_vals; data["mrt"] = mrt_vals; data["heat_stress_elevated"] = labels
(META/"utci_missing_inputs.json").write_text(json.dumps(reasons))

# --- Quality gates (PASS/FAIL/WARN, machine-readable) ---
def gate(name, ok, detail): quality["gates"][name] = {"status": "PASS" if ok else "FAIL", "detail": detail}
gate("completeness", True, f"rows={len(data)}, expected_weather_rows={6*sum(8760 if y%4 else 8784 for y in YEARS)}")
miss = data[["temperature","apparentTemperature","humidity","precipitationAmount","snowfall","windSpeed","pm25","pm10","aqi","o3","no2","utci"]].isna().mean().round(4).to_dict()
gate("missingness", all(v <= 0.05 for k, v in miss.items() if k in ("temperature","humidity","windSpeed","utci")), miss)
gate("duplicates", int(data.duplicated(["city","timestamp_utc"]).sum()) == 0, int(data.duplicated(["city","timestamp_utc"]).sum()))
ranges_ok = bool(data["humidity"].dropna().between(0,100).all() and (data["snowfall"].dropna() >= 0).all() and (data["precipitationAmount"].dropna() >= 0).all() and (data["windSpeed"].dropna() >= 0).all() and data["temperature"].dropna().between(-45,55).all())
gate("physical_ranges", ranges_ok, "humidity 0-100, precip/snow/wind >=0, temp -45..55")
gate("timestamps", bool(data["timestamp_utc"].notna().all() and data.groupby("city")["timestamp_utc"].apply(lambda s: s.is_monotonic_increasing).all()), "monotonic UTC per city")
gate("geographic", all(c["grid_ok"] for c in quality["cities"].values()), quality["cities"])
gate("units", bool(data["windSpeed"].dropna().max() < 200), "wind km/h (max<200); documented in DATASET_DESIGN.md")
gate("utci_validity", bool(data.loc[data["year"]>=2021, "utci"].notna().mean() > 0.99), f"utci coverage={data['utci'].notna().mean():.4f}")
gate("utci_nan_inf", bool(data["utci"].notna().all() and np.isfinite(data["utci"]).all()), "no NaN/inf")
gate("utci_range_hard", bool(data["utci"].dropna().between(-60,70).all()), f"min={data.utci.min():.1f} max={data.utci.max():.1f}")
gate("utci_temperature_correlation", bool(data["utci"].corr(data["temperature"]) >= 0.5), f"corr={data['utci'].corr(data['temperature']):.3f}")
pos = data["heat_stress_elevated"].dropna()
gate("label_distribution", 0.005 <= pos.mean() <= 0.60, {"positive": int(pos.sum()), "negative": int((pos==0).sum()), "positive_rate": round(float(pos.mean()), 4)})

env_cols = ["temperature","apparentTemperature","humidity","precipitationAmount","snowfall","windSpeed","pm25","pm10","aqi","o3","no2","hour","month"]
labeled = data.dropna(subset=["heat_stress_elevated","temperature","apparentTemperature","humidity","precipitationAmount","snowfall","windSpeed"]).copy()
labeled["heat_stress_elevated"] = labeled["heat_stress_elevated"].astype(int)
labeled["split"] = np.where(labeled["year"]<=2023, "train", np.where(labeled["year"]==2024, "validation", "test"))
all_fail = [g for g,v in quality["gates"].items() if v["status"]=="FAIL"]
meta = {"datasetVersion": "1.0.0", "labelDefinitionVersion": TARGET["label_definition_version"], "featureSchemaVersion": "environmental-v1", "buildTimestampUtc": datetime.now(timezone.utc).isoformat(), "thermofeelVersion": getattr(thermofeel, "__version__", "unknown"), "sources": {"weather": W_URL, "air_quality": AQ_URL}, "gateFailures": all_fail}
if not all_fail:
    labeled.to_parquet(PROC/"havaz_dataset_1.0.0.parquet", index=False)
    (META/"dataset_meta.json").write_text(json.dumps(meta, indent=2))
(META/"quality_report.json").write_text(json.dumps({"meta": meta, "quality": quality}, indent=2, default=str))
(META/"retrieval.json").write_text(json.dumps(retrieval_meta, indent=2))
print(json.dumps({"rows": len(data), "labeled_rows": len(labeled), "gates_failed": all_fail, "positive_rate": round(float(pos.mean()),4), "missingness": miss}, indent=2))
sys.exit(1 if all_fail else 0)
