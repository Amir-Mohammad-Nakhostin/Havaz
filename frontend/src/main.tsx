import React, { useState } from 'react';
import { createRoot } from 'react-dom/client';
import {
  QueryClient,
  QueryClientProvider,
  useMutation,
  useQuery,
} from '@tanstack/react-query';
import {
  Activity,
  CloudSun,
  Wind,
  MapPin,
  ChevronLeft,
  ShieldCheck,
  Sun,
  Moon,
  Clock3,
  Info,
  RefreshCcw,
  TriangleAlert,
  LogIn,
  UserPlus,
  LogOut,
  User,
  Gauge,
  Sparkles,
  CalendarDays,
  Timer,
  CheckCircle2,
  Database,
  BrainCircuit,
  ArrowDownLeft,
  LayoutDashboard,
  History,
  BookmarkCheck,
  Menu,
  X,
} from 'lucide-react';
import {
  LineChart,
  Line,
  XAxis,
  YAxis,
  Tooltip,
  ResponsiveContainer,
} from 'recharts';
import {
  getDiagnostics,
  getMe,
  login,
  logout,
  postAnalysis,
  register,
  type AnalysisData,
  type HourlyPoint,
  type User as UserDto,
} from './api';
import './styles.css';

const faNum = (n: number | string) =>
  String(n).replace(/\d/g, (d) => '۰۱۲۳۴۵۶۷۸۹'[+d]);

const levelFa = (value: string): string => {
  const labels: Record<string, string> = {
    extreme: 'بحرانی',
    high: 'بالا',
    moderate: 'متوسط',
    low: 'کم',
    'very-low': 'خیلی کم',
  };

  return labels[value] ?? value;
};

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      retry: 1,
    },
    mutations: {
      retry: false,
    },
  },
});

