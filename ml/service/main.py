from pathlib import Path
from typing import Dict
import joblib, numpy as np, pandas as pd
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, Field

MODEL_VERSION = "1.2.0"
FEATURE_SCHEMA_VERSION = "environmental-v1"
FEATURE_ORDER = ["temperature","apparentTemperature","humidity","precipitationAmount","snowfall","windSpeed","pm25","pm10","aqi","o3","no2","hour","month"]
MODEL_PATH = Path(__file__).parents[1] / "models" / f"havaz_model_{MODEL_VERSION}.joblib"
model = joblib.load(MODEL_PATH) if MODEL_PATH.exists() else None
app = FastAPI(title="Havaz ML Service", version=MODEL_VERSION)

class PredictRequest(BaseModel):
    modelVersion: str = MODEL_VERSION
    featureSchemaVersion: str = FEATURE_SCHEMA_VERSION
    features: Dict[str, float] = Field(min_length=1)

@app.get("/health")
def health():
    return {"status":"ok", "modelLoaded": model is not None, "modelVersion": MODEL_VERSION, "featureSchemaVersion": FEATURE_SCHEMA_VERSION}

@app.post("/predict-risk")
def predict(req: PredictRequest):
    if model is None: raise HTTPException(503, "model unavailable")
    if req.modelVersion != MODEL_VERSION or req.featureSchemaVersion != FEATURE_SCHEMA_VERSION:
        raise HTTPException(409, "model or feature schema version mismatch")
    missing = [name for name in FEATURE_ORDER if name not in req.features]
    if missing: raise HTTPException(422, {"message":"missing features", "missing":missing})
    values = [float(req.features[name]) for name in FEATURE_ORDER]
    if not np.isfinite(values).all(): raise HTTPException(422, "features must be finite numbers")
    frame = pd.DataFrame([values], columns=FEATURE_ORDER)
    probability = float(model.predict_proba(frame)[0, 1])
    return {"modelVersion": MODEL_VERSION, "featureSchemaVersion": FEATURE_SCHEMA_VERSION, "riskProbability": round(probability,4), "riskClass": "high" if probability>=.6 else "moderate" if probability>=.4 else "low", "confidence": round(abs(probability-.5)*2,4)}
