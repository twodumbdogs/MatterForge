using MatterForge.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MatterForge.Migrations;

[DbContext(typeof(MatterForgeDbContext))]
[Migration("20260618233000_AddWorkflowNotificationSteps")]
public partial class AddWorkflowNotificationSteps : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "StepType",
            table: "WorkflowSteps",
            type: "nvarchar(40)",
            maxLength: 40,
            nullable: false,
            defaultValue: "Approval");

        migrationBuilder.AddColumn<string>(
            name: "NotificationSubject",
            table: "WorkflowSteps",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "NotificationBody",
            table: "WorkflowSteps",
            type: "nvarchar(max)",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "NotificationRecipients",
            table: "WorkflowSteps",
            type: "nvarchar(1000)",
            maxLength: 1000,
            nullable: false,
            defaultValue: "");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "StepType",
            table: "WorkflowSteps");

        migrationBuilder.DropColumn(
            name: "NotificationSubject",
            table: "WorkflowSteps");

        migrationBuilder.DropColumn(
            name: "NotificationBody",
            table: "WorkflowSteps");

        migrationBuilder.DropColumn(
            name: "NotificationRecipients",
            table: "WorkflowSteps");
    }
}
