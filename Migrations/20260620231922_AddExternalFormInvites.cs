using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CMIForge.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalFormInvites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExternalFormInvites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FormDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FormVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientContactId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecipientName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RecipientEmail = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    SenderUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeadPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TokenHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    OpenedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EmailQueuedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FormSubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalFormInvites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalFormInvites_Contacts_RecipientContactId",
                        column: x => x.RecipientContactId,
                        principalTable: "Contacts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ExternalFormInvites_FormDefinitions_FormDefinitionId",
                        column: x => x.FormDefinitionId,
                        principalTable: "FormDefinitions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ExternalFormInvites_FormSubmissions_FormSubmissionId",
                        column: x => x.FormSubmissionId,
                        principalTable: "FormSubmissions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ExternalFormInvites_FormVersions_FormVersionId",
                        column: x => x.FormVersionId,
                        principalTable: "FormVersions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ExternalFormInvites_Users_LeadPartnerId",
                        column: x => x.LeadPartnerId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ExternalFormInvites_Users_SenderUserId",
                        column: x => x.SenderUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalFormInvites_FormDefinitionId_CreatedAt",
                table: "ExternalFormInvites",
                columns: new[] { "FormDefinitionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalFormInvites_FormSubmissionId",
                table: "ExternalFormInvites",
                column: "FormSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalFormInvites_FormVersionId",
                table: "ExternalFormInvites",
                column: "FormVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalFormInvites_LeadPartnerId",
                table: "ExternalFormInvites",
                column: "LeadPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalFormInvites_RecipientContactId",
                table: "ExternalFormInvites",
                column: "RecipientContactId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalFormInvites_SenderUserId",
                table: "ExternalFormInvites",
                column: "SenderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalFormInvites_Status_ExpiresAt",
                table: "ExternalFormInvites",
                columns: new[] { "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalFormInvites_TokenHash",
                table: "ExternalFormInvites",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExternalFormInvites");
        }
    }
}
