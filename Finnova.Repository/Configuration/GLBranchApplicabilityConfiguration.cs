using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class GLBranchApplicabilityConfiguration : IEntityTypeConfiguration<GLBranchApplicability>
{
    public void Configure(EntityTypeBuilder<GLBranchApplicability> builder)
    {
        builder.ToTable("gl_branch_applicabilities");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.GLAccountId).IsRequired();
        builder.Property(x => x.BranchId).IsRequired();
        builder.HasIndex(x => new { x.GLAccountId, x.BranchId }).IsUnique();
    }
}
