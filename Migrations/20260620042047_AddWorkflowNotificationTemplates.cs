using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CMIForge.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowNotificationTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "NotificationTemplateId",
                table: "WorkflowSteps",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WorkflowNotificationTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Key = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowNotificationTemplates", x => x.Id);
                });

            var createdAt = new DateTimeOffset(2026, 6, 20, 4, 20, 47, TimeSpan.Zero);
            migrationBuilder.InsertData(
                table: "WorkflowNotificationTemplates",
                columns: new[] { "Id", "Name", "Key", "Description", "Subject", "Body", "IsActive", "CreatedAt", "UpdatedAt" },
                values: new object[,]
                {
                    {
                        new Guid("6e8b9d9b-9bd7-4a75-9ce1-4cb2b82ab5aa"),
                        "Workflow step update",
                        "workflow-step-update",
                        "General workflow notification for movement between intake/review steps.",
                        "Submission {{SubmissionNumber}} needs attention",
                        "{{WorkflowName}} reached {{StepName}} for submission {{SubmissionNumber}}.\n\nStatus: {{SubmissionStatus}}\nSubmitter: {{SubmitterName}}",
                        true,
                        createdAt,
                        createdAt
                    },
                    {
                        new Guid("0c011745-5481-4cdd-8ea6-b949e6868a8a"),
                        "Workflow approved",
                        "workflow-approved",
                        "Notification used when an intake or review workflow reaches approval.",
                        "Submission {{SubmissionNumber}} was approved",
                        "{{WorkflowName}} approved submission {{SubmissionNumber}}.\n\nStatus: {{SubmissionStatus}}",
                        true,
                        createdAt,
                        createdAt
                    },
                    {
                        new Guid("bb37ae4b-8341-4a65-8872-e9169d56f230"),
                        "Workflow returned",
                        "workflow-returned",
                        "Notification used when a workflow item needs revision or follow-up.",
                        "Submission {{SubmissionNumber}} was returned",
                        "{{WorkflowName}} returned submission {{SubmissionNumber}} at {{StepName}}.\n\nPlease review the workflow notes in CMIForge.",
                        true,
                        createdAt,
                        createdAt
                    }
                });

            migrationBuilder.Sql("""
                UPDATE WorkflowSteps
                SET NotificationTemplateId = '6e8b9d9b-9bd7-4a75-9ce1-4cb2b82ab5aa'
                WHERE StepType = N'Notification'
                  AND NotificationTemplateId IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSteps_NotificationTemplateId",
                table: "WorkflowSteps",
                column: "NotificationTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowNotificationTemplates_IsActive_Name",
                table: "WorkflowNotificationTemplates",
                columns: new[] { "IsActive", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowNotificationTemplates_Key",
                table: "WorkflowNotificationTemplates",
                column: "Key",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowSteps_WorkflowNotificationTemplates_NotificationTemplateId",
                table: "WorkflowSteps",
                column: "NotificationTemplateId",
                principalTable: "WorkflowNotificationTemplates",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowSteps_WorkflowNotificationTemplates_NotificationTemplateId",
                table: "WorkflowSteps");

            migrationBuilder.DropTable(
                name: "WorkflowNotificationTemplates");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowSteps_NotificationTemplateId",
                table: "WorkflowSteps");

            migrationBuilder.DropColumn(
                name: "NotificationTemplateId",
                table: "WorkflowSteps");
        }
    }
}
