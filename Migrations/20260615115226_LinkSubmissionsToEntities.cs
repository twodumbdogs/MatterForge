using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CMIForge.Migrations
{
    /// <inheritdoc />
    public partial class LinkSubmissionsToEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClientId",
                table: "FormSubmissions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MatterId",
                table: "FormSubmissions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FormSubmissions_ClientId",
                table: "FormSubmissions",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_FormSubmissions_MatterId",
                table: "FormSubmissions",
                column: "MatterId");

            migrationBuilder.AddForeignKey(
                name: "FK_FormSubmissions_Clients_ClientId",
                table: "FormSubmissions",
                column: "ClientId",
                principalTable: "Clients",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FormSubmissions_Matters_MatterId",
                table: "FormSubmissions",
                column: "MatterId",
                principalTable: "Matters",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FormSubmissions_Clients_ClientId",
                table: "FormSubmissions");

            migrationBuilder.DropForeignKey(
                name: "FK_FormSubmissions_Matters_MatterId",
                table: "FormSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_FormSubmissions_ClientId",
                table: "FormSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_FormSubmissions_MatterId",
                table: "FormSubmissions");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "FormSubmissions");

            migrationBuilder.DropColumn(
                name: "MatterId",
                table: "FormSubmissions");
        }
    }
}
