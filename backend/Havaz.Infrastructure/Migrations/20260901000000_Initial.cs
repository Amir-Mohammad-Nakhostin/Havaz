using System;
using Havaz.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace Havaz.Infrastructure.Migrations;

[DbContext(typeof(HavazDbContext))]
[Migration("20260901000000_Initial")]
public partial class Initial : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "PlanningRequests", columns: table => new
        {
            Id = table.Column<Guid>(type: "uuid", nullable: false),
            RequestId = table.Column<string>(type: "text", nullable: false),
            Latitude = table.Column<double>(type: "double precision", nullable: false),
            Longitude = table.Column<double>(type: "double precision", nullable: false),
            Activity = table.Column<string>(type: "text", nullable: false),
            Intensity = table.Column<string>(type: "text", nullable: false),
            DurationMinutes = table.Column<int>(type: "integer", nullable: false),
            Date = table.Column<DateOnly>(type: "date", nullable: false),
            PreferredStart = table.Column<string>(type: "text", nullable: true),
            PreferredEnd = table.Column<string>(type: "text", nullable: true),
            RiskScore = table.Column<int>(type: "integer", nullable: false),
            RiskLevel = table.Column<string>(type: "text", nullable: false),
            DecisionMode = table.Column<string>(type: "text", nullable: false),
            MlAvailable = table.Column<bool>(type: "boolean", nullable: false),
            ModelVersion = table.Column<string>(type: "text", nullable: true),
            FeatureSchemaVersion = table.Column<string>(type: "text", nullable: true),
            RiskProbability = table.Column<double>(type: "double precision", nullable: true),
            Confidence = table.Column<double>(type: "double precision", nullable: true),
            EnvironmentJson = table.Column<string>(type: "jsonb", nullable: false),
            FactorsJson = table.Column<string>(type: "jsonb", nullable: false),
            RecommendationsJson = table.Column<string>(type: "jsonb", nullable: false),
            SafetyJson = table.Column<string>(type: "jsonb", nullable: false),
            CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
        }, constraints: table => table.PrimaryKey("PK_PlanningRequests", x => x.Id));
        migrationBuilder.CreateIndex(name: "IX_PlanningRequests_CreatedAt", table: "PlanningRequests", column: "CreatedAt");
        migrationBuilder.CreateTable(name: "SavedPlans", columns: table => new
        {
            Id = table.Column<Guid>(type: "uuid", nullable: false),
            Activity = table.Column<string>(type: "text", nullable: false),
            Latitude = table.Column<double>(type: "double precision", nullable: false),
            Longitude = table.Column<double>(type: "double precision", nullable: false),
            SelectedStart = table.Column<string>(type: "text", nullable: false),
            SelectedEnd = table.Column<string>(type: "text", nullable: false),
            RiskScore = table.Column<int>(type: "integer", nullable: false),
            EnvironmentalSummaryJson = table.Column<string>(type: "jsonb", nullable: false),
            CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
        }, constraints: table => table.PrimaryKey("PK_SavedPlans", x => x.Id));
        migrationBuilder.CreateIndex(name: "IX_SavedPlans_CreatedAt", table: "SavedPlans", column: "CreatedAt");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("PlanningRequests");
        migrationBuilder.DropTable("SavedPlans");
    }
}
