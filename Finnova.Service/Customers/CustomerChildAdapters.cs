using Finnova.Models.Contracts.Customers;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Customers.References;
namespace Finnova.Service.Customers;

public sealed class AccountContractBoundary(IAccountRepository accounts) : IAccountContractBoundary
{
    public async Task RequireModifyPrerequisiteAsync(Guid customerId, string? accountNumber, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(accountNumber))
            throw new CustomerBoundaryUnavailableException("Account/Contract");

        var account = await accounts.GetByAccountNumberAsync(accountNumber.Trim(), ct);
        if (account is null || account.Status != AccountStatus.Active)
            throw new CustomerBoundaryUnavailableException("Account/Contract");
    }

    public Task<CustomerCreditResponse> GetCreditAsync(Guid customerId, CancellationToken ct = default)
        => throw new CustomerBoundaryUnavailableException("Account/Contract credit");

    public Task<CustomerCreditResponse> UpdateValidUpToAsync(Guid customerId, DateOnly? validUpTo, string actorId, CancellationToken ct = default)
        => throw new CustomerBoundaryUnavailableException("Account/Contract credit");
}

public sealed class CustomerChildMapper : ICustomerChildMapper
{
    public BankAccountRequest Request(BankAccount account)
        => new(account.AccountTypeCode, account.AccountNumber, account.BankCode, account.MICRCode,
            account.ClearingDate, account.Address, account.ContractNumber, account.SubContractNumber,
            account.IsDefault);

    public Task AddBank(Customer customer, BankAccountRequest request, CancellationToken ct = default)
    {
        var account = new BankAccount();
        account.Initialize(request.AccountTypeCode, request.AccountNumber, request.BankCode, request.MICRCode,
            request.ClearingDate, request.Address, request.ContractNumber, request.SubContractNumber,
            request.IsDefault);
        customer.BankAccounts.Add(account);
        return Task.CompletedTask;
    }

    public Task ModifyBank(BankAccount account, BankAccountRequest request, CancellationToken ct = default)
    {
        account.Initialize(request.AccountTypeCode, request.AccountNumber, request.BankCode, request.MICRCode,
            request.ClearingDate, request.Address, request.ContractNumber, request.SubContractNumber,
            request.IsDefault);
        return Task.CompletedTask;
    }

    public Task SetDefault(Customer customer, BankAccount account)
    {
        foreach (var row in customer.BankAccounts)
            row.SetDefault(row.Id == account.Id);
        return Task.CompletedTask;
    }

    public Task ClearDefault(Customer customer)
    {
        foreach (var row in customer.BankAccounts)
            row.SetDefault(false);
        return Task.CompletedTask;
    }

    public CustomerAuditEntry Audit(Customer customer, string actorId, CustomerAuditAction action)
        => CustomerAuditEntry.Create(customer.Id, action, actorId,
            $"{{\"Action\":\"{action}\",\"CustomerCode\":\"{customer.CustomerCode}\"}}");

    public BankAccountResponse BankResponse(BankAccount account)
        => new(account.Id, account.AccountTypeCode, account.AccountNumber, account.BankCode,
            null, null, account.MICRCode, account.ClearingDate, account.IsDefault);

    public CustomerDocumentReference AddDocument(Customer customer, CustomerDocumentReferenceRequest request,
        IReadOnlyCollection<DocumentDefinitionProjection> definitions)
    {
        var definition = definitions.First(x => x.Id == request.DocumentDefinitionId);
        var row = new CustomerDocumentReference();
        row.Initialize(request.ConstitutionCode, request.DocumentDefinitionId, request.DocumentCode,
            definition.Category, definition.Description, definition.IsMandatory, definition.IsImageRequired,
            request.Remarks, request.IsCollected, request.IsScanned, request.Value, request.ValueSource,
            request.DmsReference, request.ViewMetadata);
        customer.Documents.Add(row);
        return row;
    }