function AuthScreen({
  onAuthenticated,
}: {
  onAuthenticated: (user: UserDto) => void;
}) {
  const [mode, setMode] = useState<'login' | 'register'>('login');
  const [displayName, setDisplayName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [passwordConfirmation, setPasswordConfirmation] = useState('');
  const [error, setError] = useState<string | null>(null);

  const mutation = useMutation({
    mutationFn: async () => {
      setError(null);

      if (mode === 'register') {
        if (!displayName.trim()) {
          throw new Error('نام نمایشی را وارد کنید.');
        }

        if (password !== passwordConfirmation) {
          throw new Error('تکرار رمز عبور با رمز عبور یکسان نیست.');
        }

        return register(
          displayName.trim(),
          email.trim(),
          password,
          passwordConfirmation
        );
      }

      return login(email.trim(), password);
    },

    onSuccess: (user) => {
      queryClient.setQueryData(['me'], user);
      onAuthenticated(user);
    },

    onError: (err) => {
      setError(
        err instanceof Error
          ? err.message
          : 'عملیات احراز هویت انجام نشد.'
      );
    },
  });

  const isRegister = mode === 'register';

  return (
    <div className="authApp">
      <div className="authBackgroundGlow authGlowOne" />
      <div className="authBackgroundGlow authGlowTwo" />

      <main className="authShell">
        <section className="authCard">
          <div className="authBrand">
            <span className="brandmark">
              <CloudSun size={24} />
            </span>

            <div>
              <strong>HAVAZ</strong>
              <small>Outdoor Intelligence</small>
            </div>
          </div>

          <div className="authHeading">
            <div className="eyebrow">
              <span className="dot" />
              تصمیم‌یار فعالیت‌های فضای باز
            </div>

            <h1>
              {isRegister
                ? 'برای تصمیم‌های بهتر آماده‌ای؟'
                : 'دوباره به HAVAZ خوش آمدی'}
            </h1>

            <p>
              {isRegister
                ? 'یک حساب بساز و تحلیل‌های محیطی خودت را در یک فضای امن مدیریت کن.'
                : 'وارد حساب خود شو تا تحلیل‌های محیطی را اجرا و تاریخچه آن‌ها را مدیریت کنی.'}
            </p>
          </div>

          <div className="authTabs">
            <button
              type="button"
              className={!isRegister ? 'active' : ''}
              onClick={() => {
                setMode('login');
                setError(null);
              }}
            >
              <LogIn size={15} />
              ورود
            </button>

            <button
              type="button"
              className={isRegister ? 'active' : ''}
              onClick={() => {
                setMode('register');
                setError(null);
              }}
            >
              <UserPlus size={15} />
              ثبت‌نام
            </button>
          </div>

          <form
            className="authForm"
            onSubmit={(event) => {
              event.preventDefault();
              mutation.mutate();
            }}
          >
            {isRegister && (
              <label>
                نام نمایشی
                <div className="inputShell">
                  <User size={16} />
                  <input
                    type="text"
                    value={displayName}
                    onChange={(event) =>
                      setDisplayName(event.target.value)
                    }
                    placeholder="مثلاً امیر"
                    autoComplete="name"
                    required
                  />
                </div>
              </label>
            )}

            <label>
              ایمیل
              <div className="inputShell ltr">
                <span className="inputPrefix">@</span>
                <input
                  type="email"
                  value={email}
                  onChange={(event) =>
                    setEmail(event.target.value)
                  }
                  placeholder="you@example.com"
                  autoComplete="email"
                  required
                />
              </div>
            </label>

            <label>
              رمز عبور
              <div className="inputShell ltr">
                <ShieldCheck size={16} />
                <input
                  type="password"
                  value={password}
                  onChange={(event) =>
                    setPassword(event.target.value)
                  }
                  placeholder="رمز عبور"
                  autoComplete={
                    isRegister
                      ? 'new-password'
                      : 'current-password'
                  }
                  required
                />
              </div>
            </label>

            {isRegister && (
              <label>
                تکرار رمز عبور
                <div className="inputShell ltr">
                  <ShieldCheck size={16} />
                  <input
                    type="password"
                    value={passwordConfirmation}
                    onChange={(event) =>
                      setPasswordConfirmation(event.target.value)
                    }
                    placeholder="تکرار رمز عبور"
                    autoComplete="new-password"
                    required
                  />
                </div>
              </label>
            )}

            {error && (
              <div className="authError" role="alert">
                <TriangleAlert size={16} />
                <span>{error}</span>
              </div>
            )}

            <button
              className="primary authSubmit"
              type="submit"
              disabled={mutation.isPending}
            >
              {mutation.isPending ? (
                <>
                  <RefreshCcw className="spin" size={17} />
                  در حال بررسی…
                </>
              ) : (
                <>
                  {isRegister ? 'ساخت حساب' : 'ورود به HAVAZ'}
                  <ArrowDownLeft size={17} />
                </>
              )}
            </button>
          </form>

          <div className="authSecurity">
            <div className="securityIcon">
              <ShieldCheck size={17} />
            </div>

            <div>
              <strong>احراز هویت امن</strong>
              <span>
                نشست کاربر در سمت سرور مدیریت می‌شود.
              </span>
            </div>
          </div>
        </section>

        <div className="authFooter">
          <span>HAVAZ</span>
          <span>Environmental Decision Intelligence</span>
        </div>
      </main>
    </div>
  );
}

function App() {
  /*
   * IMPORTANT:
   * All hooks are executed on every render.
   * There is deliberately NO hook after a conditional return.
   * This fixes the React "Rendered more hooks than during the previous render"
   * error that caused the blank screen after login.
   */

  const meQuery = useQuery({
    queryKey: ['me'],
    queryFn: getMe,
    retry: false,
  });

  const [dark, setDark] = useState(false);
  const [result, setResult] = useState<AnalysisData | null>(null);
  const [selected, setSelected] = useState<HourlyPoint | null>(null);
  const [mobileNavOpen, setMobileNavOpen] = useState(false);

  const [activity, setActivity] = useState('running');
  const [intensity, setIntensity] = useState('moderate');
  const [duration, setDuration] = useState(90);
  const [date, setDate] = useState('2026-09-02');

  const mutation = useMutation({
    mutationFn: postAnalysis,

    onSuccess: (data) => {
      setResult(data);
      setSelected(data.hourlyAnalysis[0] ?? null);
    },

    onError: () => {
      setSelected(null);
    },
  });

  const diagnosticsQuery = useQuery({
    queryKey: ['diagnostics'],
    queryFn: getDiagnostics,
    enabled: import.meta.env.DEV && !!meQuery.data,
    retry: 0,
  });

  const analyze = () => {
    mutation.mutate({
      latitude: 29.5918,
      longitude: 52.5837,
      activity,
      intensity,
      durationMinutes: duration,
      date,
      preferredTimeRange: {
        start: '06:00',
        end: '21:00',
      },
      preferences: {
        avoidRain: true,
        avoidPoorAirQuality: true,
        preferCoolerWeather: true,
      },
    });
  };

  const handleLogout = async () => {
    try {
      await logout();
    } finally {
      queryClient.clear();
      setResult(null);
      setSelected(null);
    }
  };

  const loading = mutation.isPending;

  const error =
    mutation.error instanceof Error
      ? mutation.error.message
      : null;

  const chart = result?.hourlyAnalysis ?? [];

  const diagnostics = diagnosticsQuery.data;

  const diagStatus = (value?: boolean) =>
    diagnosticsQuery.isError
      ? 'قطع'
      : value === undefined
        ? 'در انتظار'
        : value
          ? 'متصل'
          : 'قطع';

  if (meQuery.isPending) {
    return (
      <div className={dark ? 'app dark' : 'app'}>
        <div className="loading authLoading">
          <RefreshCcw className="spin" size={18} />
          در حال بررسی نشست کاربر…
        </div>
      </div>
    );
  }

  if (!meQuery.data) {
    return (
      <AuthScreen
        onAuthenticated={(user) => {
          queryClient.setQueryData(['me'], user);
        }}
      />
    );
  }

  const user = meQuery.data;

  return (
    <div className={dark ? 'app dark' : 'app'}>
      <header className="mainHeader">
        <div className="headerInner">
          <div className="brand">
            <span className="brandmark">
              <CloudSun size={21} />
            </span>

            <div className="brandText">
              <span>HAVAZ</span>
              <small>Outdoor Intelligence</small>
            </div>
          </div>

          <nav className={`mainNav ${mobileNavOpen ? 'open' : ''}`}>
            <a className="active">
              <LayoutDashboard size={14} />
              داشبورد
            </a>

            <a>
              <History size={14} />
              تاریخچه تحلیل‌ها
            </a>

            <a>
              <BookmarkCheck size={14} />
              برنامه‌های ذخیره‌شده
            </a>
          </nav>

          <div className="headerActions">
            <div className="userBadge">
              <span className="userAvatar">
                {user.displayName?.charAt(0) ?? 'U'}
              </span>

              <span className="userName">
                {user.displayName}
              </span>
            </div>

            <button
              className="iconBtn themeBtn"
              onClick={() => setDark((value) => !value)}
              aria-label="تغییر پوسته"
              title="تغییر پوسته"
            >
              {dark ? <Sun size={17} /> : <Moon size={17} />}
            </button>

            <button
              className="iconBtn logoutIcon"
              onClick={handleLogout}
              aria-label="خروج"
              title="خروج"
            >
              <LogOut size={17} />
            </button>

            <button
              className="iconBtn mobileNavToggle"
              onClick={() => setMobileNavOpen((open) => !open)}
              aria-label="باز/بسته کردن منو"
            >
              {mobileNavOpen ? <X size={17} /> : <Menu size={17} />}
            </button>
          </div>
        </div>
      </header>

      <main className="dashboardMain">
        {import.meta.env.DEV && (
          <section
            className="diagnostics"
            aria-label="وضعیت سرویس‌های توسعه"
          >
            <div className="diagnosticsTitle">
              <Database size={14} />
              وضعیت زیرساخت
            </div>

            <span>
              API
              <b className={diagnostics?.aspNetApi ? 'ok' : ''}>
                {diagStatus(diagnostics?.aspNetApi)}
              </b>
            </span>

            <span>
              Weather
              <b
                className={
                  diagnostics?.weatherProvider ? 'ok' : ''
                }
              >
                {diagStatus(diagnostics?.weatherProvider)}
              </b>
            </span>

            <span>
              AQ
              <b
                className={
                  diagnostics?.airQualityProvider ? 'ok' : ''
                }
              >
                {diagStatus(
                  diagnostics?.airQualityProvider
                )}
              </b>
            </span>

            <span>
              ML
              <b className={diagnostics?.mlService ? 'ok' : ''}>
                {diagStatus(diagnostics?.mlService)}
              </b>
            </span>

            <span>
              PostgreSQL
              <b className={diagnostics?.database ? 'ok' : ''}>
                {diagStatus(diagnostics?.database)}
              </b>
            </span>
          </section>
        )}

        <section className="hero heroModern">
          <div className="heroCopy">
            <div className="eyebrow">
              <span className="dot" />
              تصمیم‌یار هوشمند فعالیت‌های فضای باز
            </div>

            <h1>
              قبل از اینکه
              <br />
              <em>بیرون بروی، هوا را بسنج.</em>
            </h1>

            <p>
              HAVAZ شرایط محیطی را تحلیل می‌کند، ریسک فعالیت را
              محاسبه می‌کند و بهترین بازه زمانی را پیشنهاد می‌دهد.
            </p>

            <div className="heroActions">
              <button
                className="primary heroCta"
                onClick={() =>
                  document
                    .getElementById('planner')
                    ?.scrollIntoView({ behavior: 'smooth' })
                }
              >
                شروع تحلیل
                <ChevronLeft size={18} />
              </button>

              <div className="heroTrust">
                <ShieldCheck size={17} />
                <span>
                  مدل محیطی + موتور قوانین ایمنی
                </span>
              </div>
            </div>
          </div>

          <div className="heroVisual">
            <div className="heroOrbit orbitOne" />
            <div className="heroOrbit orbitTwo" />

            <div className="orbModern">
              <div className="orbGlow">
                <Sun size={44} />
              </div>

              <strong>شرایط محیطی</strong>
              <span>در حال پایش</span>
            </div>

            <div className="heroFloatingCard heroFloatTop">
              <div className="floatIcon">
                <BrainCircuit size={17} />
              </div>

              <div>
                <strong>HYBRID</strong>
                <span>ML + Safety Rules</span>
              </div>
            </div>

            <div className="heroFloatingCard heroFloatBottom">
              <CheckCircle2 size={17} />
              <span>تحلیل چندساعته</span>
            </div>
          </div>
        </section>

        <section className="statsModern">
          <div className="statCard">
            <div className="statIcon">
              <MapPin size={17} />
            </div>

            <div>
              <span>موقعیت تحلیل</span>
              <strong>شیراز، ایران</strong>
            </div>

            <small>۲۹.۵۹°N · ۵۲.۵۸°E</small>
          </div>

          <div className="statCard">
            <div className="statIcon warm">
              <Sun size={17} />
            </div>

            <div>
              <span>دمای محیط</span>
              <strong>
                {result?.environment.temperature != null
                  ? `${faNum(
                      Math.round(
                        result.environment.temperature
                      )
                    )}°C`
                  : '—'}
              </strong>
            </div>

            <small>
              {result
                ? 'داده زنده'
                : 'منتظر تحلیل'}
            </small>
          </div>

          <div className="statCard">
            <div className="statIcon air">
              <Wind size={17} />
            </div>

            <div>
              <span>کیفیت هوا</span>
              <strong>
                {result &&
                result.dataQuality.airQualityAvailable &&
                result.environment.aqi != null
                  ? `AQI ${faNum(
                      Math.round(result.environment.aqi)
                    )}`
                  : '—'}
              </strong>
            </div>

            <small>
              {result?.dataQuality.airQualityAvailable
                ? 'CAMS'
                : 'در انتظار تحلیل'}
            </small>
          </div>

          <div className="statCard">
            <div className="statIcon risk">
              <ShieldCheck size={17} />
            </div>

            <div>
              <span>ریسک کلی</span>
              <strong>
                {result
                  ? `${faNum(result.analysis.riskScore)} / ۱۰۰`
                  : '—'}
              </strong>
            </div>

            <small>
              {result
                ? levelFa(result.analysis.riskLevel)
                : '—'}
            </small>
          </div>
        </section>

        <div className="sectionHead sectionHeadModern">
          <div>
            <div className="eyebrow">
              <Sparkles size={14} />
              برنامه‌ریزی هوشمند
            </div>

            <h2>فعالیتت را تعریف کن</h2>

            <p>
              HAVAZ شرایط محیطی بازه انتخابی را بررسی می‌کند.
            </p>
          </div>

          <div className="sectionMeta">
            <span>
              <MapPin size={13} />
              شیراز
            </span>

            <span>
              <Clock3 size={13} />
              ۰۶:۰۰ تا ۲۱:۰۰
            </span>
          </div>
        </div>

        <section className="workspace workspaceModern" id="planner">
          <div className="panel plannerPanel">
            <div className="panelHeaderModern">
              <div className="panelNumber">01</div>

              <div>
                <span className="panelKicker">
                  ACTIVITY PROFILE
                </span>

                <h3>مشخصات فعالیت</h3>
              </div>
            </div>

            <div className="formStack">
              <label>
                <span className="fieldLabel">
                  موقعیت مکانی
                  <small>موقعیت فعلی تحلیل</small>
                </span>

                <div className="inputShell">
                  <MapPin size={16} />

                  <input
                    value="شیراز، ایران · ۲۹.۵۹ / ۵۲.۵۸"
                    readOnly
                  />
                </div>
              </label>

              <div className="twoModern">
                <label>
                  <span className="fieldLabel">
                    فعالیت
                  </span>

                  <div className="selectShell">
                    <Activity size={15} />

                    <select
                      value={activity}
                      onChange={(e) =>
                        setActivity(e.target.value)
                      }
                    >
                      <option value="walking">
                        پیاده‌روی
                      </option>

                      <option value="running">
                        دویدن
                      </option>

                      <option value="cycling">
                        دوچرخه‌سواری
                      </option>

                      <option value="football">
                        فوتبال
                      </option>

                      <option value="hiking">
                        کوهنوردی
                      </option>

                      <option value="outdoor-work">
                        کار در فضای باز
                      </option>
                    </select>
                  </div>
                </label>

                <label>
                  <span className="fieldLabel">
                    شدت فعالیت
                  </span>

                  <div className="selectShell">
                    <Gauge size={15} />

                    <select
                      value={intensity}
                      onChange={(e) =>
                        setIntensity(e.target.value)
                      }
                    >
                      <option value="low">کم</option>
                      <option value="moderate">
                        متوسط
                      </option>
                      <option value="high">زیاد</option>
                      <option value="very-high">
                        خیلی زیاد
                      </option>
                    </select>
                  </div>
                </label>
              </div>

              <div className="twoModern">
                <label>
                  <span className="fieldLabel">
                    مدت فعالیت
                  </span>

                  <div className="inputShell">
                    <Timer size={15} />

                    <input
                      type="number"
                      min={15}
                      max={720}
                      value={duration}
                      onChange={(e) =>
                        setDuration(
                          Number(e.target.value)
                        )
                      }
                    />

                    <span className="inputUnit">
                      دقیقه
                    </span>
                  </div>
                </label>

                <label>
                  <span className="fieldLabel">
                    تاریخ
                  </span>

                  <div className="inputShell">
                    <CalendarDays size={15} />

                    <input
                      type="date"
                      value={date}
                      onChange={(e) =>
                        setDate(e.target.value)
                      }
                    />
                  </div>
                </label>
              </div>

              <div className="preferenceBox">
                <div className="preferenceIcon">
                  <ShieldCheck size={17} />
                </div>

                <div>
                  <strong>فیلترهای ایمنی فعال</strong>

                  <span>
                    بارش شدید، کیفیت هوای نامناسب، گرما و
                    باد شدید در تصمیم نهایی لحاظ می‌شوند.
                  </span>
                </div>

                <CheckCircle2 size={17} />
              </div>

              <button
                className="primary full analyzeButton"
                onClick={analyze}
                disabled={loading}
              >
                {loading ? (
                  <>
                    <RefreshCcw className="spin" size={18} />
                    در حال تحلیل شرایط…
                  </>
                ) : (
                  <>
                    تحلیل بهترین زمان
                    <ChevronLeft size={18} />
                  </>
                )}
              </button>

              {error && (
                <div className="errorBox" role="alert">
                  <TriangleAlert size={16} />
                  <span>{error}</span>
                </div>
              )}
            </div>

            <div className="plannerFooter">
              <Info size={14} />

              <span>
                HAVAZ یک تصمیم‌یار است؛ هشدارهای رسمی
                هواشناسی همیشه اولویت دارند.
              </span>
            </div>
          </div>

          <div className="panel insightPanel insightModern">
            <div className="panelHeaderModern">
              <div className="panelNumber panelNumberTeal">
                02
              </div>

              <div>
                <span className="panelKicker">
                  LIVE ENVIRONMENT
                </span>

                <h3>شرایط محیطی</h3>
              </div>

              {result && (
                <span className="liveBadge">
                  <span />
                  LIVE
                </span>
              )}
            </div>

            {result ? (
              <>
                <div className="environmentHero">
                  <div className="environmentTemperature">
                    <Sun size={28} />

                    <div>
                      <strong>
                        {result.environment.temperature != null
                          ? faNum(
                              Math.round(
                                result.environment.temperature
                              )
                            )
                          : '—'}
                        °
                      </strong>

                      <span>
                        دمای هوای محیط
                      </span>
                    </div>
                  </div>

                  <div className="environmentStatus">
                    <span>وضعیت تحلیل</span>

                    <strong>
                      {result.analysis.riskScore <= 30
                        ? 'شرایط مناسب'
                        : result.analysis.riskScore <= 60
                          ? 'نیازمند توجه'
                          : 'ریسک بالا'}
                    </strong>

                    <small>
                      {result.analysis.decisionMode}
                    </small>
                  </div>
                </div>

                <div className="environmentGrid">
                  <div>
                    <Wind size={16} />
                    <span>باد</span>
                    <strong>
                      {result.environment.windSpeed != null
                        ? `${faNum(
                            Math.round(
                              result.environment.windSpeed
                            )
                          )} km/h`
                        : '—'}
                    </strong>
                  </div>

                  <div>
                    <CloudSun size={16} />
                    <span>رطوبت</span>
                    <strong>
                      {result.environment.humidity != null
                        ? `${faNum(
                            Math.round(
                              result.environment.humidity
                            )
                          )}٪`
                        : '—'}
                    </strong>
                  </div>

                  <div>
                    <Activity size={16} />
                    <span>AQI</span>
                    <strong>
                      {result.environment.aqi != null
                        ? faNum(
                            Math.round(
                              result.environment.aqi
                            )
                          )
                        : '—'}
                    </strong>
                  </div>

                  <div>
                    <CloudSun size={16} />
                    <span>بارش</span>
                    <strong>
                      {result.environment.rainProbability !=
                      null
                        ? `${faNum(
                            Math.round(
                              result.environment
                                .rainProbability
                            )
                          )}٪`
                        : '—'}
                    </strong>
                  </div>
                </div>

                <div className="chartCard">
                  <div className="chartHeader">
                    <div>
                      <strong>پروفایل ریسک ساعتی</strong>
                      <span>
                        روی هر نقطه کلیک کن تا جزئیات آن ساعت
                        نمایش داده شود.
                      </span>
                    </div>

                    <span className="chartCount">
                      {faNum(chart.length)} نقطه
                    </span>
                  </div>

                  <ResponsiveContainer
                    width="100%"
                    height={190}
                  >
                    <LineChart
                      data={chart}
                      margin={{
                        top: 12,
                        right: 8,
                        left: -25,
                        bottom: 0,
                      }}
                      onClick={(state) => {
                        const point =
                          state?.activePayload?.[0]
                            ?.payload;

                        if (point) {
                          setSelected(
                            point as HourlyPoint
                          );
                        }
                      }}
                    >
                      <XAxis
                        dataKey="time"
                        tick={{
                          fontSize: 10,
                        }}
                        tickLine={false}
                        axisLine={false}
                      />

                      <YAxis
                        hide
                        domain={[0, 100]}
                      />

                      <Tooltip
                        formatter={(value) => [
                          `${value}`,
                          'ریسک',
                        ]}
                      />

                      <Line
                        type="monotone"
                        dataKey="riskScore"
                        stroke="#0f766e"
                        strokeWidth={3}
                        dot={{
                          r: 3,
                          fill: '#0f766e',
                        }}
                        activeDot={{
                          r: 6,
                        }}
                      />
                    </LineChart>
                  </ResponsiveContainer>
                </div>

                {selected && (
                  <div className="selectedHour">
                    <div className="selectedHourMain">
                      <span>ساعت انتخاب‌شده</span>
                      <strong>
                        {faNum(selected.time)}
                      </strong>
                    </div>

                    <div
                      className={
                        selected.isSafe
                          ? 'selectedSafe'
                          : 'selectedUnsafe'
                      }
                    >
                      {selected.isSafe ? (
                        <CheckCircle2 size={16} />
                      ) : (
                        <TriangleAlert size={16} />
                      )}

                      {selected.isSafe
                        ? 'قابل قبول'
                        : 'دارای محدودیت ایمنی'}
                    </div>

                    <div className="selectedRisk">
                      <span>ریسک</span>
                      <strong>
                        {faNum(selected.riskScore)}
                      </strong>
                    </div>
                  </div>
                )}
              </>
            ) : (
              <div className="environmentEmpty">
                <div className="emptyVisual">
                  <CloudSun size={32} />
                </div>

                <strong>
                  هنوز تحلیلی اجرا نشده است
                </strong>

                <span>
                  مشخصات فعالیت را وارد کن تا HAVAZ شرایط
                  واقعی محیط را برایت تحلیل کند.
                </span>

                <div className="emptySteps">
                  <span>
                    <b>۱</b>
                    فعالیت
                  </span>

                  <span>
                    <b>۲</b>
                    شدت و مدت
                  </span>

                  <span>
                    <b>۳</b>
                    تحلیل
                  </span>
                </div>
              </div>
            )}
          </div>
        </section>

        {loading && (
          <div className="loading analysisLoading">
            <RefreshCcw className="spin" size={17} />
            <span>
              دریافت داده‌های آب‌وهوا و کیفیت هوا و اجرای مدل
              محیطی…
            </span>
          </div>
        )}

        {result && (
          <section className="results resultsModern">
            <div className="sectionHead sectionHeadModern">
              <div>
                <div className="eyebrow">
                  <ShieldCheck size={14} />
                  نتیجه تحلیل
                </div>

                <h2>تصمیم پیشنهادی HAVAZ</h2>

                <p>
                  سه استراتژی متفاوت بر اساس شرایط واقعی
                  محیطی محاسبه شده‌اند.
                </p>
              </div>

              <div className="analysisMeta">
                <span>
                  <span className="metaDot" />
                  {result.analysis.decisionMode}
                </span>

                <span>
                  ID: {result.requestId.slice(0, 8)}
                </span>
              </div>
            </div>

            <div className="resultGrid resultGridModern">
              <div className="riskCard riskCardModern">
                <div className="riskHeader">
                  <span>شاخص ریسک کلی</span>
                  <Gauge size={17} />
                </div>

                <div className="riskRing riskRingModern">
                  <div>
                    <strong>
                      {faNum(
                        result.analysis.riskScore
                      )}
                    </strong>

                    <small>/۱۰۰</small>
                  </div>
                </div>

                <div
                  className={
                    result.analysis.riskScore > 60
                      ? 'riskStatus bad'
                      : result.analysis.riskScore > 40
                        ? 'riskStatus mid'
                        : 'riskStatus good'
                  }
                >
                  {levelFa(
                    result.analysis.riskLevel
                  )}
                </div>

                <div className="riskExplanation">
                  <span>روش تصمیم‌گیری</span>
                  <strong>
                    {result.analysis.mlAvailable
                      ? 'HYBRID'
                      : 'RULE-BASED'}
                  </strong>
                </div>

                <div className="riskBar">
                  <i
                    style={{
                      width: `${result.analysis.riskScore}%`,
                    }}
                  />
                </div>

                {result.prediction.mlAvailable && (
                  <div className="mlCard">
                    <div className="mlIcon">
                      <BrainCircuit size={16} />
                    </div>

                    <div>
                      <span>مدل محیطی</span>

                      <strong>
                        {result.prediction.modelVersion ??
                          '—'}
                      </strong>
                    </div>

                    <small>
                      {result.prediction.confidence !=
                      null
                        ? `${Math.round(
                            result.prediction
                              .confidence * 100
                          )}%`
                        : '—'}
                    </small>
                  </div>
                )}

                {!result.analysis.mlAvailable && (
                  <div className="fallback">
                    <Info size={14} />
                    <span>
                      مدل یادگیری ماشین در دسترس نبود؛
                      نتیجه با موتور قوانین محاسبه شد.
                    </span>
                  </div>
                )}

                {!result.dataQuality.airQualityAvailable && (
                  <div className="fallback">
                    <Info size={14} />
                    <span>
                      داده کیفیت هوا برای بخشی از بازه
                      موجود نبود.
                    </span>
                  </div>
                )}
              </div>

              <div className="plans plansModern">
                <div className="plansHeader">
                  <div>
                    <strong>بازه‌های پیشنهادی</strong>
                    <span>
                      بر اساس ریسک، ترجیحات و محدودیت‌های
                      ایمنی
                    </span>
                  </div>

                  <Sparkles size={17} />
                </div>

                {result.safety.noSafeSlot ? (
                  <div className="emptyState noSafeSlot">
                    <div className="emptyStateIcon">
                      <TriangleAlert size={25} />
                    </div>

                    <h3>زمان امنی پیدا نشد</h3>

                    <p>
                      در بازه انتخاب‌شده، هیچ پنجره‌ای
                      تمام محدودیت‌های ایمنی را پاس نکرد.
                    </p>

                    {result.safety.reasons.map(
                      (reason) => (
                        <small key={reason}>
                          • {reason}
                        </small>
                      )
                    )}
                  </div>
                ) : (
                  result.recommendations.map(
                    (plan, index) => (
                      <div
                        className={
                          index === 0
                            ? 'plan planModern best'
                            : 'plan planModern'
                        }
                        key={plan.type}
                      >
                        <div className="planTop">
                          <span className="planRank">
                            {index === 0
                              ? '01'
                              : index === 1
                                ? '02'
                                : '03'}
                          </span>

                          <div className="planTitle">
                            <span>
                              {plan.type === 'SAFE'
                                ? 'امن‌ترین انتخاب'
                                : plan.type ===
                                    'BALANCED'
                                  ? 'انتخاب متعادل'
                                  : 'هماهنگ با ترجیحات'}
                            </span>

                            <strong>
                              {plan.start} — {plan.end}
                            </strong>
                          </div>

                          <div className="planScore">
                            <strong>
                              {faNum(plan.score)}
                            </strong>
                            <small>/۱۰۰</small>
                          </div>
                        </div>

                        <div className="planReason">
                          <Clock3 size={15} />
                          <span>{plan.reason}</span>
                        </div>

                        <div className="planMeta">
                          <span>
                            <Sun size={13} />
                            {plan.temperature != null
                              ? `${faNum(
                                  Math.round(
                                    plan.temperature
                                  )
                                )}°`
                              : '—'}
                          </span>

                          <span>
                            <CloudSun size={13} />
                            {plan.humidity != null
                              ? `${faNum(
                                  Math.round(
                                    plan.humidity
                                  )
                                )}٪`
                              : '—'}
                          </span>

                          <span>
                            <Wind size={13} />
                            {plan.windSpeed != null
                              ? `${faNum(
                                  Math.round(
                                    plan.windSpeed
                                  )
                                )} km/h`
                              : '—'}
                          </span>

                          <span>
                            AQI{' '}
                            {plan.aqi != null
                              ? faNum(
                                  Math.round(
                                    plan.aqi
                                  )
                                )
                              : '—'}
                          </span>
                        </div>
                      </div>
                    )
                  )
                )}
              </div>
            </div>

            {result.factors.length > 0 && (
              <div className="factorSection">
                <div className="factorSectionHeader">
                  <div>
                    <strong>عوامل مؤثر بر تصمیم</strong>
                    <span>
                      چرا HAVAZ این نتیجه را انتخاب کرده؟
                    </span>
                  </div>

                  <ShieldCheck size={17} />
                </div>

                <div className="factorGrid factorGridModern">
                  {result.factors
                    .slice(0, 8)
                    .map((factor) => (
                      <div
                        className="factor factorModern"
                        key={factor.name}
                      >
                        <div className="factorTop">
                          <span>
                            {factor.name}
                          </span>

                          <small>
                            {factor.source}
                          </small>
                        </div>

                        <strong>
                          {faNum(
                            Math.round(
                              factor.score
                            )
                          )}
                        </strong>

                        <div className="factorLine">
                          <i
                            style={{
                              width: `${Math.min(
                                100,
                                Math.max(
                                  0,
                                  factor.score
                                )
                              )}%`,
                            }}
                          />
                        </div>

                        <p>
                          {factor.explanation}
                        </p>
                      </div>
                    ))}
                </div>
              </div>
            )}
          </section>
        )}
      </main>

      <footer className="modernFooter">
        <div className="footerBrand">
          <span className="brandmark">
            <CloudSun size={17} />
          </span>

          <strong>HAVAZ</strong>
        </div>

        <span>
          Environmental Decision Intelligence
        </span>

        <span className="footerRight">
          Weather & Air Quality · Open-Meteo
        </span>
      </footer>
    </div>
  );
}

createRoot(document.getElementById('root')!).render(
  <QueryClientProvider client={queryClient}>
    <App />
  </QueryClientProvider>
);