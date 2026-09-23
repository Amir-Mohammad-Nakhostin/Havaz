using Microsoft.EntityFrameworkCore;
namespace Havaz.Infrastructure;

public sealed class HavazDbContext(DbContextOptions<HavazDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<PlanningRequest> PlanningRequests => Set<PlanningRequest>();
    public DbSet<SavedPlan> SavedPlans => Set<SavedPlan>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<User>().HasKey(x => x.Id);
        builder.Entity<User>().HasIndex(x => x.NormalizedEmail).IsUnique();
        builder.Entity<PlanningRequest>().HasKey(x => x.Id);
        builder.Entity<PlanningRequest>().HasIndex(x => x.CreatedAt);
        builder.Entity<PlanningRequest>().HasIndex(x => x.UserId);
        builder.Entity<PlanningRequest>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<PlanningRequest>().Property(x => x.EnvironmentJson).HasColumnType("jsonb");
        builder.Entity<PlanningRequest>().Property(x => x.FactorsJson).HasColumnType("jsonb");
        builder.Entity<PlanningRequest>().Property(x => x.RecommendationsJson).HasColumnType("jsonb");
        builder.Entity<PlanningRequest>().Property(x => x.SafetyJson).HasColumnType("jsonb");
        builder.Entity<SavedPlan>().HasKey(x => x.Id);
        builder.Entity<SavedPlan>().HasIndex(x => x.CreatedAt);
        builder.Entity<SavedPlan>().HasIndex(x => x.UserId);
        builder.Entity<SavedPlan>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DisplayName { get; set; } = "";
    public string Email { get; set; } = "";
    public string NormalizedEmail { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "USER";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class PlanningRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public string RequestId { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Activity { get; set; } = "";
    public string Intensity { get; set; } = "";
    public int DurationMinutes { get; set; }
    public DateOnly Date { get; set; }
    public string? PreferredStart { get; set; }
    public string? PreferredEnd { get; set; }
    public int RiskScore { get; set; }
    public string RiskLevel { get; set; } = "";
    public string DecisionMode { get; set; } = "";
    public bool MlAvailable { get; set; }
    public string? ModelVersion { get; set; }
    public string? FeatureSchemaVersion { get; set; }
    public double? RiskProbability { get; set; }
    public double? Confidence { get; set; }
    public string EnvironmentJson { get; set; } = "{}";
    public string FactorsJson { get; set; } = "[]";
    public string RecommendationsJson { get; set; } = "[]";
    public string SafetyJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class SavedPlan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public string Activity { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string SelectedStart { get; set; } = "";
    public string SelectedEnd { get; set; } = "";
    public int RiskScore { get; set; }
    public string EnvironmentalSummaryJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
