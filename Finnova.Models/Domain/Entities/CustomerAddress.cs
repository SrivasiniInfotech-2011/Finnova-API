using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Entities;

public sealed class CustomerAddress
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid CustomerId { get; private set; }
    public CustomerAddressType AddressType { get; private set; }
    public string? HouseNumber { get; private set; }
    public string? BlockNumber { get; private set; }
    public string? PostalCode { get; private set; }
    public string? City { get; private set; }
    public string? State { get; private set; }
    public string? Country { get; private set; }
    public string? POBoxNumber { get; private set; }
    public string? WayNumber { get; private set; }
    public string? Telephone { get; private set; }
    public string? ResidenceFaxNumber { get; private set; }
    public string? Mobile { get; private set; }
    public string? Email { get; private set; }
    public string? Website { get; private set; }
    public string? ContactPerson { get; private set; }
    public string? ContactPersonPhoneNumber { get; private set; }
    public string? ContactPersonFlatNumber { get; private set; }
    public string? AreaSheik { get; private set; }
    public string? LocationLandmark { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public void Initialize(
        CustomerAddressType addressType, string? houseNumber, string? blockNumber, string? postalCode,
        string? city, string? state, string? country, string? poBoxNumber, string? wayNumber,
        string? telephone, string? residenceFaxNumber, string? mobile, string? email, string? website,
        string? contactPerson, string? contactPersonPhoneNumber, string? contactPersonFlatNumber,
        string? areaSheik, string? locationLandmark)
    {
        AddressType = addressType;
        HouseNumber = houseNumber;
        BlockNumber = blockNumber;
        PostalCode = postalCode;
        City = city;
        State = state;
        Country = country;
        POBoxNumber = poBoxNumber;
        WayNumber = wayNumber;
        Telephone = telephone;
        ResidenceFaxNumber = residenceFaxNumber;
        Mobile = mobile;
        Email = email;
        Website = website;
        ContactPerson = contactPerson;
        ContactPersonPhoneNumber = contactPersonPhoneNumber;
        ContactPersonFlatNumber = contactPersonFlatNumber;
        AreaSheik = areaSheik;
        LocationLandmark = locationLandmark;
        UpdatedAtUtc = DateTime.UtcNow;
    }
    public DateTime UpdatedAtUtc { get; private set; } = DateTime.UtcNow;
}
