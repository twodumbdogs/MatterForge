using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CMIForge.Migrations
{
    /// <inheritdoc />
    public partial class AddEnhancementVotesAndClientAliases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClientAliases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Alias = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    NormalizedAlias = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientAliases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientAliases_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EnhancementRequestVotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnhancementRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnhancementRequestVotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnhancementRequestVotes_EnhancementRequests_EnhancementRequestId",
                        column: x => x.EnhancementRequestId,
                        principalTable: "EnhancementRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EnhancementRequestVotes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientAliases_ClientId_NormalizedAlias",
                table: "ClientAliases",
                columns: new[] { "ClientId", "NormalizedAlias" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientAliases_NormalizedAlias",
                table: "ClientAliases",
                column: "NormalizedAlias");

            migrationBuilder.CreateIndex(
                name: "IX_EnhancementRequestVotes_EnhancementRequestId_UserId",
                table: "EnhancementRequestVotes",
                columns: new[] { "EnhancementRequestId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EnhancementRequestVotes_UserId_CreatedAt",
                table: "EnhancementRequestVotes",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientAliases");

            migrationBuilder.DropTable(
                name: "EnhancementRequestVotes");
        }
    }
}
