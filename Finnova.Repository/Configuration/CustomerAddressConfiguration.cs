using Finnova.Models.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finnova.Repository.Configuration;

public sealed class CustomerAddressConfiguration : IEntityTypeConfiguration<CustomerAddress>
{
    public void Configure(EntityTypeBuilder<CustomerAddress> b)
    {
        b.ToTable("CustomerAddresses"); b.HasKey(x => x.Id); b.Property(x => x.AddressType).HasConversion<string>().HasMaxLength(20);
        foreach (var p in new[] { nameof(CustomerAddress.HouseNumber), nameof(CustomerAddress.BlockNumber), nameof(CustomerAddress.City), nameof(CustomerAddress.State), nameof(CustomerAddress.Country), nameof(CustomerAddress.POBoxNumber), nameof(CustomerAddress.WayNumber), nameof(CustomerAddress.Telephone), nameof(CustomerAddress.ResidenceFaxNumber), nameof(CustomerAddress.Mobile), nameof(CustomerAddress.Email), nameof(CustomerAddress.Website), nameof(CustomerAddress.ContactPerson), nameof(CustomerAddress.ContactPersonPhoneNumber), nameof(CustomerAddress.ContactPersonFlatNumber), nameof(CustomerAddress.AreaSheik), nameof(CustomerAddress.LocationLandmark) }) b.Property<string?>(p).HasMaxLength(300);
        b.Property(x => x.PostalCode).HasMaxLength(6); b.HasIndex(x => new { x.CustomerId, x.AddressType }).IsUnique();
        b.HasIndex(x => x.CustomerId);
    }
}
