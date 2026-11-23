using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finnova.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiptProcessing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "appropriation_audit_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceiptAppropriationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DueAllocationTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AdvanceAccountAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ResidualAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RuleVersion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_appropriation_audit_entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "appropriation_rule_audit_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineOfBusinessId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Operation = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NormalizedRequestHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_appropriation_rule_audit_entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "appropriation_rule_idempotency",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Operation = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    NormalizedRequestHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_appropriation_rule_idempotency", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "appropriation_rule_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineOfBusinessId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Direction = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FullDue = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NormalizedRequestHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActivatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeactivatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_appropriation_rule_versions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FaBankAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BankName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BankBranch = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MicrCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GlCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SlCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaBankAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "receipt_appropriations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReceiptAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DueAllocationTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AdvanceAccountAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ResidualAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineOfBusinessId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RuleVersion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RuleDirection = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    FullDue = table.Column<bool>(type: "bit", nullable: true),
                    ConfirmedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_receipt_appropriations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReceiptAuditEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DocNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SequenceNumber = table.Column<long>(type: "bigint", nullable: true),
                    Actor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChangeSummary = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiptAuditEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Receipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SequenceNumber = table.Column<long>(type: "bigint", nullable: true),
                    EntryMode = table.Column<int>(type: "int", nullable: false),
                    ReceiptType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DocDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ValueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    LineOfBusinessId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineOfBusinessName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BranchName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MarketingOfficerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MarketingOfficerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Mode = table.Column<int>(type: "int", nullable: false),
                    ReceiptFrom = table.Column<int>(type: "int", nullable: false),
                    GlBased = table.Column<bool>(type: "bit", nullable: false),
                    TrRefNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DocAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AmountInWords = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AuthorizedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuthorizedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Receipts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "appropriation_rule_details",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    DueType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CashFlowDueTypeCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Percentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_appropriation_rule_details", x => x.Id);
                    table.ForeignKey(
                        name: "FK_appropriation_rule_details_appropriation_rule_versions_RuleVersionId",
                        column: x => x.RuleVersionId,
                        principalTable: "appropriation_rule_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "advance_account_allocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceiptAppropriationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdvanceAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_advance_account_allocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_advance_account_allocations_receipt_appropriations_ReceiptAppropriationId",
                        column: x => x.ReceiptAppropriationId,
                        principalTable: "receipt_appropriations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "receipt_allocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceiptAppropriationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PendingDueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CashFlowDueType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AllocatedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OutstandingAmountAtConfirm = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RuleDetailId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RuleOrder = table.Column<int>(type: "int", nullable: true),
                    TargetAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    DueRowVersionAtConfirm = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_receipt_allocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_receipt_allocations_receipt_appropriations_ReceiptAppropriationId",
                        column: x => x.ReceiptAppropriationId,
                        principalTable: "receipt_appropriations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReceiptAddLessRows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineNo = table.Column<int>(type: "int", nullable: false),
                    LoanAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VehicleOrMachineNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    AccountDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GlAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GlAccountCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SlAccountCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiptAddLessRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceiptAddLessRows_Receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalTable: "Receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReceiptBankTransferDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FaBankAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BankName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AccountNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MicrCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BankBranch = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GlCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SlCode = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiptBankTransferDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceiptBankTransferDetails_Receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalTable: "Receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReceiptCashDenominations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    FaceValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiptCashDenominations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceiptCashDenominations_Receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalTable: "Receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReceiptChequeDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstrumentNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InstrumentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DraweeBankId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DraweeBankCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DraweeBankName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DraweeBranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DraweePlaceName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PaymentGatewayRefNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AckNo = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiptChequeDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceiptChequeDetails_Receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalTable: "Receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReceiptLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineNo = table.Column<int>(type: "int", nullable: false),
                    LoanAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PrimeAccountNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubAccountNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AccountDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ComponentCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreditGlAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreditGlCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SlAccountCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiptLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceiptLines_Receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalTable: "Receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_advance_account_allocations_ReceiptAppropriationId",
                table: "advance_account_allocations",
                column: "ReceiptAppropriationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_appropriation_audit_entries_ClientRequestId",
                table: "appropriation_audit_entries",
                column: "ClientRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_appropriation_audit_entries_ReceiptAppropriationId",
                table: "appropriation_audit_entries",
                column: "ReceiptAppropriationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_appropriation_rule_audit_entries_RuleVersionId",
                table: "appropriation_rule_audit_entries",
                column: "RuleVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_appropriation_rule_details_RuleVersionId_DueType",
                table: "appropriation_rule_details",
                columns: new[] { "RuleVersionId", "DueType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_appropriation_rule_details_RuleVersionId_SequenceNumber",
                table: "appropriation_rule_details",
                columns: new[] { "RuleVersionId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_appropriation_rule_idempotency_IdempotencyKey",
                table: "appropriation_rule_idempotency",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_appropriation_rule_versions_CompanyId_LineOfBusinessId_IsActive",
                table: "appropriation_rule_versions",
                columns: new[] { "CompanyId", "LineOfBusinessId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_appropriation_rule_versions_IdempotencyKey",
                table: "appropriation_rule_versions",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_receipt_allocations_ReceiptAppropriationId_PendingDueId",
                table: "receipt_allocations",
                columns: new[] { "ReceiptAppropriationId", "PendingDueId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_receipt_appropriations_ClientRequestId",
                table: "receipt_appropriations",
                column: "ClientRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_receipt_appropriations_ReceiptId",
                table: "receipt_appropriations",
                column: "ReceiptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptAddLessRows_ReceiptId",
                table: "ReceiptAddLessRows",
                column: "ReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptBankTransferDetails_ReceiptId",
                table: "ReceiptBankTransferDetails",
                column: "ReceiptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptCashDenominations_ReceiptId",
                table: "ReceiptCashDenominations",
                column: "ReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptChequeDetails_ReceiptId",
                table: "ReceiptChequeDetails",
                column: "ReceiptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptLines_ReceiptId",
                table: "ReceiptLines",
                column: "ReceiptId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "advance_account_allocations");

            migrationBuilder.DropTable(
                name: "appropriation_audit_entries");

            migrationBuilder.DropTable(
                name: "appropriation_rule_audit_entries");

            migrationBuilder.DropTable(
                name: "appropriation_rule_details");

            migrationBuilder.DropTable(
                name: "appropriation_rule_idempotency");

            migrationBuilder.DropTable(
                name: "FaBankAccounts");

            migrationBuilder.DropTable(
                name: "receipt_allocations");

            migrationBuilder.DropTable(
                name: "ReceiptAddLessRows");

            migrationBuilder.DropTable(
                name: "ReceiptAuditEntries");

            migrationBuilder.DropTable(
                name: "ReceiptBankTransferDetails");

            migrationBuilder.DropTable(
                name: "ReceiptCashDenominations");

            migrationBuilder.DropTable(
                name: "ReceiptChequeDetails");

            migrationBuilder.DropTable(
                name: "ReceiptLines");

            migrationBuilder.DropTable(
                name: "appropriation_rule_versions");

            migrationBuilder.DropTable(
                name: "receipt_appropriations");

            migrationBuilder.DropTable(
                name: "Receipts");
        }
    }
}
