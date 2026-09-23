export type User = {
  id: string;
  displayName: string;
  email: string;
  role: string;
  createdAt: string;
};

export type Plan = {
  type: string;
  title: string;
  start: string;
  end: string;
  score: number;
  riskLevel: string;
  temperature: number | null;
  humidity: number | null;
  aqi: number | null;
  rainProbability: number | null;
  windSpeed: number | null;
  reason: string;
};

export type HourlyPoint = {
  time: string;
  temperature: number;
  humidity: number;
  aqi: number | null;
  rainProbability: number;
  windSpeed: number;
  riskScore: number;
  riskLevel: string;
  isSafe: boolean;
};

export type AnalysisData = {
  requestId: string;
  location: {
    latitude: number;
    longitude: number;
    timezone: string;
  };
  analysis: {
    riskScore: number;
    riskLevel: string;
    decisionMode: string;
    mlAvailable: boolean;
  };
  prediction: {
    mlAvailable: boolean;
    riskProbability: number | null;
    confidence: number | null;
    modelVersion: string | null;
    featureSchemaVersion: string | null;
  };
  environment: {
    temperature: number | null;
    apparentTemperature: number | null;
    humidity: number | null;
    rain: number | null;
    rainProbability: number | null;
    precipitationAmount: number | null;
    snowfall: number | null;
    windSpeed: number | null;
    windDirection: number | null;
    weatherCode: number | null;
    aqi: number | null;
    pm25: number | null;
    pm10: number | null;
    o3: number | null;
    no2: number | null;
  };
  factors: {
    name: string;
    score: number;
    source: string;
    explanation: string;
  }[];
  recommendations: Plan[];
  safety: {
    triggered: boolean;
    noSafeSlot: boolean;
    reasons: string[];
  };
  hourlyAnalysis: HourlyPoint[];
  dataQuality: {
    weatherAvailable: boolean;
    airQualityAvailable: boolean;
    mlAvailable: boolean;
    dataTimestamp: string;
  };
};

export type AnalysisRequest = {
  latitude: number;
  longitude: number;
  activity: string;
  intensity: string;
  durationMinutes: number;
  date: string;
  preferredTimeRange?: {
    start: string;
    end: string;
  };
  preferences?: {
    avoidRain: boolean;
    avoidPoorAirQuality: boolean;
    preferCoolerWeather: boolean;
  };
};

export type Diagnostics = {
  aspNetApi: boolean;
  weatherProvider: boolean;
  airQualityProvider: boolean;
  mlService: boolean;
  database: boolean;
};

const base =
  import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000';

async function parseEnvelope<T>(response: Response): Promise<T> {
  const body = await response.json().catch(() => null);

  if (!response.ok || !body?.success) {
    throw new Error(
      body?.error?.message || 'ارتباط با سرور برقرار نشد.'
    );
  }

  return body.data as T;
}

function getCsrfToken(): string | null {
  const match = document.cookie
    .split('; ')
    .find((row) => row.startsWith('HAVAZ.CSRF='));

  if (!match) return null;

  return decodeURIComponent(match.substring('HAVAZ.CSRF='.length));
}

async function request<T>(
  path: string,
  options: RequestInit = {}
): Promise<T> {
  const method = (options.method || 'GET').toUpperCase();

  const headers = new Headers(options.headers);

  if (
    method !== 'GET' &&
    method !== 'HEAD' &&
    method !== 'OPTIONS'
  ) {
    const csrf = getCsrfToken();

    if (csrf) {
      headers.set('X-CSRF-TOKEN', csrf);
    }
  }

  const response = await fetch(`${base}${path}`, {
    ...options,
    headers,
    credentials: 'include',
  });

  return parseEnvelope<T>(response);
}

export function register(
  displayName: string,
  email: string,
  password: string,
  passwordConfirmation: string
): Promise<User> {
  return request<User>('/api/auth/register', {
    method: 'POST',
    headers: {
      'content-type': 'application/json',
    },
    body: JSON.stringify({
      displayName,
      email,
      password,
      passwordConfirmation,
    }),
  });
}

export function login(
  email: string,
  password: string
): Promise<User> {
  return request<User>('/api/auth/login', {
    method: 'POST',
    headers: {
      'content-type': 'application/json',
    },
    body: JSON.stringify({
      email,
      password,
    }),
  });
}

export function logout(): Promise<unknown> {
  return request('/api/auth/logout', {
    method: 'POST',
  });
}

export function getMe(): Promise<User> {
  return request<User>('/api/auth/me');
}

export function postAnalysis(
  req: AnalysisRequest
): Promise<AnalysisData> {
  return request<AnalysisData>('/api/analysis', {
    method: 'POST',
    headers: {
      'content-type': 'application/json',
    },
    body: JSON.stringify(req),
  });
}

export async function getDiagnostics(): Promise<Diagnostics> {
  const response = await fetch(`${base}/api/diagnostics`, {
    credentials: 'include',
  });

  if (!response.ok) {
    throw new Error('diagnostics unavailable');
  }

  return response.json() as Promise<Diagnostics>;
}