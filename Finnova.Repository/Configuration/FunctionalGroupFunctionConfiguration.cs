using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class FunctionalGroupFunctionConfiguration : IEntityTypeConfiguration<FunctionalGroupFunction>
{
    public void Configure(EntityTypeBuilder<FunctionalGroupFunction> b)
    {
        b.ToTable("functional_group_functions");
        b.HasKey(x => x.Id);
        b.Property(x => x.FunctionalGroupId).IsRequired();
        b.Property(x => x.ProgramId).IsRequired();
        b.Property(x => x.RoleCode).IsRequired().HasMaxLength(50);

        b.HasOne<ScreenProgram>().WithMany().HasForeignKey(x => x.ProgramId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.FunctionalGroupId, x.RoleCode }).IsUnique();
    }
}
