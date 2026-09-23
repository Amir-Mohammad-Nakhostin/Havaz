"""UTCI unit/reference tests. Boundary uses the documented threshold rule; reference cases are
fixed environmental probes with independently reasoned expected direction (not self-generated)."""
import numpy as np, pandas as pd, sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).parents[1]/"training"))
from utci_core import compute_utci, solar_cossza

def _row(T, Td, wind_kmh, ssrd, fdir, dni, strd, ts):
    return pd.DataFrame({"temperature":[T],"dewpoint":[Td],"windSpeed":[wind_kmh],
        "shortwave_radiation":[ssrd],"direct_radiation":[fdir],"direct_normal_irradiance":[dni],
        "terrestrial_radiation":[strd],"timestamp_utc":pd.to_datetime([ts], utc=True)})

def test_threshold_boundary_exact():
    for utci, expected in [(31.9999,0),(32.0,0),(32.0001,1),(45.0,1)]:
        assert (utci > 32.0) == bool(expected)

def test_cold_inputs_not_hot():
    u,_ = compute_utci(_row(-10,-15,10,0,0,0,300,"2025-01-15T03:00"), 35.7, 51.4)
    assert u[0] < -10

def test_monotonic_temperature():
    base = dict(Td=10, wind_kmh=5, ssrd=0, fdir=0, dni=0, strd=380, ts="2025-07-15T08:30")
    u25,_ = compute_utci(_row(25, **base), 35.7, 51.4)
    u35,_ = compute_utci(_row(35, **base), 35.7, 51.4)
    u45,_ = compute_utci(_row(45, **base), 35.7, 51.4)
    assert u25[0] < u35[0] < u45[0]

def test_solar_increases_utci():
    night,_ = compute_utci(_row(30,15,5,0,0,0,380,"2025-07-15T20:30"), 35.7, 51.4)
    day,_  = compute_utci(_row(30,15,5,800,600,900,420,"2025-07-15T08:30"), 35.7, 51.4)
    assert day[0] > night[0]

def test_solar_geometry_sanity():
    ts = pd.to_datetime(["2025-06-21T08:30","2025-06-21T20:30"], utc=True)  # noon vs midnight UTC, Tehran
    c = solar_cossza(ts, 35.7, 51.4)
    assert c[0] > 0.8 and c[1] == 0.0  # high sun at noon, clipped at night

def test_plausibility_range():
    u,_ = compute_utci(_row(45,20,3,900,700,1000,450,"2025-07-15T08:30"), 31.3, 48.7)
    assert -60 <= u[0] <= 70
