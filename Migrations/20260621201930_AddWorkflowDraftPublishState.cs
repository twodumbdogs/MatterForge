using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CMIForge.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowDraftPublishState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPublished",
                table: "WorkflowDefinitions",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PublishedAt",
                table: "WorkflowDefinitions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitions_IsPublished_IsActive_FormDefinitionId",
                table: "WorkflowDefinitions",
                columns: new[] { "IsPublished", "IsActive", "FormDefinitionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkflowDefinitions_IsPublished_IsActive_FormDefinitionId",
                table: "WorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "IsPublished",
                table: "WorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "WorkflowDefinitions");
        }
    }
}
