using System;
using Havaz.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace Havaz.Infrastructure.Migrations;

[DbContext(typeof(HavazDbContext))]
[Migration("20260901200000_AuthOwnership")]
public partial class AuthOwnership : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "Users", columns: table => new
        {
            Id = table.Column<Guid>(type: "uuid", nullable: false),
            DisplayName = table.Column<string>(type: "text", nullable: false),
            Email = table.Column<string>(type: "text", nullable: false),
            NormalizedEmail = table.Column<string>(type: "text", nullable: false),
            PasswordHash = table.Column<string>(type: "text", nullable: false),
            Role = table.Column<string>(type: "text", nullable: false),
            IsActive = table.Column<bool>(type: "boolean", nullable: false),
            CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
        }, constraints: table => table.PrimaryKey("PK_Users", x => x.Id));
        migrationBuilder.AddColumn<Guid>(name: "UserId", table: "PlanningRequests", type: "uuid", nullable: false, defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        migrationBuilder.AddColumn<Guid>(name: "UserId", table: "SavedPlans", type: "uuid", nullable: false, defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        migrationBuilder.CreateIndex(name: "IX_Users_NormalizedEmail", table: "Users", column: "NormalizedEmail", unique: true);
        migrationBuilder.CreateIndex(name: "IX_PlanningRequests_UserId", table: "PlanningRequests", column: "UserId");
        migrationBuilder.CreateIndex(name: "IX_SavedPlans_UserId", table: "SavedPlans", column: "UserId");
        migrationBuilder.AddForeignKey(name: "FK_PlanningRequests_Users_UserId", table: "PlanningRequests", column: "UserId", principalTable: "Users", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
        migrationBuilder.AddForeignKey(name: "FK_SavedPlans_Users_UserId", table: "SavedPlans", column: "UserId", principalTable: "Users", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("FK_PlanningRequests_Users_UserId", "PlanningRequests");
        migrationBuilder.DropForeignKey("FK_SavedPlans_Users_UserId", "SavedPlans");
        migrationBuilder.DropTable("Users");
        migrationBuilder.DropIndex("IX_PlanningRequests_UserId", "PlanningRequests");
        migrationBuilder.DropIndex("IX_SavedPlans_UserId", "SavedPlans");
        migrationBuilder.DropColumn("UserId", "PlanningRequests");
        migrationBuilder.DropColumn("UserId", "SavedPlans");
    }
}
