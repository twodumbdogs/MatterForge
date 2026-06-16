using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MatterForge.Migrations
{
    /// <inheritdoc />
    public partial class AddSubmitterUserToSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SubmitterUserId",
                table: "FormSubmissions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FormSubmissions_SubmitterUserId",
                table: "FormSubmissions",
                column: "SubmitterUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_FormSubmissions_Users_SubmitterUserId",
                table: "FormSubmissions",
                column: "SubmitterUserId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FormSubmissions_Users_SubmitterUserId",
                table: "FormSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_FormSubmissions_SubmitterUserId",
                table: "FormSubmissions");

            migrationBuilder.DropColumn(
                name: "SubmitterUserId",
                table: "FormSubmissions");
        }
    }
}
