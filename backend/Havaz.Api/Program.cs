using System.Security.Claims;
using System.Text.Json;
using System.Threading.RateLimiting;
using Havaz.Application;
using Havaz.Domain;
using Havaz.Infrastructure;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

const string CsrfCookie = "HAVAZ.CSRF";
const string CsrfHeader = "X-CSRF-TOKEN";
var builder = WebApplication.CreateBuilder(args);
var isDev = builder.Environment.IsDevelopment();
var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? (isDev ? new[] { "http://localhost:5173", "http://127.0.0.1:5173" } : Array.Empty<string>());

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(origins).AllowAnyHeader().WithMethods("GET", "POST", "DELETE").AllowCredentials()));
builder.Services.AddDbContext<HavazDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
{
    o.Cookie.Name = "HAVAZ.AUTH"; o.Cookie.HttpOnly = true; o.Cookie.SameSite = SameSiteMode.Lax;
    o.Cookie.SecurePolicy = isDev ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    o.ExpireTimeSpan = TimeSpan.FromHours(12); o.SlidingExpiration = true;
    o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
});
builder.Services.AddAuthorization();
builder.Services.AddAntiforgery(o => { o.HeaderName = CsrfHeader; o.Cookie.Name = "HAVAZ.AFT"; o.Cookie.HttpOnly = true; });
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy("auth", c => RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString() ?? "anon", _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});
builder.Services.AddTransient<RetryHandler>();
builder.Services.AddHttpClient<IWeatherProvider, OpenMeteoWeatherProvider>(c => { c.BaseAddress = new Uri(builder.Configuration["Providers:WeatherBaseUrl"] ?? "https://api.open-meteo.com/"); c.Timeout = TimeSpan.FromSeconds(10); }).AddHttpMessageHandler<RetryHandler>();
builder.Services.AddHttpClient<IAirQualityProvider, OpenMeteoAirQualityProvider>(c => { c.BaseAddress = new Uri(builder.Configuration["Providers:AirQualityBaseUrl"] ?? "https://air-quality-api.open-meteo.com/"); c.Timeout = TimeSpan.FromSeconds(10); }).AddHttpMessageHandler<RetryHandler>();
builder.Services.AddHttpClient<IMlPredictionProvider, PythonMlPredictionProvider>(c => { c.BaseAddress = new Uri(builder.Configuration["Ml:BaseUrl"] ?? "http://localhost:8001/"); c.Timeout = TimeSpan.FromSeconds(5); });
builder.Services.AddScoped<IAnalysisService, AnalysisService>();
builder.Services.AddScoped<IAuthService, AuthService>();

var app = builder.Build();
app.UseRateLimiter();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
    await next();
});
// CSRF: double-submit cookie. Cookie-based auth + SameSite=Lax + explicit CORS origins;
// every authenticated state-changing request must echo the HAVAZ.CSRF cookie value in X-CSRF-TOKEN.
app.Use(async (ctx, next) =>
{
    var stateChanging = HttpMethods.IsPost(ctx.Request.Method) || HttpMethods.IsPut(ctx.Request.Method) || HttpMethods.IsPatch(ctx.Request.Method) || HttpMethods.IsDelete(ctx.Request.Method);
    if (ctx.User.Identity?.IsAuthenticated == true && stateChanging
        && !ctx.Request.Path.StartsWithSegments("/api/auth/logout") && !ctx.Request.Path.StartsWithSegments("/api/auth/login") && !ctx.Request.Path.StartsWithSegments("/api/auth/register"))
    {
        var header = ctx.Request.Headers[CsrfHeader].ToString();
        var cookie = ctx.Request.Cookies[CsrfCookie];
        if (string.IsNullOrEmpty(header) || string.IsNullOrEmpty(cookie) || header.Length < 16 || header != cookie)
        {
            ctx.Response.StatusCode = 403;
            await ctx.Response.WriteAsJsonAsync(new ApiEnvelope<object>(false, null, new ApiError("CSRF_VALIDATION_FAILED", "درخواست نامعتبر است."), ctx.TraceIdentifier));
            return;
        }
    }
    await next();
});

