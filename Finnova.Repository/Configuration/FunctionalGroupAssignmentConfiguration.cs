using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class FunctionalGroupAssignmentConfiguration : IEntityTypeConfiguration<FunctionalGroupAssignment>
{
    public void Configure(EntityTypeBuilder<FunctionalGroupAssignment> b)
    {
        b.ToTable("functional_group_assignments");
        b.HasKey(x => x.Id);
        b.Property(x => x.FunctionalGroupId).IsRequired();
        b.Property(x => x.UserAccountId);
        b.Property(x => x.UserGroupId);

        b.HasIndex(x => new { x.FunctionalGroupId, x.UserAccountId });
        b.HasIndex(x => new { x.FunctionalGroupId, x.UserGroupId });

        // Exactly one owner target set (R6.5): a user OR a group, never both/neither.
        b.ToTable(t => t.HasCheckConstraint(
            "CK_functional_group_assignments_owner",
            "([UserAccountId] IS NOT NULL AND [UserGroupId] IS NULL) OR ([UserAccountId] IS NULL AND [UserGroupId] IS NOT NULL)"));
    }
}
