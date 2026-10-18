using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class GLAccountVersionConfiguration : IEntityTypeConfiguration<GLAccountVersion>
{
    public void Configure(EntityTypeBuilder<GLAccountVersion> builder)
    {
        builder.ToTable("gl_account_versions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.GLAccountId).IsRequired();
        builder.Property(x => x.VersionNumber).IsRequired();
        builder.Property(x => x.ProposedPayload).IsRequired();
        builder.Property(x => x.MakerId).IsRequired().HasMaxLength(100);
        builder.Property(x => x.SubmittedAtUtc).IsRequired();
        builder.Property(x => x.State).IsRequired();
        builder.HasIndex(x => x.GLAccountId);
        // At most one Pending version per GL account (R4.4).
        builder.HasIndex(x => new { x.GLAccountId, x.State })
               .IsUnique()
               .HasFilter("[State] = 0");   // GLVersionState.Pending = 0
    }
}
