using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.AccountNumber).IsRequired().HasMaxLength(20);
        builder.HasIndex(a => a.AccountNumber).IsUnique();
        builder.Property(a => a.AccountName).IsRequired().HasMaxLength(200);
        builder.Property(a => a.Type).HasConversion<string>().HasMaxLength(50);
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(50);
        builder.Property(a => a.Balance).HasPrecision(18, 4);
        builder.Property(a => a.Currency).IsRequired().HasMaxLength(3);
        builder.Property(a => a.BranchCode).HasMaxLength(20);
        builder.Property(a => a.IfscCode).HasMaxLength(11);
        builder.HasIndex(a => a.UserId);
        builder.HasIndex(a => a.OrganizationId);
    }
}
