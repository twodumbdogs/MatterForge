using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CMIForge.Migrations
{
    /// <inheritdoc />
    public partial class AddEntityNoteImpersonation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ImpersonatedUserId",
                table: "EntityNotes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EntityNotes_ImpersonatedUserId",
                table: "EntityNotes",
                column: "ImpersonatedUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_EntityNotes_Users_ImpersonatedUserId",
                table: "EntityNotes",
                column: "ImpersonatedUserId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EntityNotes_Users_ImpersonatedUserId",
                table: "EntityNotes");

            migrationBuilder.DropIndex(
                name: "IX_EntityNotes_ImpersonatedUserId",
                table: "EntityNotes");

            migrationBuilder.DropColumn(
                name: "ImpersonatedUserId",
                table: "EntityNotes");
        }
    }
}
