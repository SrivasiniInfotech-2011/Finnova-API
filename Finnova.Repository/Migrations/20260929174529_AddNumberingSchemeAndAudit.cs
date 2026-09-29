using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finnova.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddNumberingSchemeAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "numbering_scheme_audit_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SchemeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    OldValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValues = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ChangedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_numbering_scheme_audit_entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "numbering_schemes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FormatTemplate = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Prefix = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Suffix = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SeqStart = table.Column<long>(type: "bigint", nullable: false),
                    SeqIncrement = table.Column<int>(type: "int", nullable: false),
                    SeqPadding = table.Column<int>(type: "int", nullable: false),
                    ResetRule = table.Column<int>(type: "int", nullable: false),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    ScopeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CurrentValue = table.Column<long>(type: "bigint", nullable: false),
                    PeriodKey = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_numbering_schemes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_numbering_scheme_audit_entries_SchemeId_ChangedAtUtc",
                table: "numbering_scheme_audit_entries",
                columns: new[] { "SchemeId", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_numbering_schemes_Code",
                table: "numbering_schemes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_numbering_schemes_DocumentType_Scope_ScopeId",
                table: "numbering_schemes",
                columns: new[] { "DocumentType", "Scope", "ScopeId" },
                unique: true,
                filter: "[ScopeId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_numbering_schemes_Name",
                table: "numbering_schemes",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "numbering_scheme_audit_entries");

            migrationBuilder.DropTable(
                name: "numbering_schemes");
        }
    }
}
