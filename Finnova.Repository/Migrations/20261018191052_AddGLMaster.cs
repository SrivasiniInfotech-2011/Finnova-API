using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finnova.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddGLMaster : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "company_posting_states",
                columns: table => new
                {
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JvPostingEnabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_posting_states", x => x.CompanyId);
                });

            migrationBuilder.CreateTable(
                name: "gl_account_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GLAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    ProposedPayload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MakerId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    State = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gl_account_versions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "gl_accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GLCode = table.Column<string>(type: "nvarchar(9)", maxLength: 9, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    ParentGLId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AccountNature = table.Column<int>(type: "int", nullable: false),
                    NormalBalance = table.Column<int>(type: "int", nullable: false),
                    GLType = table.Column<int>(type: "int", nullable: false),
                    SubLedgerType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TaxType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsPostable = table.Column<bool>(type: "bit", nullable: false),
                    CurrencyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AllowManualJV = table.Column<bool>(type: "bit", nullable: false),
                    CostCentreMandatory = table.Column<bool>(type: "bit", nullable: false),
                    ProfitCentreMandatory = table.Column<bool>(type: "bit", nullable: false),
                    ReportingLine = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    MakerId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CheckerId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AuthorizedVersionNumber = table.Column<int>(type: "int", nullable: false),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gl_accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "gl_audit_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GLAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    OldValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValues = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ChangedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ChangedByRole = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gl_audit_entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "gl_system_account_mappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SystemGLType = table.Column<int>(type: "int", nullable: false),
                    GLAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gl_system_account_mappings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "gl_branch_applicabilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GLAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gl_branch_applicabilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_gl_branch_applicabilities_gl_accounts_GLAccountId",
                        column: x => x.GLAccountId,
                        principalTable: "gl_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_gl_account_versions_GLAccountId",
                table: "gl_account_versions",
                column: "GLAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_gl_account_versions_GLAccountId_State",
                table: "gl_account_versions",
                columns: new[] { "GLAccountId", "State" },
                unique: true,
                filter: "[State] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_gl_accounts_CompanyId_GLCode",
                table: "gl_accounts",
                columns: new[] { "CompanyId", "GLCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_gl_accounts_CompanyId_Name",
                table: "gl_accounts",
                columns: new[] { "CompanyId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_gl_accounts_ParentGLId",
                table: "gl_accounts",
                column: "ParentGLId");

            migrationBuilder.CreateIndex(
                name: "IX_gl_audit_entries_ChangedAtUtc",
                table: "gl_audit_entries",
                column: "ChangedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_gl_audit_entries_GLAccountId",
                table: "gl_audit_entries",
                column: "GLAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_gl_branch_applicabilities_GLAccountId_BranchId",
                table: "gl_branch_applicabilities",
                columns: new[] { "GLAccountId", "BranchId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_gl_system_account_mappings_CompanyId_SystemGLType",
                table: "gl_system_account_mappings",
                columns: new[] { "CompanyId", "SystemGLType" },
                unique: true,
                filter: "[IsActive] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "company_posting_states");

            migrationBuilder.DropTable(
                name: "gl_account_versions");

            migrationBuilder.DropTable(
                name: "gl_audit_entries");

            migrationBuilder.DropTable(
                name: "gl_branch_applicabilities");

            migrationBuilder.DropTable(
                name: "gl_system_account_mappings");

            migrationBuilder.DropTable(
                name: "gl_accounts");
        }
    }
}
