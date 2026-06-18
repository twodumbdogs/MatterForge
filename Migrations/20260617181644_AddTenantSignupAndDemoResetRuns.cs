using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MatterForge.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantSignupAndDemoResetRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DemoResetRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Trigger = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    DeletedRows = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Error = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemoResetRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantProvisioningRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirmName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AdminFirstName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    AdminLastName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    AdminEmail = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    DesiredDomain = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    DesiredSubdomain = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Plan = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    InternalNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantProvisioningRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantProvisioningRequests_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_DemoResetRuns_StartedAt",
                table: "DemoResetRuns",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_DemoResetRuns_Status_StartedAt",
                table: "DemoResetRuns",
                columns: new[] { "Status", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantProvisioningRequests_AdminEmail",
                table: "TenantProvisioningRequests",
                column: "AdminEmail");

            migrationBuilder.CreateIndex(
                name: "IX_TenantProvisioningRequests_Status_CreatedAt",
                table: "TenantProvisioningRequests",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantProvisioningRequests_UpdatedByUserId",
                table: "TenantProvisioningRequests",
                column: "UpdatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DemoResetRuns");

            migrationBuilder.DropTable(
                name: "TenantProvisioningRequests");
        }
    }
}
