using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CMIForge.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailOutboxAndGraphNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmailOutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    MailboxAddress = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    FromEmail = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    FromName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    ReplyToEmail = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    ToRecipients = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsBodyHtml = table.Column<bool>(type: "bit", nullable: false),
                    FormSubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmissionWorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WorkflowStepId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastAttemptAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SentAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailOutboxMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailOutboxMessages_FormSubmissions_FormSubmissionId",
                        column: x => x.FormSubmissionId,
                        principalTable: "FormSubmissions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EmailOutboxMessages_SubmissionWorkflowInstances_SubmissionWorkflowInstanceId",
                        column: x => x.SubmissionWorkflowInstanceId,
                        principalTable: "SubmissionWorkflowInstances",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EmailOutboxMessages_WorkflowSteps_WorkflowStepId",
                        column: x => x.WorkflowStepId,
                        principalTable: "WorkflowSteps",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailOutboxMessages_FormSubmissionId",
                table: "EmailOutboxMessages",
                column: "FormSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailOutboxMessages_Status_NextAttemptAt_CreatedAt",
                table: "EmailOutboxMessages",
                columns: new[] { "Status", "NextAttemptAt", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailOutboxMessages_SubmissionWorkflowInstanceId",
                table: "EmailOutboxMessages",
                column: "SubmissionWorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailOutboxMessages_WorkflowStepId",
                table: "EmailOutboxMessages",
                column: "WorkflowStepId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailOutboxMessages");
        }
    }
}
