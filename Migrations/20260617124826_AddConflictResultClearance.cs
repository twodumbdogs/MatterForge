using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CMIForge.Migrations
{
    /// <inheritdoc />
    public partial class AddConflictResultClearance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClearanceNotes",
                table: "ConflictsResults",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ClearanceStatus",
                table: "ConflictsResults",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClearedAt",
                table: "ConflictsResults",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClearedByUserId",
                table: "ConflictsResults",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConflictsResults_ClearedByUserId_ClearedAt",
                table: "ConflictsResults",
                columns: new[] { "ClearedByUserId", "ClearedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ConflictsResults_ConflictSearchId_ClearanceStatus",
                table: "ConflictsResults",
                columns: new[] { "ConflictSearchId", "ClearanceStatus" });

            migrationBuilder.AddForeignKey(
                name: "FK_ConflictsResults_Users_ClearedByUserId",
                table: "ConflictsResults",
                column: "ClearedByUserId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConflictsResults_Users_ClearedByUserId",
                table: "ConflictsResults");

            migrationBuilder.DropIndex(
                name: "IX_ConflictsResults_ClearedByUserId_ClearedAt",
                table: "ConflictsResults");

            migrationBuilder.DropIndex(
                name: "IX_ConflictsResults_ConflictSearchId_ClearanceStatus",
                table: "ConflictsResults");

            migrationBuilder.DropColumn(
                name: "ClearanceNotes",
                table: "ConflictsResults");

            migrationBuilder.DropColumn(
                name: "ClearanceStatus",
                table: "ConflictsResults");

            migrationBuilder.DropColumn(
                name: "ClearedAt",
                table: "ConflictsResults");

            migrationBuilder.DropColumn(
                name: "ClearedByUserId",
                table: "ConflictsResults");
        }
    }
}
