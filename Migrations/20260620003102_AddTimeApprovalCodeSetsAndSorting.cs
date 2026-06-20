using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CMIForge.Migrations
{
    /// <inheritdoc />
    public partial class AddTimeApprovalCodeSetsAndSorting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Narrative",
                table: "TimeEntries",
                newName: "InternalNotes");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ApprovedAt",
                table: "TimeEntries",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedByUserId",
                table: "TimeEntries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientNarrative",
                table: "TimeEntries",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExportedAt",
                table: "TimeEntries",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExportedByUserId",
                table: "TimeEntries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SubmittedAt",
                table: "TimeEntries",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TimePhaseId",
                table: "TimeEntries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TimeTaskId",
                table: "TimeEntries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE TimeEntries
                SET ClientNarrative = InternalNotes,
                    InternalNotes = ''
                WHERE ClientNarrative = '';

                UPDATE TimeEntries
                SET SubmittedAt = COALESCE(SubmittedAt, UpdatedAt, CreatedAt)
                WHERE Status IN ('Submitted', 'Approved', 'Billed', 'No Charge');

                UPDATE TimeEntries
                SET ApprovedAt = COALESCE(ApprovedAt, UpdatedAt, CreatedAt)
                WHERE Status IN ('Approved', 'Billed', 'No Charge');

                UPDATE TimeEntries
                SET Status = 'Approved'
                WHERE Status IN ('Billed', 'No Charge');
                """);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresTimeApproval",
                table: "Matters",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "TimeCodeSetId",
                table: "Matters",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TimeIncrementMinutes",
                table: "Matters",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TimeCodeSets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeCodeSets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TimePhases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TimeCodeSetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimePhases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TimePhases_TimeCodeSets_TimeCodeSetId",
                        column: x => x.TimeCodeSetId,
                        principalTable: "TimeCodeSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TimeTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TimeCodeSetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TimePhaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TimeTasks_TimeCodeSets_TimeCodeSetId",
                        column: x => x.TimeCodeSetId,
                        principalTable: "TimeCodeSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TimeTasks_TimePhases_TimePhaseId",
                        column: x => x.TimePhaseId,
                        principalTable: "TimePhases",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_TimeEntries_ApprovedByUserId",
                table: "TimeEntries",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TimeEntries_ExportedAt_WorkDate",
                table: "TimeEntries",
                columns: new[] { "ExportedAt", "WorkDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TimeEntries_ExportedByUserId",
                table: "TimeEntries",
                column: "ExportedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TimeEntries_Status_WorkDate",
                table: "TimeEntries",
                columns: new[] { "Status", "WorkDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TimeEntries_TimePhaseId",
                table: "TimeEntries",
                column: "TimePhaseId");

            migrationBuilder.CreateIndex(
                name: "IX_TimeEntries_TimeTaskId",
                table: "TimeEntries",
                column: "TimeTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_Matters_TimeCodeSetId",
                table: "Matters",
                column: "TimeCodeSetId");

            migrationBuilder.CreateIndex(
                name: "IX_TimeCodeSets_Key",
                table: "TimeCodeSets",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TimePhases_TimeCodeSetId_Code",
                table: "TimePhases",
                columns: new[] { "TimeCodeSetId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TimePhases_TimeCodeSetId_SortOrder",
                table: "TimePhases",
                columns: new[] { "TimeCodeSetId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_TimeTasks_TimeCodeSetId_Code",
                table: "TimeTasks",
                columns: new[] { "TimeCodeSetId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TimeTasks_TimeCodeSetId_SortOrder",
                table: "TimeTasks",
                columns: new[] { "TimeCodeSetId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_TimeTasks_TimePhaseId",
                table: "TimeTasks",
                column: "TimePhaseId");

            migrationBuilder.AddForeignKey(
                name: "FK_Matters_TimeCodeSets_TimeCodeSetId",
                table: "Matters",
                column: "TimeCodeSetId",
                principalTable: "TimeCodeSets",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TimeEntries_TimePhases_TimePhaseId",
                table: "TimeEntries",
                column: "TimePhaseId",
                principalTable: "TimePhases",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TimeEntries_TimeTasks_TimeTaskId",
                table: "TimeEntries",
                column: "TimeTaskId",
                principalTable: "TimeTasks",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TimeEntries_Users_ApprovedByUserId",
                table: "TimeEntries",
                column: "ApprovedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TimeEntries_Users_ExportedByUserId",
                table: "TimeEntries",
                column: "ExportedByUserId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Matters_TimeCodeSets_TimeCodeSetId",
                table: "Matters");

            migrationBuilder.DropForeignKey(
                name: "FK_TimeEntries_TimePhases_TimePhaseId",
                table: "TimeEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_TimeEntries_TimeTasks_TimeTaskId",
                table: "TimeEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_TimeEntries_Users_ApprovedByUserId",
                table: "TimeEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_TimeEntries_Users_ExportedByUserId",
                table: "TimeEntries");

            migrationBuilder.DropTable(
                name: "TimeTasks");

            migrationBuilder.DropTable(
                name: "TimePhases");

            migrationBuilder.DropTable(
                name: "TimeCodeSets");

            migrationBuilder.DropIndex(
                name: "IX_TimeEntries_ApprovedByUserId",
                table: "TimeEntries");

            migrationBuilder.DropIndex(
                name: "IX_TimeEntries_ExportedAt_WorkDate",
                table: "TimeEntries");

            migrationBuilder.DropIndex(
                name: "IX_TimeEntries_ExportedByUserId",
                table: "TimeEntries");

            migrationBuilder.DropIndex(
                name: "IX_TimeEntries_Status_WorkDate",
                table: "TimeEntries");

            migrationBuilder.DropIndex(
                name: "IX_TimeEntries_TimePhaseId",
                table: "TimeEntries");

            migrationBuilder.DropIndex(
                name: "IX_TimeEntries_TimeTaskId",
                table: "TimeEntries");

            migrationBuilder.DropIndex(
                name: "IX_Matters_TimeCodeSetId",
                table: "Matters");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "TimeEntries");

            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                table: "TimeEntries");

            migrationBuilder.Sql("""
                UPDATE TimeEntries
                SET InternalNotes = ClientNarrative
                WHERE InternalNotes = '' AND ClientNarrative <> '';
                """);

            migrationBuilder.DropColumn(
                name: "ClientNarrative",
                table: "TimeEntries");

            migrationBuilder.DropColumn(
                name: "ExportedAt",
                table: "TimeEntries");

            migrationBuilder.DropColumn(
                name: "ExportedByUserId",
                table: "TimeEntries");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "TimeEntries");

            migrationBuilder.DropColumn(
                name: "TimePhaseId",
                table: "TimeEntries");

            migrationBuilder.DropColumn(
                name: "TimeTaskId",
                table: "TimeEntries");

            migrationBuilder.DropColumn(
                name: "RequiresTimeApproval",
                table: "Matters");

            migrationBuilder.DropColumn(
                name: "TimeCodeSetId",
                table: "Matters");

            migrationBuilder.DropColumn(
                name: "TimeIncrementMinutes",
                table: "Matters");

            migrationBuilder.RenameColumn(
                name: "InternalNotes",
                table: "TimeEntries",
                newName: "Narrative");
        }
    }
}
