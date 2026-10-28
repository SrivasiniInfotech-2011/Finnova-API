using Finnova.Models.Contracts.Customers;
using Finnova.Models.Domain.Exceptions;

namespace Finnova.Service.Customers.References;

public interface IIndiaAddressControl
{
    Task<AddressControlResult> ValidateAndApproveAsync(CustomerAddressRequest request, CancellationToken ct = default);
    Task<CustomerAddressResponse> CopyCommunicationToPermanentAsync(CustomerAddressResponse communication, CancellationToken ct = default);
}

public sealed class IndiaAddressControlDeferredAdapter : IIndiaAddressControl
{
    public Task<AddressControlResult> ValidateAndApproveAsync(CustomerAddressRequest request, CancellationToken ct = default)
        => throw new CustomerBoundaryUnavailableException("India Address Control");

    public Task<CustomerAddressResponse> CopyCommunicationToPermanentAsync(CustomerAddressResponse communication, CancellationToken ct = default)
        => Task.FromResult(communication with { AddressType = Finnova.Models.Domain.Enums.CustomerAddressType.Permanent });
}

public sealed class IndiaAddressControlTestFake : IIndiaAddressControl
{
    public Task<AddressControlResult> ValidateAndApproveAsync(CustomerAddressRequest r, CancellationToken ct = default)
        => Task.FromResult(new AddressControlResult(true, Array.Empty<string>(), new CustomerAddressResponse(r.AddressType, r.HouseNumber, r.BlockNumber, r.PostalCode, r.City, r.State, r.Country, r.POBoxNumber, r.WayNumber, r.Telephone, r.ResidenceFaxNumber, r.Mobile, r.Email, r.Website, r.ContactPerson, r.ContactPersonPhoneNumber, r.ContactPersonFlatNumber, r.AreaSheik, r.LocationLandmark)));

    public Task<CustomerAddressResponse> CopyCommunicationToPermanentAsync(CustomerAddressResponse c, CancellationToken ct = default)
        => Task.FromResult(c with { AddressType = Finnova.Models.Domain.Enums.CustomerAddressType.Permanent });
}
