using Finnova.Models.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finnova.Repository.Configuration;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        b.ToTable("Customers"); b.HasKey(x => x.Id);
        b.Property(x => x.CustomerCode).HasMaxLength(12).IsRequired();
        b.HasIndex(x => x.CustomerCode).IsUnique();
        b.Property(x => x.CustomerTypeCode).HasMaxLength(50).IsRequired();
        b.Property(x => x.GroupCode).HasMaxLength(4);
        b.Property(x => x.IndustryCode).HasMaxLength(3);
        b.Property(x => x.CustomerPostingGroupCode).HasMaxLength(50);
        b.Property(x => x.ConstitutionCode).HasMaxLength(50);
        b.Property(x => x.NationalityCode).HasMaxLength(10);
        b.Property(x => x.TitleCode).HasMaxLength(50);
        b.Property(x => x.MaritalStatusCode).HasMaxLength(50);
        b.Property(x => x.CustomerBusinessTypeCode).HasMaxLength(50);
        b.Property(x => x.CustomerBusinessSubTypeCode).HasMaxLength(50);
        b.Property(x => x.IndustrySectorCode).HasMaxLength(50);
        b.Property(x => x.SubSectorCode).HasMaxLength(50);
        foreach (var p in new[] { nameof(Customer.FirstName), nameof(Customer.SecondName), nameof(Customer.ThirdName),
            nameof(Customer.FourthName), nameof(Customer.FifthName), nameof(Customer.SixthName) }) b.Property<string?>(p).HasMaxLength(100);
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.Property(x => x.FinancialRequirement).HasMaxLength(240);
        b.Property(x => x.Accommodation).HasMaxLength(240);
        b.Property(x => x.RelationCode).HasMaxLength(50);
        b.Property(x => x.EmployerEconomicActivityCode).HasMaxLength(50);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        b.HasIndex(x => new { x.CompanyId, x.NationalityCode });
        // No GroupName, IndustryName, ConstitutionName, NationalityName, or GLName column.
    }
}
