using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finnova.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddCourtAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "court_audit_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourtId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    OldValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValues = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ChangedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_court_audit_entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "courts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CourtType = table.Column<int>(type: "int", nullable: false),
                    Jurisdiction = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_courts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_court_audit_entries_CourtId_ChangedAtUtc",
                table: "court_audit_entries",
                columns: new[] { "CourtId", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_courts_Code",
                table: "courts",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_courts_Name",
                table: "courts",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "court_audit_entries");

            migrationBuilder.DropTable(
                name: "courts");
        }
    }
}
