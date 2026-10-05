using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class UserAccessAssignmentConfiguration : IEntityTypeConfiguration<UserAccessAssignment>
{
    public void Configure(EntityTypeBuilder<UserAccessAssignment> b)
    {
        b.ToTable("user_access_assignments");
        b.HasKey(x => x.Id);
        b.Property(x => x.UserAccountId);
        b.Property(x => x.UserGroupId);
        b.Property(x => x.LineOfBusinessId).IsRequired();
        b.Property(x => x.RoleCenterName).IsRequired().HasMaxLength(100);
        b.Property(x => x.ProgramId).IsRequired();
        b.Property(x => x.RoleCode).IsRequired().HasMaxLength(50);
        b.Property(x => x.CanAdd).IsRequired();
        b.Property(x => x.CanModify).IsRequired();
        b.Property(x => x.CanQuery).IsRequired();
        b.Property(x => x.CanDelete).IsRequired();

        b.HasOne<LineOfBusiness>().WithMany().HasForeignKey(x => x.LineOfBusinessId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ScreenProgram>().WithMany().HasForeignKey(x => x.ProgramId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.UserAccountId, x.LineOfBusinessId, x.RoleCode });
        b.HasIndex(x => new { x.UserGroupId, x.LineOfBusinessId, x.RoleCode });

        // Exactly one owner (user or group) (R8.9 owner polymorphism).
        b.ToTable(t => t.HasCheckConstraint(
            "CK_user_access_assignments_owner",
            "([UserAccountId] IS NOT NULL AND [UserGroupId] IS NULL) OR ([UserAccountId] IS NULL AND [UserGroupId] IS NOT NULL)"));
    }
}
