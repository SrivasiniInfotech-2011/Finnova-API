using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finnova.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddNationalityAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "nationalities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nationalities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "nationality_audit_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NationalityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    OldName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NewName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ChangedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nationality_audit_entries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_nationalities_Code",
                table: "nationalities",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_nationalities_Name",
                table: "nationalities",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_nationality_audit_entries_NationalityId_ChangedAtUtc",
                table: "nationality_audit_entries",
                columns: new[] { "NationalityId", "ChangedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "nationalities");

            migrationBuilder.DropTable(
                name: "nationality_audit_entries");
        }
    }
}
