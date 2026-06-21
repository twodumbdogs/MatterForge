using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CMIForge.Migrations
{
    /// <inheritdoc />
    public partial class AddConflictResultEscalations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EscalatedAt",
                table: "ConflictsResults",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EscalatedByUserId",
                table: "ConflictsResults",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EscalatedToUserId",
                table: "ConflictsResults",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EscalationApprovalNotes",
                table: "ConflictsResults",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EscalationApprovedAt",
                table: "ConflictsResults",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EscalationApprovedByUserId",
                table: "ConflictsResults",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EscalationNotes",
                table: "ConflictsResults",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_ConflictsResults_EscalatedByUserId",
                table: "ConflictsResults",
                column: "EscalatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ConflictsResults_EscalatedToUserId_EscalatedAt",
                table: "ConflictsResults",
                columns: new[] { "EscalatedToUserId", "EscalatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ConflictsResults_EscalationApprovedByUserId_EscalationApprovedAt",
                table: "ConflictsResults",
                columns: new[] { "EscalationApprovedByUserId", "EscalationApprovedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_ConflictsResults_Users_EscalatedByUserId",
                table: "ConflictsResults",
                column: "EscalatedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ConflictsResults_Users_EscalatedToUserId",
                table: "ConflictsResults",
                column: "EscalatedToUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ConflictsResults_Users_EscalationApprovedByUserId",
                table: "ConflictsResults",
                column: "EscalationApprovedByUserId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConflictsResults_Users_EscalatedByUserId",
                table: "ConflictsResults");

            migrationBuilder.DropForeignKey(
                name: "FK_ConflictsResults_Users_EscalatedToUserId",
                table: "ConflictsResults");

            migrationBuilder.DropForeignKey(
                name: "FK_ConflictsResults_Users_EscalationApprovedByUserId",
                table: "ConflictsResults");

            migrationBuilder.DropIndex(
                name: "IX_ConflictsResults_EscalatedByUserId",
                table: "ConflictsResults");

            migrationBuilder.DropIndex(
                name: "IX_ConflictsResults_EscalatedToUserId_EscalatedAt",
                table: "ConflictsResults");

            migrationBuilder.DropIndex(
                name: "IX_ConflictsResults_EscalationApprovedByUserId_EscalationApprovedAt",
                table: "ConflictsResults");

            migrationBuilder.DropColumn(
                name: "EscalatedAt",
                table: "ConflictsResults");

            migrationBuilder.DropColumn(
                name: "EscalatedByUserId",
                table: "ConflictsResults");

            migrationBuilder.DropColumn(
                name: "EscalatedToUserId",
                table: "ConflictsResults");

            migrationBuilder.DropColumn(
                name: "EscalationApprovalNotes",
                table: "ConflictsResults");

            migrationBuilder.DropColumn(
                name: "EscalationApprovedAt",
                table: "ConflictsResults");

            migrationBuilder.DropColumn(
                name: "EscalationApprovedByUserId",
                table: "ConflictsResults");

            migrationBuilder.DropColumn(
                name: "EscalationNotes",
                table: "ConflictsResults");
        }
    }
}
