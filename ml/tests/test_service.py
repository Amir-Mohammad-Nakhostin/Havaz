import warnings
import pandas as pd
from fastapi.testclient import TestClient
from service.main import app, MODEL_VERSION, FEATURE_SCHEMA_VERSION, FEATURE_ORDER, MODEL_PATH, model

client = TestClient(app)
FEATURES = {"temperature":34.2,"apparentTemperature":38.1,"humidity":61,"precipitationAmount":0,"snowfall":0,"windSpeed":8.4,"pm25":42.1,"pm10":78.4,"aqi":96,"o3":30,"no2":20,"hour":14,"month":9,}

def payload(features=FEATURES): return {"modelVersion":MODEL_VERSION,"featureSchemaVersion":FEATURE_SCHEMA_VERSION,"features":features}

def test_health_model_loading_version_and_schema():
    response = client.get('/health')
    assert response.status_code == 200
    assert response.json() == {"status":"ok","modelLoaded":True,"modelVersion":MODEL_VERSION,"featureSchemaVersion":FEATURE_SCHEMA_VERSION}

def test_model_file_and_feature_order():
    assert MODEL_PATH.exists(); assert model is not None
    assert list(model.feature_names_in_) == FEATURE_ORDER

def test_valid_prediction_schema_and_no_sklearn_feature_name_warning():
    with warnings.catch_warnings(record=True) as caught:
        warnings.simplefilter('always')
        response = client.post('/predict-risk', json=payload())
    assert response.status_code == 200
    assert not [w for w in caught if 'does not have valid feature names' in str(w.message)]
    body = response.json()
    assert body['modelVersion'] == MODEL_VERSION and body['featureSchemaVersion'] == FEATURE_SCHEMA_VERSION
    assert 0 <= body['riskProbability'] <= 1 and 0 <= body['confidence'] <= 1 and body['riskClass'] in {'low','moderate','high'}

def test_invalid_feature_payload_rejected(): assert client.post('/predict-risk', json=payload({})).status_code == 422

def test_missing_feature_rejected():
    features = FEATURES.copy(); features.pop('aqi')
    assert client.post('/predict-risk', json=payload(features)).status_code == 422

def test_non_finite_feature_rejected():
    features = FEATURES.copy(); features['temperature'] = 'NaN'
    assert client.post('/predict-risk', json=payload(features)).status_code == 422

def test_schema_mismatch_rejected():
    bad = payload(); bad['featureSchemaVersion'] = 'wrong'
    assert client.post('/predict-risk', json=bad).status_code == 409

def test_named_dataframe_inference_direct():
    frame = pd.DataFrame([[FEATURES[name] for name in FEATURE_ORDER]], columns=FEATURE_ORDER)
    assert model.predict_proba(frame).shape == (1,2)