static Guid? UserId(HttpContext ctx) => Guid.TryParse(ctx.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
static IResult Unauthenticated(HttpContext ctx) => Results.Json(new ApiEnvelope<object>(false, null, new ApiError("UNAUTHENTICATED", "برای مشاهده این بخش باید وارد حساب کاربری شوید."), ctx.TraceIdentifier), statusCode: 401);
static void IssueCsrf(HttpContext ctx)
{
    var token = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
    ctx.Response.Cookies.Append(CsrfCookie, token, new CookieOptions { HttpOnly = false, SameSite = SameSiteMode.Lax, Secure = ctx.Request.IsHttps, Path = "/" });
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

if (isDev)
{
    app.MapGet("/api/diagnostics", async (
        HavazDbContext db,
        IWeatherProvider weather,
        IAirQualityProvider airQuality,
        IMlPredictionProvider ml,
        CancellationToken ct) =>
    {
        var database = false;
        try
        {
            database = await db.Database.CanConnectAsync(ct);
        }
        catch
        {
        }

        var weatherProvider = false;
        try
        {
            var result = await weather.GetWeather(29.5918, 52.5837, ct);
            weatherProvider = result is not null;
        }
        catch
        {
        }

        var airQualityProvider = false;
        try
        {
            var result = await airQuality.GetAirQuality(29.5918, 52.5837, ct);
            airQualityProvider = result is not null;
        }
        catch
        {
        }

        var mlService = false;
        try
        {
            var prediction = await ml.Predict(
                new MlFeatureVector(
                    25.0,
                    24.0,
                    40.0,
                    0.0,
                    0.0,
                    10.0,
                    10.0,
                    20.0,
                    30.0,
                    50.0,
                    10.0,
                    12.0,
                    9.0
                ),
                ct
            );

            mlService = prediction is not null;
        }
        catch
        {
        }

        return Results.Ok(new
        {
            aspNetApi = true,
            weatherProvider,
            airQualityProvider,
            mlService,
            database
        });
    });
}

app.MapPost("/api/auth/register", async (RegisterRequest req, IAuthService auth, HttpContext ctx) =>
{
    var user = await auth.Register(req);
    if (user is null) return Results.BadRequest(new ApiEnvelope<object>(false, null, new ApiError("REGISTRATION_FAILED", "ثبت‌نام انجام نشد. اطلاعات را بررسی کنید."), ctx.TraceIdentifier));
    return SignIn(user, ctx);
}).RequireRateLimiting("auth");

app.MapPost("/api/auth/login", async (LoginRequest req, IAuthService auth, HttpContext ctx) =>
{
    var user = await auth.Login(req.Email, req.Password);
    if (user is null) return Results.Json(new ApiEnvelope<object>(false, null, new ApiError("INVALID_CREDENTIALS", "ایمیل یا رمز عبور نادرست است."), ctx.TraceIdentifier), statusCode: 401);
    return SignIn(user, ctx);
}).RequireRateLimiting("auth");

IResult SignIn(UserDto user, HttpContext ctx)
{
    var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Email, user.Email), new Claim(ClaimTypes.Role, user.Role) }, CookieAuthenticationDefaults.AuthenticationScheme);
    return SignInAsync(user, identity, ctx).Result;
}
async Task<IResult> SignInAsync(UserDto user, ClaimsIdentity identity, HttpContext ctx)
{
    await ctx.SignInAsync(new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(12) });
    IssueCsrf(ctx);
    return Results.Ok(new ApiEnvelope<UserDto>(true, user, null, ctx.TraceIdentifier));
}

app.MapPost("/api/auth/logout", async (HttpContext ctx) => { await ctx.SignOutAsync(); return Results.Ok(new ApiEnvelope<object>(true, new { }, null, ctx.TraceIdentifier)); });

app.MapGet("/api/auth/me", async (IAuthService auth, HttpContext ctx) =>
{
    var id = UserId(ctx); if (id is null) return Unauthenticated(ctx);
    var user = await auth.GetById(id.Value); if (user is null) return Unauthenticated(ctx);
    IssueCsrf(ctx);
    return Results.Ok(new ApiEnvelope<UserDto>(true, user, null, ctx.TraceIdentifier));
});

