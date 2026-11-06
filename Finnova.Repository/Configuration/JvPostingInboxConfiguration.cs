using Finnova.Models.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finnova.Repository.Configuration;

public sealed class JvPostingInboxConfiguration : IEntityTypeConfiguration<JvPostingInbox>
{
    public void Configure(EntityTypeBuilder<JvPostingInbox> builder)
    {
        builder.HasKey(x => x.RequestId);
    }
}
