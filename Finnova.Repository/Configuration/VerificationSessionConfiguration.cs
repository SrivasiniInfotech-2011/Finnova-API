using Finnova.Models.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finnova.Repository.Configuration;

public class VerificationSessionConfiguration : IEntityTypeConfiguration<VerificationSession>
{
    public void Configure(EntityTypeBuilder<VerificationSession> b)
    {
        b.ToTable("verification_sessions");
        b.HasKey(x => x.Id);
        b.Property(x => x.CorrelationId).IsRequired().HasMaxLength(100);
        b.Property(x => x.ActorReference).IsRequired().HasMaxLength(200);
        b.Property(x => x.RequestedOperationsJson).IsRequired().HasMaxLength(1000);
        b.Property(x => x.CreatedUtc).IsRequired();
        b.HasIndex(x => x.CorrelationId);
    }
}