app.MapPost("/api/analysis", async (AnalysisRequest request, IAnalysisService service, HttpContext ctx, CancellationToken ct) =>
{
    var uid = UserId(ctx); if (uid is null) return Unauthenticated(ctx);
    var err = Validate(request);
    if (err is not null) return Results.BadRequest(new ApiEnvelope<object>(false, null, new ApiError("INVALID_ANALYSIS_REQUEST", err), ctx.TraceIdentifier));
    try { var data = await service.Analyze(request, uid.Value, ct); return Results.Ok(new ApiEnvelope<AnalysisDataDto>(true, data, null, data.RequestId)); }
    catch (InvalidOperationException ex) when (ex.Message == "NO_ENVIRONMENTAL_DATA") { return Results.Json(new ApiEnvelope<object>(false, null, new ApiError("ENVIRONMENTAL_DATA_UNAVAILABLE", "برای تاریخ انتخاب‌شده داده محیطی در دسترس نیست."), ctx.TraceIdentifier), statusCode: 422); }
    catch (HttpRequestException) { return Results.Json(new ApiEnvelope<object>(false, null, new ApiError("WEATHER_PROVIDER_UNAVAILABLE", "اطلاعات آب‌وهوا در دسترس نیست."), ctx.TraceIdentifier), statusCode: 503); }
    catch (TaskCanceledException) when (!ct.IsCancellationRequested) { return Results.Json(new ApiEnvelope<object>(false, null, new ApiError("WEATHER_PROVIDER_TIMEOUT", "پاسخ سرویس آب‌وهوا به‌موقع دریافت نشد."), ctx.TraceIdentifier), statusCode: 504); }
    catch (Exception) { return Results.Json(new ApiEnvelope<object>(false, null, new ApiError("ANALYSIS_FAILED", "در حال حاضر تحلیل انجام نشد. لطفاً دوباره تلاش کنید."), ctx.TraceIdentifier), statusCode: 500); }
});

app.MapGet("/api/analysis/history", async (HavazDbContext db, HttpContext ctx, int page = 1, int pageSize = 20) =>
{
    var uid = UserId(ctx); if (uid is null) return Unauthenticated(ctx);
    page = Math.Max(page, 1); pageSize = Math.Clamp(pageSize, 1, 50);
    var query = db.PlanningRequests.Where(r => r.UserId == uid.Value).OrderByDescending(r => r.CreatedAt);
    var total = await query.CountAsync();
    var items = await query.Skip((page - 1) * pageSize).Take(pageSize)
        .Select(r => new HistoryItemDto(r.RequestId, r.Activity, r.Intensity, r.DurationMinutes, r.Date.ToString("yyyy-MM-dd"), r.RiskScore, r.RiskLevel, r.DecisionMode, r.MlAvailable, r.ModelVersion, r.CreatedAt)).ToListAsync();
    return Results.Ok(new ApiEnvelope<HistoryPageDto>(true, new(items, total, page, pageSize), null, ctx.TraceIdentifier));
});

app.MapGet("/api/analysis/{requestId}", async (string requestId, HavazDbContext db, HttpContext ctx) =>
{
    var uid = UserId(ctx); if (uid is null) return Unauthenticated(ctx);
    var r = await db.PlanningRequests.SingleOrDefaultAsync(x => x.RequestId == requestId && x.UserId == uid.Value);
    if (r is null) return Results.Json(new ApiEnvelope<object>(false, null, new ApiError("NOT_FOUND", "موردی یافت نشد."), ctx.TraceIdentifier), statusCode: 404);
    return Results.Ok(new ApiEnvelope<object>(true, new
    {
        r.RequestId, r.Activity, r.Intensity, r.DurationMinutes, Date = r.Date.ToString("yyyy-MM-dd"), r.RiskScore, r.RiskLevel, r.DecisionMode, r.MlAvailable, r.ModelVersion, r.FeatureSchemaVersion, r.RiskProbability, r.Confidence, r.CreatedAt,
        environment = JsonSerializer.Deserialize<JsonElement>(r.EnvironmentJson), factors = JsonSerializer.Deserialize<JsonElement>(r.FactorsJson), recommendations = JsonSerializer.Deserialize<JsonElement>(r.RecommendationsJson), safety = JsonSerializer.Deserialize<JsonElement>(r.SafetyJson)
    }, null, ctx.TraceIdentifier));
});

