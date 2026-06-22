using System;
using CMIForge.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CMIForge.Migrations
{
    [DbContext(typeof(CMIForgeDbContext))]
    [Migration("20260621125009_AddExternalInviteEmailTracking")]
    public partial class AddExternalInviteEmailTracking : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ExternalFormInviteId",
                table: "EmailOutboxMessages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailOutboxMessages_ExternalFormInviteId",
                table: "EmailOutboxMessages",
                column: "ExternalFormInviteId");

            migrationBuilder.AddForeignKey(
                name: "FK_EmailOutboxMessages_ExternalFormInvites_ExternalFormInviteId",
                table: "EmailOutboxMessages",
                column: "ExternalFormInviteId",
                principalTable: "ExternalFormInvites",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmailOutboxMessages_ExternalFormInvites_ExternalFormInviteId",
                table: "EmailOutboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_EmailOutboxMessages_ExternalFormInviteId",
                table: "EmailOutboxMessages");

            migrationBuilder.DropColumn(
                name: "ExternalFormInviteId",
                table: "EmailOutboxMessages");
        }
    }
}
