using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CMIForge.Migrations
{
    /// <inheritdoc />
    public partial class AddSubmissionNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SubmissionNumber",
                table: "FormSubmissions",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                """
                WITH NumberedSubmissions AS
                (
                    SELECT Id, ROW_NUMBER() OVER (ORDER BY SubmittedAt, Id) AS SubmissionNumber
                    FROM FormSubmissions
                )
                UPDATE s
                SET SubmissionNumber = n.SubmissionNumber
                FROM FormSubmissions s
                INNER JOIN NumberedSubmissions n ON n.Id = s.Id;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "SubmissionNumber",
                table: "FormSubmissions",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FormSubmissions_SubmissionNumber",
                table: "FormSubmissions",
                column: "SubmissionNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FormSubmissions_SubmissionNumber",
                table: "FormSubmissions");

            migrationBuilder.DropColumn(
                name: "SubmissionNumber",
                table: "FormSubmissions");
        }
    }
}
