using Havaz.Application;
using Havaz.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Havaz.Backend.Tests;

public sealed class OwnershipTests
{
    private static HavazDbContext Db() => new(new DbContextOptionsBuilder<HavazDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task OwnershipFilteredQueriesPreventIdor()
    {
        await using var db = Db();
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        db.Users.AddRange(new User { Id = a, Email = "a@x.com", NormalizedEmail = "A@X.COM", DisplayName = "A", PasswordHash = "h" }, new User { Id = b, Email = "b@x.com", NormalizedEmail = "B@X.COM", DisplayName = "B", PasswordHash = "h" });
        var analysisA = new PlanningRequest { UserId = a, RequestId = "req-a", Activity = "running" };
        var planA = new SavedPlan { UserId = a, Activity = "running", SelectedStart = "08:00", SelectedEnd = "09:00", RiskScore = 20 };
        db.PlanningRequests.Add(analysisA); db.SavedPlans.Add(planA); await db.SaveChangesAsync();

        // User A reads own resources
        Assert.NotNull(await db.PlanningRequests.SingleOrDefaultAsync(r => r.RequestId == "req-a" && r.UserId == a));
        Assert.NotNull(await db.SavedPlans.SingleOrDefaultAsync(p => p.Id == planA.Id && p.UserId == a));
        // User B cannot (IDOR attempt)
        Assert.Null(await db.PlanningRequests.SingleOrDefaultAsync(r => r.RequestId == "req-a" && r.UserId == b));
        Assert.Null(await db.SavedPlans.SingleOrDefaultAsync(p => p.Id == planA.Id && p.UserId == b));
        // History query returns only own rows
        Assert.Single(await db.PlanningRequests.Where(r => r.UserId == a).ToListAsync());
        Assert.Empty(await db.PlanningRequests.Where(r => r.UserId == b).ToListAsync());
    }

    [Fact]
    public async Task AnalysisServicePersistsOwnerServerSide()
    {
        await using var db = Db();
        var uid = Guid.NewGuid();
        db.Users.Add(new User { Id = uid, Email = "o@x.com", NormalizedEmail = "O@X.COM", DisplayName = "O", PasswordHash = "h" });
        await db.SaveChangesAsync();
        var service = new AnalysisService(new FakeWeather(), new FakeAir(), new FakeMl(), db, Microsoft.Extensions.Logging.Abstractions.NullLogger<AnalysisService>.Instance);
        await service.Analyze(new AnalysisRequest(29.5918, 52.5837, "running", "high", 90, new DateOnly(2026, 9, 5), null, null), uid, CancellationToken.None);
        var persisted = await db.PlanningRequests.SingleAsync();
        Assert.Equal(uid, persisted.UserId);
    }

    private sealed class FakeWeather : IWeatherProvider
    {
        public Task<Havaz.Domain.EnvironmentalSeries> GetWeather(double la, double lo, CancellationToken ct)
            => Task.FromResult(new Havaz.Domain.EnvironmentalSeries("Asia/Tehran", Enumerable.Range(6, 15).Select(h => new Havaz.Domain.EnvironmentalObservation(new DateTime(2026, 9, 5, h, 0, 0), la, lo, 22, 22, 40, 0, 0, 0, 0, 6, 180, 0, null, null, null, null, null)).ToList()));
    }
    private sealed class FakeAir : IAirQualityProvider
    {
        public Task<Havaz.Domain.AirQualitySeries> GetAirQuality(double la, double lo, CancellationToken ct)
            => Task.FromResult(new Havaz.Domain.AirQualitySeries("Asia/Tehran", Enumerable.Range(6, 15).Select(h => new Havaz.Domain.AirQualityPoint(new DateTime(2026, 9, 5, h, 0, 0), 20, 5, 10, 20, 10)).ToList()));
    }
    private sealed class FakeMl : IMlPredictionProvider
    {
        public Task<Havaz.Domain.MlPrediction?> Predict(Havaz.Domain.MlFeatureVector f, CancellationToken ct) => Task.FromResult<Havaz.Domain.MlPrediction?>(new("1.2.0", "environmental-v1", .1, "low", .9));
    }
}
