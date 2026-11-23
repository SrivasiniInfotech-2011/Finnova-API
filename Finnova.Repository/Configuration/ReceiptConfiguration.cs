using Finnova.Models.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finnova.Repository.Configuration;

public sealed class ReceiptConfiguration : IEntityTypeConfiguration<Receipt>
{
    public void Configure(EntityTypeBuilder<Receipt> b)
    {
        b.ToTable("receipts", t => t.HasCheckConstraint("CK_receipts_doc_amount", "[DocAmount] > 0"));
        b.HasKey(x => x.Id);
        b.Property(x => x.DocNo).IsRequired().HasMaxLength(100);
        b.Property(x => x.EntryMode).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.ReceiptType).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        b.Property(x => x.Mode).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.ReceiptFrom).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.LineOfBusinessName).IsRequired().HasMaxLength(200);
        b.Property(x => x.BranchCode).IsRequired().HasMaxLength(20);
        b.Property(x => x.BranchName).IsRequired().HasMaxLength(100);
        b.Property(x => x.MarketingOfficerName).IsRequired().HasMaxLength(50);
        b.Property(x => x.TrRefNo).HasMaxLength(30);
        b.Property(x => x.CustomerCode).IsRequired().HasMaxLength(20);
        b.Property(x => x.CustomerName).IsRequired().HasMaxLength(300);
        b.Property(x => x.CurrencyCode).IsRequired().HasMaxLength(3);
        b.Property(x => x.DocAmount).HasPrecision(18, 2);
        b.Property(x => x.AmountInWords).IsRequired().HasMaxLength(300);
        b.Property(x => x.AuthorizedBy).HasMaxLength(200);
        b.Property(x => x.CreatedBy).IsRequired().HasMaxLength(200);
        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasIndex(x => x.ClientRequestId).IsUnique();
        b.HasIndex(x => new { x.ReceiptType, x.DocNo }).IsUnique();
        b.HasIndex(x => new { x.BranchId, x.DocDate });
        b.HasIndex(x => x.CustomerCode);
        b.HasIndex(x => new { x.DocDate, x.CreatedAtUtc });

        b.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.ReceiptId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.AddLess).WithOne().HasForeignKey(x => x.ReceiptId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Denominations).WithOne().HasForeignKey(x => x.ReceiptId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Cheque).WithOne().HasForeignKey<ReceiptChequeDetail>(x => x.ReceiptId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.BankTransfer).WithOne().HasForeignKey<ReceiptBankTransferDetail>(x => x.ReceiptId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ReceiptLineConfiguration : IEntityTypeConfiguration<ReceiptLine>
{
    public void Configure(EntityTypeBuilder<ReceiptLine> b)
    {
        b.ToTable("receipt_lines");
        b.HasKey(x => x.Id);
        b.Property(x => x.PrimeAccountNo).IsRequired().HasMaxLength(100);
        b.Property(x => x.SubAccountNo).HasMaxLength(30);
        b.Property(x => x.AccountDescription).IsRequired().HasMaxLength(200);
        b.Property(x => x.ComponentCode).IsRequired().HasMaxLength(50);
        b.Property(x => x.CreditGlCode).IsRequired().HasMaxLength(9);
        b.Property(x => x.SlAccountCode).HasMaxLength(20);
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.HasIndex(x => new { x.ReceiptId, x.LineNo }).IsUnique();
        b.HasOne<LoanAccount>().WithMany().HasForeignKey(x => x.LoanAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ReceiptAddLessConfiguration : IEntityTypeConfiguration<ReceiptAddLess>
{
    public void Configure(EntityTypeBuilder<ReceiptAddLess> b)
    {
        b.ToTable("receipt_add_less");
        b.HasKey(x => x.Id);
        b.Property(x => x.AccountNumber).IsRequired().HasMaxLength(100);
        b.Property(x => x.VehicleOrMachineNo).HasMaxLength(50);
        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(10).IsRequired();
        b.Property(x => x.AccountDescription).IsRequired().HasMaxLength(200);
        b.Property(x => x.GlAccountCode).IsRequired().HasMaxLength(9);
        b.Property(x => x.SlAccountCode).HasMaxLength(20);
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.Remarks).HasMaxLength(200);
        b.HasIndex(x => new { x.ReceiptId, x.LineNo }).IsUnique();
        b.HasOne<LoanAccount>().WithMany().HasForeignKey(x => x.LoanAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ReceiptCashDenominationConfiguration : IEntityTypeConfiguration<ReceiptCashDenomination>
{
    public void Configure(EntityTypeBuilder<ReceiptCashDenomination> b)
    {
        b.ToTable("receipt_cash_denominations");
        b.HasKey(x => x.Id);
        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(10).IsRequired();
        b.Property(x => x.FaceValue).HasPrecision(9, 2);
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.HasIndex(x => new { x.ReceiptId, x.Kind, x.FaceValue }).IsUnique();
    }
}

public sealed class ReceiptChequeDetailConfiguration : IEntityTypeConfiguration<ReceiptChequeDetail>
{
    public void Configure(EntityTypeBuilder<ReceiptChequeDetail> b)
    {
        b.ToTable("receipt_cheque_details");
        b.HasKey(x => x.Id);
        b.Property(x => x.InstrumentNo).IsRequired().HasMaxLength(6).IsFixedLength();
        b.Property(x => x.DraweeBankCode).IsRequired().HasMaxLength(20);
        b.Property(x => x.DraweeBankName).IsRequired().HasMaxLength(150);
        b.Property(x => x.DraweePlaceName).IsRequired().HasMaxLength(150);
        b.Property(x => x.PaymentGatewayRefNo).HasMaxLength(50);
        b.Property(x => x.AckNo).HasMaxLength(50);
        b.HasIndex(x => x.ReceiptId).IsUnique();
    }
}

public sealed class ReceiptBankTransferDetailConfiguration : IEntityTypeConfiguration<ReceiptBankTransferDetail>
{
    public void Configure(EntityTypeBuilder<ReceiptBankTransferDetail> b)
    {
        b.ToTable("receipt_bank_transfer_details");
        b.HasKey(x => x.Id);
        b.Property(x => x.BankName).IsRequired().HasMaxLength(150);
        b.Property(x => x.AccountNo).IsRequired().HasMaxLength(34);
        b.Property(x => x.MicrCode).IsRequired().HasMaxLength(9);
        b.Property(x => x.BankBranch).IsRequired().HasMaxLength(150);
        b.Property(x => x.GlCode).IsRequired().HasMaxLength(9);
        b.Property(x => x.SlCode).HasMaxLength(20);
        b.HasIndex(x => x.ReceiptId).IsUnique();
    }
}

public sealed class ReceiptAuditEntryConfiguration : IEntityTypeConfiguration<ReceiptAuditEntry>
{
    public void Configure(EntityTypeBuilder<ReceiptAuditEntry> b)
    {
        b.ToTable("receipt_audit_entries");
        b.HasKey(x => x.Id);
        b.Property(x => x.Action).IsRequired().HasMaxLength(40);
        b.Property(x => x.DocNo).IsRequired().HasMaxLength(100);
        b.Property(x => x.Actor).IsRequired().HasMaxLength(200);
        b.Property(x => x.CorrelationId).IsRequired().HasMaxLength(64);
        b.Property(x => x.ChangeSummary).IsRequired().HasMaxLength(1000);
        // No FK: audit entries outlive the receipt and an unknown id reads as an empty list (R11.5).
        b.HasIndex(x => new { x.ReceiptId, x.OccurredAtUtc });
    }
}

public sealed class FaBankAccountConfiguration : IEntityTypeConfiguration<FaBankAccount>
{
    public void Configure(EntityTypeBuilder<FaBankAccount> b)
    {
        b.ToTable("fa_bank_accounts");
        b.HasKey(x => x.Id);
        b.Property(x => x.BankName).IsRequired().HasMaxLength(150);
        b.Property(x => x.BankBranch).IsRequired().HasMaxLength(150);
        b.Property(x => x.AccountNumber).IsRequired().HasMaxLength(34);
        b.Property(x => x.MicrCode).IsRequired().HasMaxLength(9);
        b.Property(x => x.GlCode).IsRequired().HasMaxLength(9);
        b.Property(x => x.SlCode).HasMaxLength(20);
        b.HasIndex(x => new { x.BankName, x.AccountNumber }).IsUnique();
    }
}