    public void ModifyDocument(CustomerDocumentReference row, CustomerDocumentReferenceRequest request,
        IReadOnlyCollection<DocumentDefinitionProjection> definitions)
    {
        var definition = definitions.First(x => x.Id == request.DocumentDefinitionId);
        row.Initialize(request.ConstitutionCode, request.DocumentDefinitionId, request.DocumentCode,
            definition.Category, definition.Description, definition.IsMandatory, definition.IsImageRequired,
            request.Remarks, request.IsCollected, request.IsScanned, request.Value, request.ValueSource,
            request.DmsReference, request.ViewMetadata);
    }

    public CustomerDocumentResponse DocumentResponse(CustomerDocumentReference row)
        => new(row.Id, row.DocumentDefinitionId, row.DocumentCode, row.DocumentCategory, row.DocumentDescription,
            row.IsMandatory, row.IsImageRequired, row.IsCollected, row.IsScanned, row.Remarks, row.Value,
            row.ValueSource, row.DmsReference, row.ViewMetadata);

    public CustomerTrackEntry AddTrack(Customer customer, CustomerTrackRequest request)
    {
        var row = new CustomerTrackEntry();
        row.Initialize(request.LineOfBusinessCode, request.AccountNumber, request.TrackTypeCode,
            request.TrackDate, request.Reason, request.ByLogin);
        customer.Tracks.Add(row);
        return row;
    }

    public void ReleaseTrack(CustomerTrackEntry row, string actorId) => row.Release(actorId);

    public CustomerTrackResponse TrackResponse(CustomerTrackEntry row)
        => new(row.Id, row.LineOfBusinessCode, row.AccountNumber, row.TrackTypeCode, null, row.TrackDate,
            row.Reason, row.ByLogin, row.ReleaseDate, row.ReleasedBy, row.Status);

    public FactoringSubLimit AddFactoring(Customer customer, FactoringSubLimitRequest request)
    {
        var row = new FactoringSubLimit();
        row.Initialize(request.FactoringApplicable, request.FactoringTypeCode, request.Limit, request.Covenant,
            request.CovenantClause, request.CutOffDate);
        customer.FactoringSubLimits.Add(row);
        return row;
    }

    public void ModifyFactoring(FactoringSubLimit row, FactoringSubLimitRequest request)
        => row.Initialize(request.FactoringApplicable, request.FactoringTypeCode, request.Limit, request.Covenant,
            request.CovenantClause, request.CutOffDate);

    public FactoringSubLimitResponse FactoringResponse(FactoringSubLimit row)
        => new(row.Id, row.FactoringApplicable, row.FactoringTypeCode, null, row.Limit, row.Covenant,
            row.CovenantClause, row.CutOffDate);

    public CustomerAddressResponse AddressResponse(CustomerAddress row)
        => new(row.AddressType, row.HouseNumber, row.BlockNumber, row.PostalCode, row.City, row.State,
            row.Country, row.POBoxNumber, row.WayNumber, row.Telephone, row.ResidenceFaxNumber, row.Mobile,
            row.Email, row.Website, row.ContactPerson, row.ContactPersonPhoneNumber,
            row.ContactPersonFlatNumber, row.AreaSheik, row.LocationLandmark);

    public void ReplacePermanent(Customer customer, CustomerAddressResponse address)
    {
        var permanent = customer.Addresses.SingleOrDefault(x => x.AddressType == CustomerAddressType.Permanent);
        if (permanent is null)
        {
            permanent = new CustomerAddress();
            customer.Addresses.Add(permanent);
        }

        permanent.Initialize(address.AddressType, address.HouseNumber, address.BlockNumber, address.PostalCode,
            address.City, address.State, address.Country, address.POBoxNumber, address.WayNumber, address.Telephone,
            address.ResidenceFaxNumber, address.Mobile, address.Email, address.Website, address.ContactPerson,
            address.ContactPersonPhoneNumber, address.ContactPersonFlatNumber, address.AreaSheik,
            address.LocationLandmark);
    }
}
