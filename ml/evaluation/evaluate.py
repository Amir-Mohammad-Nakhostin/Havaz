"""Phase 4B evaluation on held-out 2025 test + Ahvaz geographic holdout + error analysis + calibration."""
import json
from pathlib import Path
import numpy as np, pandas as pd, joblib
from sklearn.metrics import precision_score, recall_score, f1_score, roc_auc_score, average_precision_score, brier_score_loss, confusion_matrix
from sklearn.calibration import calibration_curve
ROOT = Path(__file__).parents[1]
FEATURES = ["temperature","apparentTemperature","humidity","precipitationAmount","snowfall","windSpeed","pm25","pm10","aqi","o3","no2","hour","month"]
df = pd.read_parquet(ROOT/"datasets/processed/havaz_dataset_1.0.0.parquet")
for c in ["pm25","pm10","aqi","o3","no2"]:
    df[c] = df.groupby("city")[c].transform(lambda s: s.fillna(s.median())).fillna(0.0)
model = joblib.load(ROOT/"models/havaz_model_1.2.0_candidate.joblib")
def metrics(frame, name):
    X, y = frame[FEATURES], frame["heat_stress_elevated"].astype(int)
    p, pr = model.predict(X), model.predict_proba(X)[:,1]
    return {"set": name, "n": len(frame), "precision": round(precision_score(y,p),4), "recall": round(recall_score(y,p),4), "f1": round(f1_score(y,p),4), "roc_auc": round(roc_auc_score(y,pr),4), "pr_auc": round(average_precision_score(y,pr),4), "brier": round(brier_score_loss(y,pr),4), "confusion": confusion_matrix(y,p).tolist()}
test = df[df["split"]=="test"]
res = [metrics(test, "test_2025_all_cities")]
ahvaz = test[test["city"]=="Ahvaz"]; rest = test[test["city"]!="Ahvaz"]
res.append(metrics(ahvaz, "geographic_holdout_ahvaz_2025"))
res.append(metrics(rest, "test_2025_without_ahvaz"))
# calibration curve on test
pt, pp = calibration_curve(test["heat_stress_elevated"].astype(int), model.predict_proba(test[FEATURES])[:,1], n_bins=10)
cal = {"prob_true": pt.round(4).tolist(), "prob_pred": pp.round(4).tolist()}
# error analysis by city/month/temp band
t = test.copy(); t["pred"] = model.predict(t[FEATURES]); t["err"] = (t["pred"] != t["heat_stress_elevated"]).astype(int)
t["temp_band"] = pd.cut(t["temperature"], [-50,0,15,25,32,60], labels=["<0","0-15","15-25","25-32",">32"])
err = {"by_city": t.groupby("city")["err"].mean().round(4).to_dict(), "by_month": t.groupby("month")["err"].mean().round(4).to_dict(), "by_temp_band": t.groupby("temp_band", observed=True)["err"].mean().round(4).to_dict()}
# logistic coefficients
coef = model.named_steps["model"].coef_[0] if hasattr(model, "named_steps") else None
out = {"metrics": res, "calibration": cal, "error_analysis": err, "logistic_coefficients": dict(zip(FEATURES, np.round(coef,4).tolist())) if coef is not None else None}
(ROOT/"evaluation"/"phase4b_evaluation.json").write_text(json.dumps(out, indent=2, default=str))
print(json.dumps(res, indent=2))
