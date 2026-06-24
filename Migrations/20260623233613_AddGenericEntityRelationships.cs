using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CMIForge.Migrations
{
    /// <inheritdoc />
    public partial class AddGenericEntityRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RelationshipTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Key = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RelationshipTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EntityRelationships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromEntityType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    FromEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToEntityType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ToEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RelationshipTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntityRelationships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntityRelationships_RelationshipTypes_RelationshipTypeId",
                        column: x => x.RelationshipTypeId,
                        principalTable: "RelationshipTypes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_EntityRelationships_FromEntityType_FromEntityId",
                table: "EntityRelationships",
                columns: new[] { "FromEntityType", "FromEntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_EntityRelationships_FromEntityType_FromEntityId_ToEntityType_ToEntityId_RelationshipTypeId",
                table: "EntityRelationships",
                columns: new[] { "FromEntityType", "FromEntityId", "ToEntityType", "ToEntityId", "RelationshipTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EntityRelationships_RelationshipTypeId",
                table: "EntityRelationships",
                column: "RelationshipTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_EntityRelationships_ToEntityType_ToEntityId",
                table: "EntityRelationships",
                columns: new[] { "ToEntityType", "ToEntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_RelationshipTypes_IsActive_Scope_Name",
                table: "RelationshipTypes",
                columns: new[] { "IsActive", "Scope", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_RelationshipTypes_Scope_Key",
                table: "RelationshipTypes",
                columns: new[] { "Scope", "Key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EntityRelationships");

            migrationBuilder.DropTable(
                name: "RelationshipTypes");
        }
    }
}
