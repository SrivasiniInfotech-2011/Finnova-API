using Finnova.Models.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finnova.Repository.Configuration;

public sealed class FactoringSubLimitConfiguration : IEntityTypeConfiguration<FactoringSubLimit>
{
    public void Configure(EntityTypeBuilder<FactoringSubLimit> b)
    {
        b.ToTable("CustomerFactoringSubLimits"); b.HasKey(x => x.Id); b.Property(x => x.FactoringTypeCode).HasMaxLength(50);
        b.Property(x => x.Covenant).HasMaxLength(500); b.Property(x => x.CovenantClause).HasMaxLength(1000);
        b.HasIndex(x => x.CustomerId);
    }
}
