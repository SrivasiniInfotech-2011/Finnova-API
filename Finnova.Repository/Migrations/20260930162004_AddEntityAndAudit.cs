using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finnova.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddEntityAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "entities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EntityType = table.Column<int>(type: "int", nullable: false),
                    RegistrationIdentifier = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ContactPerson = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    AddressLine = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Attributes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "entity_audit_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    OldValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValues = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ChangedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entity_audit_entries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_entities_EntityType_Code",
                table: "entities",
                columns: new[] { "EntityType", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_entities_Name",
                table: "entities",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_entity_audit_entries_EntityId_ChangedAtUtc",
                table: "entity_audit_entries",
                columns: new[] { "EntityId", "ChangedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "entities");

            migrationBuilder.DropTable(
                name: "entity_audit_entries");
        }
    }
}
