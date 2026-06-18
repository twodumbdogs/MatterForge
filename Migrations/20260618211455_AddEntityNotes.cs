using System;
using MatterForge.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MatterForge.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(MatterForgeDbContext))]
    [Migration("20260618211455_AddEntityNotes")]
    public partial class AddEntityNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EntityNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntityNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntityNotes_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_EntityNotes_CreatedByUserId",
                table: "EntityNotes",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EntityNotes_EntityType_EntityId_CreatedAt",
                table: "EntityNotes",
                columns: new[] { "EntityType", "EntityId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EntityNotes");
        }
    }
}
