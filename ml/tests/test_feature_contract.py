import json, pandas as pd
from pathlib import Path
ROOT = Path(__file__).parents[1]
def test_feature_schema_matches_dataset():
    meta = json.loads((ROOT/"datasets/metadata/dataset_meta.json").read_text())
    assert meta["featureSchemaVersion"] == "environmental-v1"
    df = pd.read_parquet(ROOT/"datasets/processed/havaz_dataset_1.0.0.parquet")
    for c in ["temperature","apparentTemperature","humidity","precipitationAmount","snowfall","windSpeed","pm25","pm10","aqi","o3","no2","hour","month","utci","mrt","heat_stress_elevated"]:
        assert c in df.columns
