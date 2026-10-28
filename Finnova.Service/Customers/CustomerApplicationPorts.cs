using Finnova.Models.Contracts.Customers;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Service.Customers.References;

namespace Finnova.Service.Customers;

public interface IDocumentNumberGenerator
{
    Task<string> GenerateAsync(string scope, int length, CancellationToken ct = default);
}

public interface IAccountContractBoundary
{
    Task RequireModifyPrerequisiteAsync(Guid customerId, string? accountNumber, CancellationToken ct = default);
    Task<CustomerCreditResponse> GetCreditAsync(Guid customerId, CancellationToken ct = default);
    Task<CustomerCreditResponse> UpdateValidUpToAsync(Guid customerId, DateOnly? validUpTo, string actorId, CancellationToken ct = default);
}

public interface ICustomerAggregateMapper
{
    string NormalizeIdentity(CustomerCreateRequest request);
    Customer Create(CustomerCreateRequest request, string customerCode, MasterReferenceValidationResult references);
    void ApplyModify(Customer customer, CustomerModifyRequest request, MasterReferenceValidationResult references);
    CustomerAuditEntry CreatedAudit(Customer customer, string actorId);
    CustomerAuditEntry ModifiedAudit(Customer customer, string actorId);
    CustomerResponse Response(Customer customer, MasterReferenceValidationResult references);
}

public interface ICustomerProjectionMapper
{
    Task<CustomerResponse> ProjectAsync(Customer customer, CancellationToken ct = default);
}

public interface ICustomerChildMapper
{
    BankAccountRequest Request(BankAccount account);
    Task AddBank(Customer customer, BankAccountRequest request, CancellationToken ct = default);
    Task ModifyBank(BankAccount account, BankAccountRequest request, CancellationToken ct = default);
    Task SetDefault(Customer customer, BankAccount account);
    Task ClearDefault(Customer customer);
    CustomerAuditEntry Audit(Customer customer, string actorId, CustomerAuditAction action);
    BankAccountResponse BankResponse(BankAccount account);
    CustomerDocumentReference AddDocument(Customer customer, CustomerDocumentReferenceRequest request, IReadOnlyCollection<DocumentDefinitionProjection> definitions);
    void ModifyDocument(CustomerDocumentReference row, CustomerDocumentReferenceRequest request, IReadOnlyCollection<DocumentDefinitionProjection> definitions);
    CustomerDocumentResponse DocumentResponse(CustomerDocumentReference row);
    CustomerTrackEntry AddTrack(Customer customer, CustomerTrackRequest request);
    void ReleaseTrack(CustomerTrackEntry row, string actorId);
    CustomerTrackResponse TrackResponse(CustomerTrackEntry row);
    FactoringSubLimit AddFactoring(Customer customer, FactoringSubLimitRequest request);
    void ModifyFactoring(FactoringSubLimit row, FactoringSubLimitRequest request);
    FactoringSubLimitResponse FactoringResponse(FactoringSubLimit row);
    CustomerAddressResponse AddressResponse(CustomerAddress row);
    void ReplacePermanent(Customer customer, CustomerAddressResponse address);
}
