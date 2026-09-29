using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finnova.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationHierarchyAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "organizations",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(5)",
                oldMaxLength: 5);

            migrationBuilder.CreateTable(
                name: "organization_node_audit_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    OldName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    NewName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    OldParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NewParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ChangedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_node_audit_entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "organization_nodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_nodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_organization_nodes_organization_nodes_ParentId",
                        column: x => x.ParentId,
                        principalTable: "organization_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_organization_node_audit_entries_NodeId_ChangedAtUtc",
                table: "organization_node_audit_entries",
                columns: new[] { "NodeId", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_organization_nodes_Code",
                table: "organization_nodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organization_nodes_Name",
                table: "organization_nodes",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_organization_nodes_ParentId",
                table: "organization_nodes",
                column: "ParentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "organization_node_audit_entries");

            migrationBuilder.DropTable(
                name: "organization_nodes");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "organizations",
                type: "nvarchar(5)",
                maxLength: 5,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10);
        }
    }
}
