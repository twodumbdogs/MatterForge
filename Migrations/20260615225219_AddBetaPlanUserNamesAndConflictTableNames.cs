using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CMIForge.Migrations
{
    /// <inheritdoc />
    public partial class AddBetaPlanUserNamesAndConflictTableNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConflictSearches_FormSubmissions_FormSubmissionId",
                table: "ConflictSearches");

            migrationBuilder.DropForeignKey(
                name: "FK_ConflictSearches_Matters_MatterId",
                table: "ConflictSearches");

            migrationBuilder.DropForeignKey(
                name: "FK_ConflictSearches_Users_RequestedByUserId",
                table: "ConflictSearches");

            migrationBuilder.DropForeignKey(
                name: "FK_ConflictSearches_Users_ReviewedByUserId",
                table: "ConflictSearches");

            migrationBuilder.DropForeignKey(
                name: "FK_ConflictSearchResults_Clients_ClientId",
                table: "ConflictSearchResults");

            migrationBuilder.DropForeignKey(
                name: "FK_ConflictSearchResults_ConflictSearches_ConflictSearchId",
                table: "ConflictSearchResults");

            migrationBuilder.DropForeignKey(
                name: "FK_ConflictSearchResults_Matters_MatterId",
                table: "ConflictSearchResults");

            migrationBuilder.DropForeignKey(
                name: "FK_ConflictSearchResults_Parties_PartyId",
                table: "ConflictSearchResults");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ConflictSearchResults",
                table: "ConflictSearchResults");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ConflictSearches",
                table: "ConflictSearches");

            migrationBuilder.RenameTable(
                name: "ConflictSearchResults",
                newName: "ConflictsResults");

            migrationBuilder.RenameTable(
                name: "ConflictSearches",
                newName: "Conflicts");

            migrationBuilder.RenameIndex(
                name: "IX_ConflictSearchResults_PartyId",
                table: "ConflictsResults",
                newName: "IX_ConflictsResults_PartyId");

            migrationBuilder.RenameIndex(
                name: "IX_ConflictSearchResults_MatterId",
                table: "ConflictsResults",
                newName: "IX_ConflictsResults_MatterId");

            migrationBuilder.RenameIndex(
                name: "IX_ConflictSearchResults_ConflictSearchId_Score",
                table: "ConflictsResults",
                newName: "IX_ConflictsResults_ConflictSearchId_Score");

            migrationBuilder.RenameIndex(
                name: "IX_ConflictSearchResults_ClientId",
                table: "ConflictsResults",
                newName: "IX_ConflictsResults_ClientId");

            migrationBuilder.RenameIndex(
                name: "IX_ConflictSearches_SearchNumber",
                table: "Conflicts",
                newName: "IX_Conflicts_SearchNumber");

            migrationBuilder.RenameIndex(
                name: "IX_ConflictSearches_ReviewedByUserId",
                table: "Conflicts",
                newName: "IX_Conflicts_ReviewedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_ConflictSearches_RequestedByUserId",
                table: "Conflicts",
                newName: "IX_Conflicts_RequestedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_ConflictSearches_MatterId",
                table: "Conflicts",
                newName: "IX_Conflicts_MatterId");

            migrationBuilder.RenameIndex(
                name: "IX_ConflictSearches_FormSubmissionId",
                table: "Conflicts",
                newName: "IX_Conflicts_FormSubmissionId");

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "Users",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "Users",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MiddleName",
                table: "Users",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ConflictsResults",
                table: "ConflictsResults",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Conflicts",
                table: "Conflicts",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Conflicts_FormSubmissions_FormSubmissionId",
                table: "Conflicts",
                column: "FormSubmissionId",
                principalTable: "FormSubmissions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Conflicts_Matters_MatterId",
                table: "Conflicts",
                column: "MatterId",
                principalTable: "Matters",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Conflicts_Users_RequestedByUserId",
                table: "Conflicts",
                column: "RequestedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Conflicts_Users_ReviewedByUserId",
                table: "Conflicts",
                column: "ReviewedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ConflictsResults_Clients_ClientId",
                table: "ConflictsResults",
                column: "ClientId",
                principalTable: "Clients",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ConflictsResults_Conflicts_ConflictSearchId",
                table: "ConflictsResults",
                column: "ConflictSearchId",
                principalTable: "Conflicts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ConflictsResults_Matters_MatterId",
                table: "ConflictsResults",
                column: "MatterId",
                principalTable: "Matters",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ConflictsResults_Parties_PartyId",
                table: "ConflictsResults",
                column: "PartyId",
                principalTable: "Parties",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Conflicts_FormSubmissions_FormSubmissionId",
                table: "Conflicts");

            migrationBuilder.DropForeignKey(
                name: "FK_Conflicts_Matters_MatterId",
                table: "Conflicts");

            migrationBuilder.DropForeignKey(
                name: "FK_Conflicts_Users_RequestedByUserId",
                table: "Conflicts");

            migrationBuilder.DropForeignKey(
                name: "FK_Conflicts_Users_ReviewedByUserId",
                table: "Conflicts");

            migrationBuilder.DropForeignKey(
                name: "FK_ConflictsResults_Clients_ClientId",
                table: "ConflictsResults");

            migrationBuilder.DropForeignKey(
                name: "FK_ConflictsResults_Conflicts_ConflictSearchId",
                table: "ConflictsResults");

            migrationBuilder.DropForeignKey(
                name: "FK_ConflictsResults_Matters_MatterId",
                table: "ConflictsResults");

            migrationBuilder.DropForeignKey(
                name: "FK_ConflictsResults_Parties_PartyId",
                table: "ConflictsResults");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ConflictsResults",
                table: "ConflictsResults");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Conflicts",
                table: "Conflicts");

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "MiddleName",
                table: "Users");

            migrationBuilder.RenameTable(
                name: "ConflictsResults",
                newName: "ConflictSearchResults");

            migrationBuilder.RenameTable(
                name: "Conflicts",
                newName: "ConflictSearches");

            migrationBuilder.RenameIndex(
                name: "IX_ConflictsResults_PartyId",
                table: "ConflictSearchResults",
                newName: "IX_ConflictSearchResults_PartyId");

            migrationBuilder.RenameIndex(
                name: "IX_ConflictsResults_MatterId",
                table: "ConflictSearchResults",
                newName: "IX_ConflictSearchResults_MatterId");

            migrationBuilder.RenameIndex(
                name: "IX_ConflictsResults_ConflictSearchId_Score",
                table: "ConflictSearchResults",
                newName: "IX_ConflictSearchResults_ConflictSearchId_Score");

            migrationBuilder.RenameIndex(
                name: "IX_ConflictsResults_ClientId",
                table: "ConflictSearchResults",
                newName: "IX_ConflictSearchResults_ClientId");

            migrationBuilder.RenameIndex(
                name: "IX_Conflicts_SearchNumber",
                table: "ConflictSearches",
                newName: "IX_ConflictSearches_SearchNumber");

            migrationBuilder.RenameIndex(
                name: "IX_Conflicts_ReviewedByUserId",
                table: "ConflictSearches",
                newName: "IX_ConflictSearches_ReviewedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_Conflicts_RequestedByUserId",
                table: "ConflictSearches",
                newName: "IX_ConflictSearches_RequestedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_Conflicts_MatterId",
                table: "ConflictSearches",
                newName: "IX_ConflictSearches_MatterId");

            migrationBuilder.RenameIndex(
                name: "IX_Conflicts_FormSubmissionId",
                table: "ConflictSearches",
                newName: "IX_ConflictSearches_FormSubmissionId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ConflictSearchResults",
                table: "ConflictSearchResults",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ConflictSearches",
                table: "ConflictSearches",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ConflictSearches_FormSubmissions_FormSubmissionId",
                table: "ConflictSearches",
                column: "FormSubmissionId",
                principalTable: "FormSubmissions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ConflictSearches_Matters_MatterId",
                table: "ConflictSearches",
                column: "MatterId",
                principalTable: "Matters",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ConflictSearches_Users_RequestedByUserId",
                table: "ConflictSearches",
                column: "RequestedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ConflictSearches_Users_ReviewedByUserId",
                table: "ConflictSearches",
                column: "ReviewedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ConflictSearchResults_Clients_ClientId",
                table: "ConflictSearchResults",
                column: "ClientId",
                principalTable: "Clients",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ConflictSearchResults_ConflictSearches_ConflictSearchId",
                table: "ConflictSearchResults",
                column: "ConflictSearchId",
                principalTable: "ConflictSearches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ConflictSearchResults_Matters_MatterId",
                table: "ConflictSearchResults",
                column: "MatterId",
                principalTable: "Matters",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ConflictSearchResults_Parties_PartyId",
                table: "ConflictSearchResults",
                column: "PartyId",
                principalTable: "Parties",
                principalColumn: "Id");
        }
    }
}
