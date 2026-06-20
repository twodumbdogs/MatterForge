using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CMIForge.Migrations
{
    /// <inheritdoc />
    public partial class AddLegalAgreementAcceptances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LegalAgreementAcceptances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantProvisioningRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AcceptedByName = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    AcceptedByEmail = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    AgreementKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    AgreementVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    AgreementTitle = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    ProductVersion = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Accepted = table.Column<bool>(type: "bit", nullable: false),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UserAgent = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalAgreementAcceptances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LegalAgreementAcceptances_TenantProvisioningRequests_TenantProvisioningRequestId",
                        column: x => x.TenantProvisioningRequestId,
                        principalTable: "TenantProvisioningRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LegalAgreementAcceptances_AgreementKey_AgreementVersion",
                table: "LegalAgreementAcceptances",
                columns: new[] { "AgreementKey", "AgreementVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_LegalAgreementAcceptances_TenantProvisioningRequestId",
                table: "LegalAgreementAcceptances",
                column: "TenantProvisioningRequestId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LegalAgreementAcceptances");
        }
    }
}
