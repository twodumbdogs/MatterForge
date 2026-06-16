using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MatterForge.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowOutcomesRoutingAndEntraAuth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConditionFieldKey",
                table: "WorkflowSteps",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ConditionOperator",
                table: "WorkflowSteps",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Always");

            migrationBuilder.AddColumn<string>(
                name: "ConditionValue",
                table: "WorkflowSteps",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OutcomesJson",
                table: "WorkflowSteps",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConditionFieldKey",
                table: "WorkflowSteps");

            migrationBuilder.DropColumn(
                name: "ConditionOperator",
                table: "WorkflowSteps");

            migrationBuilder.DropColumn(
                name: "ConditionValue",
                table: "WorkflowSteps");

            migrationBuilder.DropColumn(
                name: "OutcomesJson",
                table: "WorkflowSteps");
        }
    }
}
