using Finnova.Models.Contracts.Customers;
using Finnova.Service.Customers.Validation;
using Finnova.Models.Domain.Exceptions;

namespace Finnova.Service.Customers.References;

public interface ICustomerMasterReferenceValidator
{
    Task<MasterReferenceValidationResult> ValidateAsync(CustomerCreateRequest request, CancellationToken ct = default);
    Task<MasterReferenceValidationResult> ValidateAsync(CustomerModifyRequest request, CustomerResponse current, CancellationToken ct = default);
}

public sealed class CustomerMasterReferenceValidator(
    ILookupReferenceResolver lookups, INationalityReferenceResolver nationalities,
    IGLPostingGroupResolver gl, IDocumentDefinitionResolver documents, IIndiaAddressControl addresses,
    CustomerFieldApplicabilityEvaluator applicability) : ICustomerMasterReferenceValidator
{
    public Task<MasterReferenceValidationResult> ValidateAsync(CustomerCreateRequest r, CancellationToken ct = default)
        => ValidateCoreAsync(r.CompanyId, r.CustomerTypeCode, r.GroupCode, r.IndustryCode, r.CustomerPostingGroupCode, r.GLAccountId, r.ConstitutionCode, r.NationalityCode, r.TitleCode, r.MaritalStatusCode, r.CustomerBusinessTypeCode, r.CustomerBusinessSubTypeCode, r.IndustrySectorCode, r.SubSectorCode, r.Addresses, r.Documents, "Create", ct);

    public Task<MasterReferenceValidationResult> ValidateAsync(CustomerModifyRequest r, CustomerResponse current, CancellationToken ct = default)
        => ValidateCoreAsync(r.CompanyId, r.CustomerTypeCode ?? current.CustomerTypeCode, r.GroupCode, r.IndustryCode, r.CustomerPostingGroupCode, r.GLAccountId, r.ConstitutionCode, r.NationalityCode, r.TitleCode, r.MaritalStatusCode, r.CustomerBusinessTypeCode, r.CustomerBusinessSubTypeCode, r.IndustrySectorCode, r.SubSectorCode, r.Addresses, r.Documents, "Modify", ct);

    private async Task<MasterReferenceValidationResult> ValidateCoreAsync(Guid companyId, string customerType, string? group, string? industry, string? posting, Guid? glId, string? constitution, string? nationality, string? title, string? marital, string? businessType, string? businessSubType, string? sector, string? subSector, IReadOnlyCollection<CustomerAddressRequest> addressRequests, IReadOnlyCollection<CustomerDocumentReferenceRequest> documentRequests, string mode, CancellationToken ct)
    {
        var map = new Dictionary<string, CustomerReferenceProjection>();

        async Task Add(string key, string? code, string type)
        {
            if (!string.IsNullOrWhiteSpace(code))
            {
                var x = await lookups.ResolveActiveAsync(CustomerLookupTypes.Module, type, code, key, ct);
                map[key] = new(x.Code, x.Value);
            }
        }

        await Add(nameof(group), group, CustomerLookupTypes.Group);
        await Add(nameof(industry), industry, CustomerLookupTypes.Industry);
        await Add(nameof(constitution), constitution, CustomerLookupTypes.Constitution);
        await Add(nameof(title), title, CustomerLookupTypes.Title);
        await Add(nameof(marital), marital, CustomerLookupTypes.MaritalStatus);
        await Add(nameof(businessType), businessType, CustomerLookupTypes.CustomerBusinessType);
        await Add(nameof(businessSubType), businessSubType, CustomerLookupTypes.CustomerBusinessSubType);
        await Add(nameof(sector), sector, CustomerLookupTypes.IndustrySector);
        await Add(nameof(subSector), subSector, CustomerLookupTypes.SubSector);

        if (!string.IsNullOrWhiteSpace(nationality))
        {
            var n = await nationalities.ResolveActiveAsync(nationality, ct);
            map[nameof(nationality)] = new(n.Code, n.Name);
        }

        if (!string.IsNullOrWhiteSpace(posting))
        {
            var g = await gl.ResolveAsync(companyId, posting, glId, ct);
            map[nameof(posting)] = new(g.Code, g.Name);
        }

        var applicable = string.IsNullOrWhiteSpace(constitution)
            ? Array.Empty<DocumentDefinitionProjection>()
            : await documents.GetApplicableAsync(constitution, CustomerLookupTypes.EntityRole, ct);

        var approvedAddresses = new List<CustomerAddressResponse>();
        foreach (var address in addressRequests)
        {
            var result = await addresses.ValidateAndApproveAsync(address, ct);
            if (!result.IsValid || result.ApprovedAddress is null)
                throw new CustomerAddressControlValidationException(result.Errors);
            approvedAddresses.Add(result.ApprovedAddress);
        }

        var requestedDocumentIds = documentRequests.Select(x => x.DocumentDefinitionId).ToHashSet();
        var mandatoryMissing = applicable.Where(x => x.IsMandatory && !requestedDocumentIds.Contains(x.Id)).ToArray();
        if (mandatoryMissing.Length > 0)
            throw new CustomerDocumentDefinitionUnavailableException(constitution!, CustomerLookupTypes.EntityRole);

        applicability.Validate(new CustomerApplicabilityContext(customerType, null, null, businessType, businessSubType, null, constitution, mode));

        return new(
            map,
            applicable.Select(x => new CustomerDocumentResponse(x.Id, x.Id, x.Code, x.Category, x.Description, x.IsMandatory, x.IsImageRequired, false, false, null, null, null, null, null)).ToArray(),
            approvedAddresses.FirstOrDefault(x => x.AddressType == Finnova.Models.Domain.Enums.CustomerAddressType.Communication),
            approvedAddresses.FirstOrDefault(x => x.AddressType == Finnova.Models.Domain.Enums.CustomerAddressType.Permanent));
    }
}
