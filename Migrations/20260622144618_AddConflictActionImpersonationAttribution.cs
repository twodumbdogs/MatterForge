using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CMIForge.Migrations
{
    /// <inheritdoc />
    public partial class AddConflictActionImpersonationAttribution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClearedAsUserId",
                table: "ConflictsResults",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EscalatedAsUserId",
                table: "ConflictsResults",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EscalationApprovedAsUserId",
                table: "ConflictsResults",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClearedAsDisplayName",
                table: "ConflictsHitArchives",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ClearedAsUserId",
                table: "ConflictsHitArchives",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConflictsResults_ClearedAsUserId",
                table: "ConflictsResults",
                column: "ClearedAsUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ConflictsResults_EscalatedAsUserId",
                table: "ConflictsResults",
                column: "EscalatedAsUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ConflictsResults_EscalationApprovedAsUserId",
                table: "ConflictsResults",
                column: "EscalationApprovedAsUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ConflictsHitArchives_ClearedAsUserId",
                table: "ConflictsHitArchives",
                column: "ClearedAsUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ConflictsHitArchives_Users_ClearedAsUserId",
                table: "ConflictsHitArchives",
                column: "ClearedAsUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ConflictsResults_Users_ClearedAsUserId",
                table: "ConflictsResults",
                column: "ClearedAsUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ConflictsResults_Users_EscalatedAsUserId",
                table: "ConflictsResults",
                column: "EscalatedAsUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ConflictsResults_Users_EscalationApprovedAsUserId",
                table: "ConflictsResults",
                column: "EscalationApprovedAsUserId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConflictsHitArchives_Users_ClearedAsUserId",
                table: "ConflictsHitArchives");

            migrationBuilder.DropForeignKey(
                name: "FK_ConflictsResults_Users_ClearedAsUserId",
                table: "ConflictsResults");

            migrationBuilder.DropForeignKey(
                name: "FK_ConflictsResults_Users_EscalatedAsUserId",
                table: "ConflictsResults");

            migrationBuilder.DropForeignKey(
                name: "FK_ConflictsResults_Users_EscalationApprovedAsUserId",
                table: "ConflictsResults");

            migrationBuilder.DropIndex(
                name: "IX_ConflictsResults_ClearedAsUserId",
                table: "ConflictsResults");

            migrationBuilder.DropIndex(
                name: "IX_ConflictsResults_EscalatedAsUserId",
                table: "ConflictsResults");

            migrationBuilder.DropIndex(
                name: "IX_ConflictsResults_EscalationApprovedAsUserId",
                table: "ConflictsResults");

            migrationBuilder.DropIndex(
                name: "IX_ConflictsHitArchives_ClearedAsUserId",
                table: "ConflictsHitArchives");

            migrationBuilder.DropColumn(
                name: "ClearedAsUserId",
                table: "ConflictsResults");

            migrationBuilder.DropColumn(
                name: "EscalatedAsUserId",
                table: "ConflictsResults");

            migrationBuilder.DropColumn(
                name: "EscalationApprovedAsUserId",
                table: "ConflictsResults");

            migrationBuilder.DropColumn(
                name: "ClearedAsDisplayName",
                table: "ConflictsHitArchives");

            migrationBuilder.DropColumn(
                name: "ClearedAsUserId",
                table: "ConflictsHitArchives");
        }
    }
}
