using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finnova.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddDraweeBankChallanRulesAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "challan_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DraweeBankId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FormatPattern = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ValidationExpression = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RoutingTarget = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_challan_rules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "drawee_bank_audit_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DraweeBankId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    BeforeSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AfterSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChangedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_drawee_bank_audit_entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "drawee_banks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BankCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BankName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_drawee_banks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "drawee_branches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DraweeBankId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlaceCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PlaceName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    PostalCode = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_drawee_branches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_drawee_branches_drawee_banks_DraweeBankId",
                        column: x => x.DraweeBankId,
                        principalTable: "drawee_banks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "restriction_details",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DraweeBankId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClearingDays = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_restriction_details", x => x.Id);
                    table.ForeignKey(
                        name: "FK_restriction_details_drawee_banks_DraweeBankId",
                        column: x => x.DraweeBankId,
                        principalTable: "drawee_banks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_challan_rules_DraweeBankId_RuleCode",
                table: "challan_rules",
                columns: new[] { "DraweeBankId", "RuleCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_drawee_bank_audit_entries_DraweeBankId_ChangedAtUtc",
                table: "drawee_bank_audit_entries",
                columns: new[] { "DraweeBankId", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_drawee_banks_BankCode",
                table: "drawee_banks",
                column: "BankCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_drawee_banks_BankName",
                table: "drawee_banks",
                column: "BankName");

            migrationBuilder.CreateIndex(
                name: "IX_drawee_branches_DraweeBankId_PlaceCode",
                table: "drawee_branches",
                columns: new[] { "DraweeBankId", "PlaceCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_restriction_details_DraweeBankId",
                table: "restriction_details",
                column: "DraweeBankId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "challan_rules");

            migrationBuilder.DropTable(
                name: "drawee_bank_audit_entries");

            migrationBuilder.DropTable(
                name: "drawee_branches");

            migrationBuilder.DropTable(
                name: "restriction_details");

            migrationBuilder.DropTable(
                name: "drawee_banks");
        }
    }
}
