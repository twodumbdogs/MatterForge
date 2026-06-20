using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CMIForge.Migrations
{
    /// <inheritdoc />
    public partial class AddConflictSearchesAndParties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConflictSearches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SearchNumber = table.Column<int>(type: "int", nullable: false),
                    SearchName = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    SearchTerms = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    NormalizedTerms = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    FormSubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MatterId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ReviewerDecision = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReviewNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    AiSummary = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConflictSearches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConflictSearches_FormSubmissions_FormSubmissionId",
                        column: x => x.FormSubmissionId,
                        principalTable: "FormSubmissions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ConflictSearches_Matters_MatterId",
                        column: x => x.MatterId,
                        principalTable: "Matters",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ConflictSearches_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ConflictSearches_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Parties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartyNumber = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    PartyType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parties", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConflictSearchResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConflictSearchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MatterId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SearchTerm = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    MatchedName = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    MatchedOn = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    MatchType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    PartyRole = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    RiskLevel = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Explanation = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    AiAssessment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConflictSearchResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConflictSearchResults_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ConflictSearchResults_ConflictSearches_ConflictSearchId",
                        column: x => x.ConflictSearchId,
                        principalTable: "ConflictSearches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConflictSearchResults_Matters_MatterId",
                        column: x => x.MatterId,
                        principalTable: "Matters",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ConflictSearchResults_Parties_PartyId",
                        column: x => x.PartyId,
                        principalTable: "Parties",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MatterParties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MatterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatterParties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatterParties_Matters_MatterId",
                        column: x => x.MatterId,
                        principalTable: "Matters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MatterParties_Parties_PartyId",
                        column: x => x.PartyId,
                        principalTable: "Parties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PartyAliases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Alias = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    NormalizedAlias = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartyAliases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartyAliases_Parties_PartyId",
                        column: x => x.PartyId,
                        principalTable: "Parties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PartyRelationships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromPartyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToPartyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RelationshipType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartyRelationships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartyRelationships_Parties_FromPartyId",
                        column: x => x.FromPartyId,
                        principalTable: "Parties",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PartyRelationships_Parties_ToPartyId",
                        column: x => x.ToPartyId,
                        principalTable: "Parties",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConflictSearches_FormSubmissionId",
                table: "ConflictSearches",
                column: "FormSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_ConflictSearches_MatterId",
                table: "ConflictSearches",
                column: "MatterId");

            migrationBuilder.CreateIndex(
                name: "IX_ConflictSearches_RequestedByUserId",
                table: "ConflictSearches",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ConflictSearches_ReviewedByUserId",
                table: "ConflictSearches",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ConflictSearches_SearchNumber",
                table: "ConflictSearches",
                column: "SearchNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConflictSearchResults_ClientId",
                table: "ConflictSearchResults",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ConflictSearchResults_ConflictSearchId_Score",
                table: "ConflictSearchResults",
                columns: new[] { "ConflictSearchId", "Score" });

            migrationBuilder.CreateIndex(
                name: "IX_ConflictSearchResults_MatterId",
                table: "ConflictSearchResults",
                column: "MatterId");

            migrationBuilder.CreateIndex(
                name: "IX_ConflictSearchResults_PartyId",
                table: "ConflictSearchResults",
                column: "PartyId");

            migrationBuilder.CreateIndex(
                name: "IX_MatterParties_MatterId_PartyId_Role",
                table: "MatterParties",
                columns: new[] { "MatterId", "PartyId", "Role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatterParties_PartyId",
                table: "MatterParties",
                column: "PartyId");

            migrationBuilder.CreateIndex(
                name: "IX_Parties_NormalizedName",
                table: "Parties",
                column: "NormalizedName");

            migrationBuilder.CreateIndex(
                name: "IX_Parties_PartyNumber",
                table: "Parties",
                column: "PartyNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartyAliases_NormalizedAlias",
                table: "PartyAliases",
                column: "NormalizedAlias");

            migrationBuilder.CreateIndex(
                name: "IX_PartyAliases_PartyId_NormalizedAlias",
                table: "PartyAliases",
                columns: new[] { "PartyId", "NormalizedAlias" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartyRelationships_FromPartyId_ToPartyId_RelationshipType",
                table: "PartyRelationships",
                columns: new[] { "FromPartyId", "ToPartyId", "RelationshipType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartyRelationships_ToPartyId",
                table: "PartyRelationships",
                column: "ToPartyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConflictSearchResults");

            migrationBuilder.DropTable(
                name: "MatterParties");

            migrationBuilder.DropTable(
                name: "PartyAliases");

            migrationBuilder.DropTable(
                name: "PartyRelationships");

            migrationBuilder.DropTable(
                name: "ConflictSearches");

            migrationBuilder.DropTable(
                name: "Parties");
        }
    }
}
