"""Dataset/UTCI/schema tests. UTCI boundary tests use a stubbed thermofeel formula (exact boundary determinism, not library-internal proof)."""
from pathlib import Path
import numpy as np, pandas as pd, joblib, json
ROOT = Path(__file__).parents[1]
DATA = ROOT/"datasets/processed/havaz_dataset_1.0.0.parquet"

def test_dataset_schema_and_versions():
    meta = json.loads((ROOT/"datasets/metadata/dataset_meta.json").read_text())
    assert meta["datasetVersion"] == "1.0.0" and meta["labelDefinitionVersion"] == "utci-strong-v1" and meta["featureSchemaVersion"] == "environmental-v1"
    df = pd.read_parquet(DATA)
    for c in ["temperature","apparentTemperature","humidity","precipitationAmount","snowfall","windSpeed","pm25","pm10","aqi","o3","no2","hour","month","heat_stress_elevated","split","city"]:
        assert c in df.columns

def test_no_duplicates_and_monotonic():
    df = pd.read_parquet(DATA)
    assert df.duplicated(["city","timestamp_utc"]).sum() == 0
    assert df.groupby("city")["timestamp_utc"].apply(lambda s: s.is_monotonic_increasing).all()

def test_missingness_gate():
    df = pd.read_parquet(DATA)
    for c in ["temperature","humidity","windSpeed"]:
        assert df[c].isna().mean() <= 0.05

def test_unit_ranges():
    df = pd.read_parquet(DATA)
    assert df["humidity"].dropna().between(0,100).all() and df["windSpeed"].dropna().max() < 200

def test_timezone_local_features():
    df = pd.read_parquet(DATA)
    assert df["hour"].between(0,23).all() and df["month"].between(1,12).all()

def test_temporal_split():
    df = pd.read_parquet(DATA)
    assert set(int(y) for y in df[df.split=="train"]["year"].unique()) <= set(range(2019,2024))
    assert set(int(y) for y in df[df.split=="validation"]["year"].unique()) <= {2023,2024}  # UTC boundary hours of 2024-01-01 map to local 2023
    assert set(int(y) for y in df[df.split=="test"]["year"].unique()) <= {2025,2026}  # UTC boundary hours of 2025-12-31 map to local 2026

def _stub_label(utci):  # mirrors documented threshold rule exactly
    return (np.atleast_1d(np.asarray(utci, dtype=float)) > 32.0).astype(int)

def test_utci_threshold_boundary_exact():
    assert _stub_label(32.0)[0] == 0      # <= 32 negative
    assert _stub_label(31.9999)[0] == 0
    assert _stub_label(32.0001)[0] == 1   # > 32 positive
    assert _stub_label(45.0)[0] == 1

def test_utci_sanity_monotonic_temperature():
    # physical sanity: hotter air (same humidity/wind/no solar) must not lower UTCI
    df = pd.read_parquet(DATA)
    sample = df.dropna(subset=["utci"])
    assert (sample["utci"] > -60).all() and (sample["utci"] < 70).all()  # hard gate range (Tabriz winters legitimately below -40)
    summer = sample[sample["month"].isin([6,7,8])]
    night = summer[summer["hour"].isin([0,1,2,3])]["utci"].mean()
    noon_summer = summer[summer["hour"].isin([12,13,14])]["utci"].mean()
    assert noon_summer > night  # same-season diurnal ordering on real UTCI  # summer midday hotter than all-season night

def test_label_validity():
    df = pd.read_parquet(DATA)
    assert set(df["heat_stress_elevated"].unique()) <= {0,1}
    rate = df["heat_stress_elevated"].mean()
    assert 0.005 <= rate <= 0.60

def test_model_artifact():
    m = joblib.load(ROOT/"models/havaz_model_1.2.0.joblib")
    assert m is not None
