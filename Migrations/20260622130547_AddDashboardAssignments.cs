using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CMIForge.Migrations
{
    /// <inheritdoc />
    public partial class AddDashboardAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DashboardAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DashboardKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SecurityRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DashboardAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DashboardAssignments_SecurityRoles_SecurityRoleId",
                        column: x => x.SecurityRoleId,
                        principalTable: "SecurityRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DashboardAssignments_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DashboardAssignments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DashboardAssignments_DashboardKey",
                table: "DashboardAssignments",
                column: "DashboardKey");

            migrationBuilder.CreateIndex(
                name: "IX_DashboardAssignments_DashboardKey_SecurityRoleId",
                table: "DashboardAssignments",
                columns: new[] { "DashboardKey", "SecurityRoleId" },
                unique: true,
                filter: "[SecurityRoleId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DashboardAssignments_DashboardKey_TeamId",
                table: "DashboardAssignments",
                columns: new[] { "DashboardKey", "TeamId" },
                unique: true,
                filter: "[TeamId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DashboardAssignments_DashboardKey_UserId",
                table: "DashboardAssignments",
                columns: new[] { "DashboardKey", "UserId" },
                unique: true,
                filter: "[UserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DashboardAssignments_SecurityRoleId",
                table: "DashboardAssignments",
                column: "SecurityRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_DashboardAssignments_TeamId",
                table: "DashboardAssignments",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_DashboardAssignments_UserId",
                table: "DashboardAssignments",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DashboardAssignments");
        }
    }
}
