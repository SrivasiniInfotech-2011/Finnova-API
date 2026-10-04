using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class UserBranchAssociationConfiguration : IEntityTypeConfiguration<UserBranchAssociation>
{
    public void Configure(EntityTypeBuilder<UserBranchAssociation> b)
    {
        b.ToTable("user_branch_associations");
        b.HasKey(x => x.Id);
        b.Property(x => x.UserAccountId);
        b.Property(x => x.UserGroupId);
        b.Property(x => x.LineOfBusiness).IsRequired().HasMaxLength(100);
        b.Property(x => x.BranchCode).IsRequired().HasMaxLength(50);
        b.Property(x => x.IsAll).IsRequired();

        b.HasIndex(x => new { x.UserAccountId, x.LineOfBusiness });
        b.HasIndex(x => new { x.UserGroupId, x.LineOfBusiness });

        b.ToTable(t => t.HasCheckConstraint(
            "CK_user_branch_associations_owner",
            "([UserAccountId] IS NOT NULL AND [UserGroupId] IS NULL) OR ([UserAccountId] IS NULL AND [UserGroupId] IS NOT NULL)"));
    }
}
