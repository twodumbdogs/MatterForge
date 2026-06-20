using CMIForge.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CMIForge.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(CMIForgeDbContext))]
    [Migration("20260619173500_AddContactArchiveAndSystemArchive")]
    public partial class AddContactArchiveAndSystemArchive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Contacts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_IsArchived_ContactNumber",
                table: "Contacts",
                columns: new[] { "IsArchived", "ContactNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_IsArchived_DisplayName",
                table: "Contacts",
                columns: new[] { "IsArchived", "DisplayName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Contacts_IsArchived_ContactNumber",
                table: "Contacts");

            migrationBuilder.DropIndex(
                name: "IX_Contacts_IsArchived_DisplayName",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Contacts");
        }
    }
}