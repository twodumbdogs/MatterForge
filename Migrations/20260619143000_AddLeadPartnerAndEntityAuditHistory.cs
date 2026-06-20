using System;
using CMIForge.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CMIForge.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(CMIForgeDbContext))]
    [Migration("20260619143000_AddLeadPartnerAndEntityAuditHistory")]
    public partial class AddLeadPartnerAndEntityAuditHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LeadPartnerId",
                table: "Matters",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LeadPartnerId",
                table: "FormSubmissions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Matters_LeadPartnerId_MatterNumber",
                table: "Matters",
                columns: new[] { "LeadPartnerId", "MatterNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_FormSubmissions_LeadPartnerId_SubmissionNumber",
                table: "FormSubmissions",
                columns: new[] { "LeadPartnerId", "SubmissionNumber" });

            migrationBuilder.AddForeignKey(
                name: "FK_FormSubmissions_Users_LeadPartnerId",
                table: "FormSubmissions",
                column: "LeadPartnerId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Matters_Users_LeadPartnerId",
                table: "Matters",
                column: "LeadPartnerId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FormSubmissions_Users_LeadPartnerId",
                table: "FormSubmissions");

            migrationBuilder.DropForeignKey(
                name: "FK_Matters_Users_LeadPartnerId",
                table: "Matters");

            migrationBuilder.DropIndex(
                name: "IX_Matters_LeadPartnerId_MatterNumber",
                table: "Matters");

            migrationBuilder.DropIndex(
                name: "IX_FormSubmissions_LeadPartnerId_SubmissionNumber",
                table: "FormSubmissions");

            migrationBuilder.DropColumn(
                name: "LeadPartnerId",
                table: "Matters");

            migrationBuilder.DropColumn(
                name: "LeadPartnerId",
                table: "FormSubmissions");
        }
    }
}


