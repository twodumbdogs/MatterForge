using MatterForge.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MatterForge.Migrations
{
    [DbContext(typeof(MatterForgeDbContext))]
    [Migration("20260618214500_AddArchiveFlagsAndBranding")]
    public partial class AddArchiveFlagsAndBranding : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Parties",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Matters",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Clients",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Users_IsArchived_SystemId",
                table: "Users",
                columns: new[] { "IsArchived", "SystemId" });

            migrationBuilder.CreateIndex(
                name: "IX_Parties_IsArchived_PartyNumber",
                table: "Parties",
                columns: new[] { "IsArchived", "PartyNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Matters_IsArchived_MatterNumber",
                table: "Matters",
                columns: new[] { "IsArchived", "MatterNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Clients_IsArchived_ClientNumber",
                table: "Clients",
                columns: new[] { "IsArchived", "ClientNumber" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Users_IsArchived_SystemId", table: "Users");
            migrationBuilder.DropIndex(name: "IX_Parties_IsArchived_PartyNumber", table: "Parties");
            migrationBuilder.DropIndex(name: "IX_Matters_IsArchived_MatterNumber", table: "Matters");
            migrationBuilder.DropIndex(name: "IX_Clients_IsArchived_ClientNumber", table: "Clients");

            migrationBuilder.DropColumn(name: "IsArchived", table: "Users");
            migrationBuilder.DropColumn(name: "IsArchived", table: "Parties");
            migrationBuilder.DropColumn(name: "IsArchived", table: "Matters");
            migrationBuilder.DropColumn(name: "IsArchived", table: "Clients");
        }
    }
}
