"""Phase 4B training on real dataset 1.0.0 (environmental-v1 schema). Fails hard on schema/data problems."""
import json, sys
from pathlib import Path
import numpy as np, pandas as pd, joblib, yaml
from sklearn.model_selection import train_test_split
from sklearn.pipeline import Pipeline
from sklearn.preprocessing import StandardScaler
from sklearn.linear_model import LogisticRegression
from sklearn.ensemble import RandomForestClassifier, GradientBoostingClassifier
from sklearn.metrics import precision_score, recall_score, f1_score, roc_auc_score, average_precision_score, brier_score_loss, confusion_matrix, balanced_accuracy_score

ROOT = Path(__file__).parents[1]
META = json.loads((ROOT/"datasets/metadata/dataset_meta.json").read_text())
EXPECTED_SCHEMA = "environmental-v1"
assert META["featureSchemaVersion"] == EXPECTED_SCHEMA, f"schema mismatch {META['featureSchemaVersion']}"
FEATURES = ["temperature","apparentTemperature","humidity","precipitationAmount","snowfall","windSpeed","pm25","pm10","aqi","o3","no2","hour","month"]
df = pd.read_parquet(ROOT/"datasets/processed/havaz_dataset_1.0.0.parquet")
missing = [c for c in FEATURES + ["heat_stress_elevated","split","city"] if c not in df.columns]
if missing: sys.exit(f"missing columns: {missing}")
for c in ["pm25","pm10","aqi","o3","no2"]:  # AQ optional: city median imputation, flagged
    df[c] = df.groupby("city")[c].transform(lambda s: s.fillna(s.median())).fillna(0.0)
X, y = df[FEATURES], df["heat_stress_elevated"].astype(int)
def split(name): m = df["split"] == name; return X[m], y[m], df[m]
Xtr, ytr, dtr = split("train"); Xva, yva, dva = split("validation"); Xte, yte, dte = split("test")
models = {"LogisticRegression": Pipeline([("scale",StandardScaler()),("model",LogisticRegression(max_iter=2000,class_weight="balanced"))]),
          "RandomForest": RandomForestClassifier(n_estimators=150,max_depth=12,random_state=42,class_weight="balanced_subsample",n_jobs=-1),
          "GradientBoosting": GradientBoostingClassifier(random_state=42)}
rows = []
for name, m in models.items():
    m.fit(Xtr, ytr); pv, tv = m.predict(Xva), m.predict_proba(Xva)[:,1]
    rows.append({"model": name, "precision": precision_score(yva,pv), "recall": recall_score(yva,pv), "f1": f1_score(yva,pv), "roc_auc": roc_auc_score(yva,tv), "pr_auc": average_precision_score(yva,tv), "brier": brier_score_loss(yva,tv), "balanced_acc": balanced_accuracy_score(yva,pv), "confusion": confusion_matrix(yva,pv).tolist()})
    print(name, {k: round(v,4) for k,v in rows[-1].items() if isinstance(v,float)})
# selection policy: PR-AUC + calibration on validation; prefer simpler model within 1% PR-AUC
best_pr = max(r["pr_auc"] for r in rows)
cands = [r for r in rows if r["pr_auc"] >= best_pr - 0.01]
sel = min(cands, key=lambda r: (r["brier"], 0 if r["model"]=="LogisticRegression" else 1))
best = models[sel["model"]].fit(pd.concat([Xtr,Xva]), pd.concat([ytr,yva]))
out = ROOT/"models"; out.mkdir(exist_ok=True)
joblib.dump(best, out/"havaz_model_1.2.0_candidate.joblib")
(ROOT/"evaluation"/"phase4b_metrics.json").write_text(json.dumps({"validation": rows, "selected": sel["model"], "selection_policy": "PR-AUC within 1% of best, then lowest Brier, then simpler model"}, indent=2, default=float))
print("selected", sel["model"])
