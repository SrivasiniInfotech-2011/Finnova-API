using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class CompanyPostingStateConfiguration : IEntityTypeConfiguration<CompanyPostingState>
{
    public void Configure(EntityTypeBuilder<CompanyPostingState> builder)
    {
        builder.ToTable("company_posting_states");
        builder.HasKey(x => x.CompanyId);
        builder.Property(x => x.JvPostingEnabled).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();
    }
}
