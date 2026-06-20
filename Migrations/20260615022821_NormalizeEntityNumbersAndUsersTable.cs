using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CMIForge.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeEntityNumbersAndUsersTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Matters_CMIForgeUsers_ResponsibleUserId",
                table: "Matters");

            migrationBuilder.DropIndex(
                name: "IX_Matters_MatterCode",
                table: "Matters");

            migrationBuilder.DropIndex(
                name: "IX_Clients_ClientCode",
                table: "Clients");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CMIForgeUsers",
                table: "CMIForgeUsers");

            migrationBuilder.DropColumn(
                name: "MatterCode",
                table: "Matters");

            migrationBuilder.DropColumn(
                name: "ClientCode",
                table: "Clients");

            migrationBuilder.RenameTable(
                name: "CMIForgeUsers",
                newName: "Users");

            migrationBuilder.RenameIndex(
                name: "IX_CMIForgeUsers_Email",
                table: "Users",
                newName: "IX_Users_Email");

            migrationBuilder.AddColumn<int>(
                name: "MatterNumber",
                table: "Matters",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ClientNumber",
                table: "Clients",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                WITH NumberedClients AS (
                    SELECT Id, ROW_NUMBER() OVER (ORDER BY CreatedAt, Id) AS NewNumber
                    FROM Clients
                )
                UPDATE Clients
                SET ClientNumber = NumberedClients.NewNumber
                FROM Clients
                INNER JOIN NumberedClients ON Clients.Id = NumberedClients.Id;
                """);

            migrationBuilder.Sql("""
                WITH NumberedMatters AS (
                    SELECT Id, ROW_NUMBER() OVER (ORDER BY CreatedAt, Id) AS NewNumber
                    FROM Matters
                )
                UPDATE Matters
                SET MatterNumber = NumberedMatters.NewNumber
                FROM Matters
                INNER JOIN NumberedMatters ON Matters.Id = NumberedMatters.Id;
                """);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Users",
                table: "Users",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Matters_MatterNumber",
                table: "Matters",
                column: "MatterNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clients_ClientNumber",
                table: "Clients",
                column: "ClientNumber",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Matters_Users_ResponsibleUserId",
                table: "Matters",
                column: "ResponsibleUserId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Matters_Users_ResponsibleUserId",
                table: "Matters");

            migrationBuilder.DropIndex(
                name: "IX_Matters_MatterNumber",
                table: "Matters");

            migrationBuilder.DropIndex(
                name: "IX_Clients_ClientNumber",
                table: "Clients");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Users",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "MatterNumber",
                table: "Matters");

            migrationBuilder.DropColumn(
                name: "ClientNumber",
                table: "Clients");

            migrationBuilder.RenameTable(
                name: "Users",
                newName: "CMIForgeUsers");

            migrationBuilder.RenameIndex(
                name: "IX_Users_Email",
                table: "CMIForgeUsers",
                newName: "IX_CMIForgeUsers_Email");

            migrationBuilder.AddColumn<string>(
                name: "MatterCode",
                table: "Matters",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ClientCode",
                table: "Clients",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("UPDATE Clients SET ClientCode = RIGHT('00000000' + CAST(ClientNumber AS varchar(8)), 8);");
            migrationBuilder.Sql("UPDATE Matters SET MatterCode = RIGHT('00000000' + CAST(MatterNumber AS varchar(8)), 8);");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CMIForgeUsers",
                table: "CMIForgeUsers",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Matters_MatterCode",
                table: "Matters",
                column: "MatterCode");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_ClientCode",
                table: "Clients",
                column: "ClientCode");

            migrationBuilder.AddForeignKey(
                name: "FK_Matters_CMIForgeUsers_ResponsibleUserId",
                table: "Matters",
                column: "ResponsibleUserId",
                principalTable: "CMIForgeUsers",
                principalColumn: "Id");
        }
    }
}
