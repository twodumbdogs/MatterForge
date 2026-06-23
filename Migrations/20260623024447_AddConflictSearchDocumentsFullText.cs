using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CMIForge.Migrations
{
    /// <inheritdoc />
    public partial class AddConflictSearchDocumentsFullText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConflictSearchDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MatterId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MatchedName = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    MatchedOn = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    MatchType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    PartyRole = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SearchableText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NormalizedSearchableText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SortNumber = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConflictSearchDocuments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConflictSearchDocuments_ClientId",
                table: "ConflictSearchDocuments",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ConflictSearchDocuments_MatterId",
                table: "ConflictSearchDocuments",
                column: "MatterId");

            migrationBuilder.CreateIndex(
                name: "IX_ConflictSearchDocuments_PartyId",
                table: "ConflictSearchDocuments",
                column: "PartyId");

            migrationBuilder.CreateIndex(
                name: "IX_ConflictSearchDocuments_SortNumber",
                table: "ConflictSearchDocuments",
                column: "SortNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ConflictSearchDocuments_SourceType_SourceId",
                table: "ConflictSearchDocuments",
                columns: new[] { "SourceType", "SourceId" },
                unique: true);

            migrationBuilder.Sql("""
                IF FULLTEXTSERVICEPROPERTY('IsFullTextInstalled') = 1
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE [name] = N'CMIForgeConflictSearchCatalog')
                    BEGIN
                        CREATE FULLTEXT CATALOG [CMIForgeConflictSearchCatalog] WITH ACCENT_SENSITIVITY = OFF;
                    END;

                    IF NOT EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE [object_id] = OBJECT_ID(N'[dbo].[ConflictSearchDocuments]'))
                    BEGIN
                        CREATE FULLTEXT INDEX ON [dbo].[ConflictSearchDocuments]
                        (
                            [SearchableText] LANGUAGE 1033,
                            [NormalizedSearchableText] LANGUAGE 1033
                        )
                        KEY INDEX [PK_ConflictSearchDocuments]
                        ON [CMIForgeConflictSearchCatalog]
                        WITH CHANGE_TRACKING AUTO;
                    END;
                END;
                """, suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE [object_id] = OBJECT_ID(N'[dbo].[ConflictSearchDocuments]'))
                BEGIN
                    DROP FULLTEXT INDEX ON [dbo].[ConflictSearchDocuments];
                END;

                IF EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE [name] = N'CMIForgeConflictSearchCatalog')
                    AND NOT EXISTS (
                        SELECT 1
                        FROM sys.fulltext_indexes
                        WHERE fulltext_catalog_id = (
                            SELECT fulltext_catalog_id
                            FROM sys.fulltext_catalogs
                            WHERE [name] = N'CMIForgeConflictSearchCatalog'
                        )
                    )
                BEGIN
                    DROP FULLTEXT CATALOG [CMIForgeConflictSearchCatalog];
                END;
                """, suppressTransaction: true);

            migrationBuilder.DropTable(
                name: "ConflictSearchDocuments");
        }
    }
}
