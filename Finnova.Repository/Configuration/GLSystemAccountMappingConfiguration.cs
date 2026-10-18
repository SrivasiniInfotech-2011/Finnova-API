using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class GLSystemAccountMappingConfiguration : IEntityTypeConfiguration<GLSystemAccountMapping>
{
    public void Configure(EntityTypeBuilder<GLSystemAccountMapping> builder)
    {
        builder.ToTable("gl_system_account_mappings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CompanyId).IsRequired();
        builder.Property(x => x.SystemGLType).IsRequired();
        builder.Property(x => x.GLAccountId).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();

        // At most one ACTIVE mapping per (company, system-GL type) — filtered unique index (R7.7).
        builder.HasIndex(x => new { x.CompanyId, x.SystemGLType })
               .IsUnique()
               .HasFilter("[IsActive] = 1");
    }
}
