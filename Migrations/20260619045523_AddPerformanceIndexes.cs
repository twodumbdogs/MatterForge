using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CMIForge.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Matters_ClientId",
                table: "Matters");

            migrationBuilder.DropIndex(
                name: "IX_Matters_ResponsibleUserId",
                table: "Matters");

            migrationBuilder.DropIndex(
                name: "IX_FormSubmissions_ClientId",
                table: "FormSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_FormSubmissions_MatterId",
                table: "FormSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_FormSubmissions_SubmitterUserId",
                table: "FormSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_Conflicts_FormSubmissionId",
                table: "Conflicts");

            migrationBuilder.DropIndex(
                name: "IX_Conflicts_MatterId",
                table: "Conflicts");

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionWorkflowTasks_AssignedTeamId_Status_CreatedAt",
                table: "SubmissionWorkflowTasks",
                columns: new[] { "AssignedTeamId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionWorkflowTasks_AssignedUserId_Status_CreatedAt",
                table: "SubmissionWorkflowTasks",
                columns: new[] { "AssignedUserId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionWorkflowTasks_Status_CreatedAt",
                table: "SubmissionWorkflowTasks",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Parties_Name",
                table: "Parties",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Matters_ClientId_MatterNumber",
                table: "Matters",
                columns: new[] { "ClientId", "MatterNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Matters_Name",
                table: "Matters",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Matters_ResponsibleUserId_MatterNumber",
                table: "Matters",
                columns: new[] { "ResponsibleUserId", "MatterNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_FormSubmissions_ClientId_SubmissionNumber",
                table: "FormSubmissions",
                columns: new[] { "ClientId", "SubmissionNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_FormSubmissions_MatterId_SubmissionNumber",
                table: "FormSubmissions",
                columns: new[] { "MatterId", "SubmissionNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_FormSubmissions_Status_SubmissionNumber",
                table: "FormSubmissions",
                columns: new[] { "Status", "SubmissionNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_FormSubmissions_SubmittedAt",
                table: "FormSubmissions",
                column: "SubmittedAt");

            migrationBuilder.CreateIndex(
                name: "IX_FormSubmissions_SubmitterUserId_SubmissionNumber",
                table: "FormSubmissions",
                columns: new[] { "SubmitterUserId", "SubmissionNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Conflicts_FormSubmissionId_SearchNumber",
                table: "Conflicts",
                columns: new[] { "FormSubmissionId", "SearchNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Conflicts_MatterId_SearchNumber",
                table: "Conflicts",
                columns: new[] { "MatterId", "SearchNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Conflicts_ReviewerDecision_CreatedAt",
                table: "Conflicts",
                columns: new[] { "ReviewerDecision", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Conflicts_Status_CreatedAt",
                table: "Conflicts",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Clients_IsArchived_Name",
                table: "Clients",
                columns: new[] { "IsArchived", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_Clients_Name",
                table: "Clients",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SubmissionWorkflowTasks_AssignedTeamId_Status_CreatedAt",
                table: "SubmissionWorkflowTasks");

            migrationBuilder.DropIndex(
                name: "IX_SubmissionWorkflowTasks_AssignedUserId_Status_CreatedAt",
                table: "SubmissionWorkflowTasks");

            migrationBuilder.DropIndex(
                name: "IX_SubmissionWorkflowTasks_Status_CreatedAt",
                table: "SubmissionWorkflowTasks");

            migrationBuilder.DropIndex(
                name: "IX_Parties_Name",
                table: "Parties");

            migrationBuilder.DropIndex(
                name: "IX_Matters_ClientId_MatterNumber",
                table: "Matters");

            migrationBuilder.DropIndex(
                name: "IX_Matters_Name",
                table: "Matters");

            migrationBuilder.DropIndex(
                name: "IX_Matters_ResponsibleUserId_MatterNumber",
                table: "Matters");

            migrationBuilder.DropIndex(
                name: "IX_FormSubmissions_ClientId_SubmissionNumber",
                table: "FormSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_FormSubmissions_MatterId_SubmissionNumber",
                table: "FormSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_FormSubmissions_Status_SubmissionNumber",
                table: "FormSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_FormSubmissions_SubmittedAt",
                table: "FormSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_FormSubmissions_SubmitterUserId_SubmissionNumber",
                table: "FormSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_Conflicts_FormSubmissionId_SearchNumber",
                table: "Conflicts");

            migrationBuilder.DropIndex(
                name: "IX_Conflicts_MatterId_SearchNumber",
                table: "Conflicts");

            migrationBuilder.DropIndex(
                name: "IX_Conflicts_ReviewerDecision_CreatedAt",
                table: "Conflicts");

            migrationBuilder.DropIndex(
                name: "IX_Conflicts_Status_CreatedAt",
                table: "Conflicts");

            migrationBuilder.DropIndex(
                name: "IX_Clients_IsArchived_Name",
                table: "Clients");

            migrationBuilder.DropIndex(
                name: "IX_Clients_Name",
                table: "Clients");

            migrationBuilder.CreateIndex(
                name: "IX_Matters_ClientId",
                table: "Matters",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Matters_ResponsibleUserId",
                table: "Matters",
                column: "ResponsibleUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FormSubmissions_ClientId",
                table: "FormSubmissions",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_FormSubmissions_MatterId",
                table: "FormSubmissions",
                column: "MatterId");

            migrationBuilder.CreateIndex(
                name: "IX_FormSubmissions_SubmitterUserId",
                table: "FormSubmissions",
                column: "SubmitterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Conflicts_FormSubmissionId",
                table: "Conflicts",
                column: "FormSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_Conflicts_MatterId",
                table: "Conflicts",
                column: "MatterId");
        }
    }
}
