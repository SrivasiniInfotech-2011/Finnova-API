using Finnova.Models.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finnova.Repository.Configuration;

public sealed class AppropriationRuleVersionConfiguration : IEntityTypeConfiguration<AppropriationRuleVersion>
{
    public void Configure(EntityTypeBuilder<AppropriationRuleVersion> b)
    {
        b.ToTable("appropriation_rule_versions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Direction).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.IdempotencyKey).HasMaxLength(100).IsRequired();
        b.Property(x => x.NormalizedRequestHash).HasMaxLength(128).IsRequired();
        b.Property(x => x.CreatedBy).HasMaxLength(200).IsRequired();
        b.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        b.HasIndex(x => new { x.CompanyId, x.LineOfBusinessId, x.IsActive });
        b.HasIndex(x => x.IdempotencyKey).IsUnique();
        b.HasMany(x => x.Details).WithOne(x => x.RuleVersion).HasForeignKey(x => x.RuleVersionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class AppropriationRuleDetailConfiguration : IEntityTypeConfiguration<AppropriationRuleDetail>
{
    public void Configure(EntityTypeBuilder<AppropriationRuleDetail> b)
    {
        b.ToTable("appropriation_rule_details");
        b.HasKey(x => x.Id);
        b.Property(x => x.DueType).HasConversion<string>().HasMaxLength(40).IsRequired();
        b.Property(x => x.CashFlowDueTypeCode).HasMaxLength(80).IsRequired();
        b.Property(x => x.Percentage).HasPrecision(5, 2).IsRequired();
        b.HasIndex(x => new { x.RuleVersionId, x.SequenceNumber }).IsUnique();
        b.HasIndex(x => new { x.RuleVersionId, x.DueType }).IsUnique();
    }
}

public sealed class AppropriationRuleAuditConfiguration : IEntityTypeConfiguration<AppropriationRuleAuditEntry>
{
    public void Configure(EntityTypeBuilder<AppropriationRuleAuditEntry> b)
    {
        b.ToTable("appropriation_rule_audit_entries");
        b.HasKey(x => x.Id);
        b.Property(x => x.Operation).HasConversion<string>().HasMaxLength(30).IsRequired();
        b.Property(x => x.Actor).HasMaxLength(200).IsRequired();
        b.Property(x => x.CorrelationId).HasMaxLength(100).IsRequired();
        b.Property(x => x.IdempotencyKey).HasMaxLength(100).IsRequired();
        b.Property(x => x.NormalizedRequestHash).HasMaxLength(128).IsRequired();
        b.HasIndex(x => x.RuleVersionId);
    }
}

public sealed class RuleMutationIdempotencyConfiguration : IEntityTypeConfiguration<RuleMutationIdempotency>
{
    public void Configure(EntityTypeBuilder<RuleMutationIdempotency> b)
    {
        b.ToTable("appropriation_rule_idempotency");
        b.HasKey(x => x.Id);
        b.Property(x => x.IdempotencyKey).HasMaxLength(100).IsRequired();
        b.Property(x => x.Operation).HasMaxLength(30).IsRequired();
        b.Property(x => x.NormalizedRequestHash).HasMaxLength(128).IsRequired();
        b.HasIndex(x => x.IdempotencyKey).IsUnique();
    }
}