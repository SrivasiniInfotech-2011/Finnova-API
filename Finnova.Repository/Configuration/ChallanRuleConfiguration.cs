using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class ChallanRuleConfiguration : IEntityTypeConfiguration<ChallanRule>
{
    public void Configure(EntityTypeBuilder<ChallanRule> builder)
    {
        builder.ToTable("challan_rules");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.DraweeBankId).IsRequired();
        builder.Property(x => x.RuleCode).IsRequired().HasMaxLength(50);
        builder.Property(x => x.FormatPattern).IsRequired().HasMaxLength(500);
        builder.Property(x => x.ValidationExpression).IsRequired().HasMaxLength(500);
        builder.Property(x => x.RoutingTarget).IsRequired().HasMaxLength(200);
        builder.Property(x => x.IsActive).IsRequired();

        // Rule Code unique within the owning bank (R6.4).
        builder.HasIndex(x => new { x.DraweeBankId, x.RuleCode }).IsUnique();
    }
}
