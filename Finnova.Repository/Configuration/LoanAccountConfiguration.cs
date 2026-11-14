using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class LoanAccountConfiguration : IEntityTypeConfiguration<LoanAccount>
{
    public void Configure(EntityTypeBuilder<LoanAccount> builder)
    {
        builder.ToTable("loan_accounts");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.AccountNumber).IsRequired().HasMaxLength(100);   // DNC templates up to 100 (D1)
        builder.HasIndex(a => a.AccountNumber).IsUnique();
        builder.Property(a => a.AccountName).IsRequired().HasMaxLength(200);
        builder.Property(a => a.Type).HasConversion<string>().HasMaxLength(50);
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(50);
        builder.Property(a => a.Balance).HasPrecision(18, 4);
        builder.Property(a => a.Currency).IsRequired().HasMaxLength(3);
        builder.Property(a => a.BranchCode).HasMaxLength(20);

        // ---- Origination references and KYC ----
        builder.Property(a => a.ProductCode).HasMaxLength(50);
        builder.Property(a => a.SchemeCode).HasMaxLength(50);
        builder.Property(a => a.CustomerNameSnapshot).HasMaxLength(300);
        builder.Property(a => a.CreationNotes).HasMaxLength(1000);
        builder.Property(a => a.KycStatus).HasConversion<string>().HasMaxLength(30);
        builder.Property(a => a.KycSource).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.KycPanValidation).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.KycPanNameMatch).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.KycAadhaarValidation).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.KycAadhaarAddressMatch).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.KycProviderReference).HasMaxLength(100);
        builder.Property(a => a.RowVersion).IsRowVersion();

        // At most one Loan Account per Application (A6).
        builder.HasIndex(a => a.ApplicationId).IsUnique().HasFilter("[ApplicationId] IS NOT NULL");
        builder.HasIndex(a => a.CustomerId);
        builder.HasIndex(a => a.LineOfBusinessId);
        builder.HasIndex(a => a.BranchId);
        builder.HasIndex(a => a.ProductCode);
        builder.HasIndex(a => a.SchemeCode);
        builder.HasIndex(a => new { a.KycFlagged, a.KycStatus });
    }
}