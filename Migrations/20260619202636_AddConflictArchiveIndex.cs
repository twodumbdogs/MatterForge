using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CMIForge.Migrations
{
    /// <inheritdoc />
    public partial class AddConflictArchiveIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ArchivedAt",
                table: "Conflicts",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ConflictsSearchArchives",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConflictSearchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SearchNumber = table.Column<int>(type: "int", nullable: false),
                    SearchName = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    SearchTerms = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    NormalizedTerms = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    SearchableText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NormalizedSearchableText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FormSubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmissionNumber = table.Column<int>(type: "int", nullable: true),
                    FormName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    MatterId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MatterNumber = table.Column<int>(type: "int", nullable: true),
                    MatterName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClientNumber = table.Column<int>(type: "int", nullable: true),
                    ClientName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestedByDisplayName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedByDisplayName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ReviewerDecision = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ReviewNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    AiSummary = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: false),
                    ResultCount = table.Column<int>(type: "int", nullable: false),
                    HighestRiskLevel = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PayloadCompression = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PayloadBytes = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConflictsSearchArchives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConflictsSearchArchives_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ConflictsSearchArchives_Conflicts_ConflictSearchId",
                        column: x => x.ConflictSearchId,
                        principalTable: "Conflicts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConflictsSearchArchives_FormSubmissions_FormSubmissionId",
                        column: x => x.FormSubmissionId,
                        principalTable: "FormSubmissions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ConflictsSearchArchives_Matters_MatterId",
                        column: x => x.MatterId,
                        principalTable: "Matters",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ConflictsSearchArchives_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ConflictsSearchArchives_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ConflictsHitArchives",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConflictSearchArchiveId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConflictSearchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConflictSearchResultId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SearchNumber = table.Column<int>(type: "int", nullable: false),
                    SearchTerm = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    MatchedName = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    MatchedOn = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    MatchType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    PartyRole = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    PartyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PartyName = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    MatterId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MatterNumber = table.Column<int>(type: "int", nullable: true),
                    MatterName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClientNumber = table.Column<int>(type: "int", nullable: true),
                    ClientName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    RiskLevel = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Explanation = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    AiAssessment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ClearanceStatus = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ClearanceNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ClearedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClearedByDisplayName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    ClearedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SearchableText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NormalizedSearchableText = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConflictsHitArchives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConflictsHitArchives_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ConflictsHitArchives_ConflictsSearchArchives_ConflictSearchArchiveId",
                        column: x => x.ConflictSearchArchiveId,
                        principalTable: "ConflictsSearchArchives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConflictsHitArchives_Matters_MatterId",
                        column: x => x.MatterId,
                        principalTable: "Matters",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ConflictsHitArchives_Parties_PartyId",
                        column: x => x.PartyId,
                        principalTable: "Parties",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ConflictsHitArchives_Users_ClearedByUserId",
                        column: x => x.ClearedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Conflicts_ArchivedAt_SearchNumber",
                table: "Conflicts",
                columns: new[] { "ArchivedAt", "SearchNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_ConflictsHitArchives_ClearedByUserId",
                table: "ConflictsHitArchives",
                column: "ClearedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ConflictsHitArchives_ClientId_SearchNumber",
                table: "ConflictsHitArchives",
                columns: new[] { "ClientId", "SearchNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_ConflictsHitArchives_ConflictSearchArchiveId_ClearanceStatus",
                table: "ConflictsHitArchives",
                columns: new[] { "ConflictSearchArchiveId", "ClearanceStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_ConflictsHitArchives_ConflictSearchArchiveId_Score",
                table: "ConflictsHitArchives",
                columns: new[] { "ConflictSearchArchiveId", "Score" });

            migrationBuilder.CreateIndex(
                name: "IX_ConflictsHitArchives_ConflictSearchId_Score",
                table: "ConflictsHitArchives",
                columns: new[] { "ConflictSearchId", "Score" });

            migrationBuilder.CreateIndex(
                name: "IX_ConflictsHitArchives_MatterId_SearchNumber",
                table: "ConflictsHitArchives",
                columns: new[] { "MatterId", "SearchNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_ConflictsHitArchives_PartyId_SearchNumber",
                table: "ConflictsHitArchives",
                columns: new[] { "PartyId", "SearchNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_ConflictsSearchArchives_ArchivedAt_SearchNumber",
                table: "ConflictsSearchArchives",
                columns: new[] { "ArchivedAt", "SearchNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_ConflictsSearchArchives_ClientId_SearchNumber",
                table: "ConflictsSearchArchives",
                columns: new[] { "ClientId", "SearchNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_ConflictsSearchArchives_ConflictSearchId",
                table: "ConflictsSearchArchives",
                column: "ConflictSearchId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConflictsSearchArchives_FormSubmissionId",
                table: "ConflictsSearchArchives",
                column: "FormSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_ConflictsSearchArchives_MatterId_SearchNumber",
                table: "ConflictsSearchArchives",
                columns: new[] { "MatterId", "SearchNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_ConflictsSearchArchives_RequestedByUserId",
                table: "ConflictsSearchArchives",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ConflictsSearchArchives_ReviewedByUserId",
                table: "ConflictsSearchArchives",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ConflictsSearchArchives_ReviewerDecision_ArchivedAt",
                table: "ConflictsSearchArchives",
                columns: new[] { "ReviewerDecision", "ArchivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ConflictsSearchArchives_SearchNumber",
                table: "ConflictsSearchArchives",
                column: "SearchNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConflictsSearchArchives_Status_ArchivedAt",
                table: "ConflictsSearchArchives",
                columns: new[] { "Status", "ArchivedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConflictsHitArchives");

            migrationBuilder.DropTable(
                name: "ConflictsSearchArchives");

            migrationBuilder.DropIndex(
                name: "IX_Conflicts_ArchivedAt_SearchNumber",
                table: "Conflicts");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "Conflicts");
        }
    }
}
