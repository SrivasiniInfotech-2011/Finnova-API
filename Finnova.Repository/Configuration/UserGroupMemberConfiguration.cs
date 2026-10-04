using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class UserGroupMemberConfiguration : IEntityTypeConfiguration<UserGroupMember>
{
    public void Configure(EntityTypeBuilder<UserGroupMember> b)
    {
        b.ToTable("user_group_members");
        b.HasKey(x => x.Id);
        b.Property(x => x.UserGroupId).IsRequired();
        b.Property(x => x.UserAccountId).IsRequired();

        // No duplicate member within a group (R5.7).
        b.HasIndex(x => new { x.UserGroupId, x.UserAccountId }).IsUnique();
    }
}
