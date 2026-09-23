"""Phase 4C validated UTCI core. Documented mapping from Open-Meteo/ERA5 archive variables
to thermofeel.calculate_mean_radiant_temperature inputs. All radiation as W/m2 hourly mean flux.
Assumptions (documented, see docs/UTCI_REFERENCE_IMPLEMENTATION.md):
  ssrd = shortwave_radiation; fdir = direct_radiation; dsrp = direct_normal_irradiance;
  ssr  = ssrd*(1-albedo), albedo=0.15; strd = terrestrial_radiation;
  strr = strd - eps*sigma*t2k^4 (eps=0.98); cossza = NOAA solar geometry (Spencer declination + EoT).
"""
import numpy as np, pandas as pd, thermofeel
SIGMA = 5.67e-8; ALBEDO = 0.15; EPS = 0.98

def solar_cossza(ts, lat, lon):
    ts = pd.Series(ts)
    doy = ts.dt.dayofyear.to_numpy(dtype=float); hour = ts.dt.hour.to_numpy(dtype=float) + 0.5
    g = 2*np.pi*(doy-1)/365.0
    decl = (0.006918 - 0.399912*np.cos(g) + 0.070257*np.sin(g) - 0.006758*np.cos(2*g)
            + 0.000907*np.sin(2*g) - 0.002697*np.cos(3*g) + 0.00148*np.sin(3*g))
    eot = 229.18*(0.000075 + 0.001868*np.cos(g) - 0.032077*np.sin(g) - 0.014615*np.cos(2*g) - 0.040849*np.sin(2*g))
    latr = np.radians(lat)
    tst = (hour*60 + eot + 4*lon) % 1440
    ha = np.radians(tst/4 - 180)
    return np.clip(np.sin(latr)*np.sin(decl) + np.cos(latr)*np.cos(decl)*np.cos(ha), 0.0, 1.0)

def compute_utci(df, lat, lon):
    t2k = df["temperature"].to_numpy(float) + 273.15
    tdk = df["dewpoint"].to_numpy(float) + 273.15
    va = df["windSpeed"].to_numpy(float) / 3.6
    ssrd = df["shortwave_radiation"].to_numpy(float)
    fdir = df["direct_radiation"].to_numpy(float)
    dsrp = df["direct_normal_irradiance"].to_numpy(float)
    strd = df["terrestrial_radiation"].to_numpy(float)
    ssr = ssrd*(1-ALBEDO)
    strr = strd - EPS*SIGMA*np.power(t2k, 4)
    cossza = solar_cossza(df["timestamp_utc"], lat, lon)
    mrt = thermofeel.calculate_mean_radiant_temperature(ssrd=ssrd, ssr=ssr, dsrp=dsrp, strd=strd, fdir=fdir, strr=strr, cossza=cossza)
    rh = thermofeel.calculate_relative_humidity_percent(t2k, tdk)
    ehpa = thermofeel.calculate_saturation_vapour_pressure(t2k)*rh/100.0
    utci = thermofeel.calculate_utci(t2k, va, mrt, ehPa=ehpa) - 273.15
    return utci, mrt - 273.15
