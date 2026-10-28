using Finnova.Models.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finnova.Repository.Configuration;

public sealed class BankAccountConfiguration : IEntityTypeConfiguration<BankAccount>
{
    public void Configure(EntityTypeBuilder<BankAccount> b)
    {
        b.ToTable("CustomerBankAccounts"); b.HasKey(x => x.Id); b.Property(x => x.AccountNumber).HasMaxLength(16).IsRequired();
        b.Property(x => x.NormalizedDuplicateKey).HasMaxLength(400).IsRequired(); b.Property(x => x.AccountTypeCode).HasMaxLength(50);
        b.Property(x => x.BankCode).HasMaxLength(50); b.Property(x => x.MICRCode).HasMaxLength(10); b.Property(x => x.Address).HasMaxLength(300);
        b.Property(x => x.ContractNumber).HasMaxLength(15); b.Property(x => x.SubContractNumber).HasMaxLength(15);
        b.HasIndex(x => new { x.CustomerId, x.NormalizedDuplicateKey }).IsUnique(); b.HasIndex(x => x.CustomerId);
        b.HasIndex(x => x.CustomerId).HasFilter("[IsDefault] = 1").IsUnique();
    }
}
