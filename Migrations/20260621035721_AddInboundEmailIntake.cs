using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CMIForge.Migrations
{
    /// <inheritdoc />
    public partial class AddInboundEmailIntake : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InboundEmailMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    MailboxAddress = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    InboundAddress = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    GraphMessageId = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    InternetMessageId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ConversationId = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    FromEmail = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    FromName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    BodyPreview = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    BodyText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ParsedClientName = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    ParsedMatterName = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    ValidationMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FormSubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboundEmailMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InboundEmailMessages_FormSubmissions_FormSubmissionId",
                        column: x => x.FormSubmissionId,
                        principalTable: "FormSubmissions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "InboundEmailAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InboundEmailMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmissionAttachmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GraphAttachmentId = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    OcrStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    OcrText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OcrError = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboundEmailAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InboundEmailAttachments_InboundEmailMessages_InboundEmailMessageId",
                        column: x => x.InboundEmailMessageId,
                        principalTable: "InboundEmailMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InboundEmailAttachments_SubmissionAttachments_SubmissionAttachmentId",
                        column: x => x.SubmissionAttachmentId,
                        principalTable: "SubmissionAttachments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_InboundEmailAttachments_InboundEmailMessageId",
                table: "InboundEmailAttachments",
                column: "InboundEmailMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_InboundEmailAttachments_SubmissionAttachmentId",
                table: "InboundEmailAttachments",
                column: "SubmissionAttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_InboundEmailMessages_FormSubmissionId",
                table: "InboundEmailMessages",
                column: "FormSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_InboundEmailMessages_GraphMessageId",
                table: "InboundEmailMessages",
                column: "GraphMessageId",
                unique: true,
                filter: "[GraphMessageId] <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_InboundEmailMessages_InternetMessageId",
                table: "InboundEmailMessages",
                column: "InternetMessageId",
                unique: true,
                filter: "[InternetMessageId] <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_InboundEmailMessages_Status_ReceivedAt",
                table: "InboundEmailMessages",
                columns: new[] { "Status", "ReceivedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InboundEmailAttachments");

            migrationBuilder.DropTable(
                name: "InboundEmailMessages");
        }
    }
}
