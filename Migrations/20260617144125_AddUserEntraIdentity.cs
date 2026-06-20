using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CMIForge.Migrations
{
    /// <inheritdoc />
    public partial class AddUserEntraIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EntraObjectId",
                table: "Users",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EntraTenantId",
                table: "Users",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EntraUserPrincipalName",
                table: "Users",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Users_EntraTenantId_EntraObjectId",
                table: "Users",
                columns: new[] { "EntraTenantId", "EntraObjectId" },
                unique: true,
                filter: "[EntraTenantId] <> '' AND [EntraObjectId] <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_Users_EntraUserPrincipalName",
                table: "Users",
                column: "EntraUserPrincipalName",
                unique: true,
                filter: "[EntraUserPrincipalName] <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_EntraTenantId_EntraObjectId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_EntraUserPrincipalName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EntraObjectId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EntraTenantId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EntraUserPrincipalName",
                table: "Users");
        }
    }
}