app.MapPost("/api/plans", async (SavePlanRequest req, HavazDbContext db, HttpContext ctx) =>
{
    var uid = UserId(ctx); if (uid is null) return Unauthenticated(ctx);
    if (string.IsNullOrWhiteSpace(req.Activity) || req.Latitude is < -90 or > 90 || req.Longitude is < -180 or > 180 || req.RiskScore is < 0 or > 100)
        return Results.BadRequest(new ApiEnvelope<object>(false, null, new ApiError("INVALID_PLAN", "اطلاعات برنامه معتبر نیست."), ctx.TraceIdentifier));
    var plan = new SavedPlan { UserId = uid.Value, Activity = req.Activity.Trim(), Latitude = req.Latitude, Longitude = req.Longitude, SelectedStart = req.SelectedStart, SelectedEnd = req.SelectedEnd, RiskScore = req.RiskScore, EnvironmentalSummaryJson = JsonSerializer.Serialize(req.EnvironmentalSummary ?? new { }) };
    db.SavedPlans.Add(plan); await db.SaveChangesAsync();
    return Results.Ok(new ApiEnvelope<SavedPlanDto>(true, ToPlanDto(plan), null, ctx.TraceIdentifier));
});

app.MapGet("/api/plans", async (HavazDbContext db, HttpContext ctx, int page = 1, int pageSize = 20) =>
{
    var uid = UserId(ctx); if (uid is null) return Unauthenticated(ctx);
    pageSize = Math.Clamp(pageSize, 1, 50);
    var items = await db.SavedPlans.Where(p => p.UserId == uid.Value).OrderByDescending(p => p.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
    return Results.Ok(new ApiEnvelope<object>(true, items.Select(ToPlanDto).ToList(), null, ctx.TraceIdentifier));
});

app.MapGet("/api/plans/{id:guid}", async (Guid id, HavazDbContext db, HttpContext ctx) =>
{
    var uid = UserId(ctx); if (uid is null) return Unauthenticated(ctx);
    var plan = await db.SavedPlans.SingleOrDefaultAsync(p => p.Id == id && p.UserId == uid.Value);
    if (plan is null) return Results.Json(new ApiEnvelope<object>(false, null, new ApiError("NOT_FOUND", "موردی یافت نشد."), ctx.TraceIdentifier), statusCode: 404);
    return Results.Ok(new ApiEnvelope<SavedPlanDto>(true, ToPlanDto(plan), null, ctx.TraceIdentifier));
});

app.MapDelete("/api/plans/{id:guid}", async (Guid id, HavazDbContext db, HttpContext ctx) =>
{
    var uid = UserId(ctx); if (uid is null) return Unauthenticated(ctx);
    var plan = await db.SavedPlans.SingleOrDefaultAsync(p => p.Id == id && p.UserId == uid.Value);
    if (plan is null) return Results.Json(new ApiEnvelope<object>(false, null, new ApiError("NOT_FOUND", "موردی یافت نشد."), ctx.TraceIdentifier), statusCode: 404);
    db.SavedPlans.Remove(plan); await db.SaveChangesAsync();
    return Results.Ok(new ApiEnvelope<object>(true, new { deleted = true }, null, ctx.TraceIdentifier));
});

app.Run();

static SavedPlanDto ToPlanDto(SavedPlan p) => new(p.Id, p.Activity, p.Latitude, p.Longitude, p.SelectedStart, p.SelectedEnd, p.RiskScore, p.EnvironmentalSummaryJson, p.CreatedAt);

static string? Validate(AnalysisRequest request)
{
    if (request.Latitude is < -90 or > 90 || request.Longitude is < -180 or > 180) return "مختصات جغرافیایی معتبر نیست.";
    if (!ActivityProfile.Profiles.ContainsKey(request.Activity)) return "نوع فعالیت معتبر نیست.";
    if (!new[] { "low", "moderate", "high", "very-high", "کم", "متوسط", "زیاد", "خیلی زیاد" }.Contains(request.Intensity, StringComparer.OrdinalIgnoreCase)) return "شدت فعالیت معتبر نیست.";
    if (request.DurationMinutes is < 15 or > 720) return "مدت فعالیت باید بین ۱۵ تا ۷۲۰ دقیقه باشد.";
    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    if (request.Date < today || request.Date > today.AddDays(6)) return "تاریخ انتخاب‌شده خارج از بازه پیش‌بینی است.";
    if (request.PreferredTimeRange is not null && (!TimeOnly.TryParseExact(request.PreferredTimeRange.Start, "HH:mm", out var s) || !TimeOnly.TryParseExact(request.PreferredTimeRange.End, "HH:mm", out var e) || s >= e)) return "بازه زمانی ترجیحی معتبر نیست.";
    return null;
}
