using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MatterForge.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSystemIdAndUserPages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SystemId",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                """
                WITH NumberedUsers AS
                (
                    SELECT Id, ROW_NUMBER() OVER (ORDER BY CreatedAt, DisplayName, Id) AS SystemId
                    FROM Users
                )
                UPDATE u
                SET SystemId = n.SystemId
                FROM Users u
                INNER JOIN NumberedUsers n ON n.Id = u.Id;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "SystemId",
                table: "Users",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_SystemId",
                table: "Users",
                column: "SystemId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_SystemId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SystemId",
                table: "Users");
        }
    }
}
