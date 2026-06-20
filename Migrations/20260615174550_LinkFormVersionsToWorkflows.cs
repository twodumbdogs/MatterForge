using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CMIForge.Migrations
{
    /// <inheritdoc />
    public partial class LinkFormVersionsToWorkflows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowDefinitionId",
                table: "FormVersions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FormVersions_WorkflowDefinitionId",
                table: "FormVersions",
                column: "WorkflowDefinitionId");

            migrationBuilder.AddForeignKey(
                name: "FK_FormVersions_WorkflowDefinitions_WorkflowDefinitionId",
                table: "FormVersions",
                column: "WorkflowDefinitionId",
                principalTable: "WorkflowDefinitions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FormVersions_WorkflowDefinitions_WorkflowDefinitionId",
                table: "FormVersions");

            migrationBuilder.DropIndex(
                name: "IX_FormVersions_WorkflowDefinitionId",
                table: "FormVersions");

            migrationBuilder.DropColumn(
                name: "WorkflowDefinitionId",
                table: "FormVersions");
        }
    }
}
