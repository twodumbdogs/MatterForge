using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CMIForge.Migrations
{
    /// <inheritdoc />
    public partial class AddUserFontScalePreference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FontScalePercent",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 100);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FontScalePercent",
                table: "Users");
        }
    }
}
