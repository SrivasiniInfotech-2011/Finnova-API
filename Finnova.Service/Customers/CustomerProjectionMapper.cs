using Finnova.Models.Contracts.Customers;
using Finnova.Models.Domain.Entities;
using Finnova.Service.Customers.References;

namespace Finnova.Service.Customers;

public sealed class CustomerProjectionMapper(
    ILookupReferenceResolver lookups,
    INationalityReferenceResolver nationalities,
    IGLPostingGroupResolver gl,
    CustomerAggregateMapper responseMapper) : ICustomerProjectionMapper
{
    public async Task<CustomerResponse> ProjectAsync(Customer customer, CancellationToken ct = default)
    {
        var references = new Dictionary<string, CustomerReferenceProjection>();

        async Task AddLookup(string key, string? code, string type)
        {
            if (string.IsNullOrWhiteSpace(code)) return;
            var value = await lookups.ResolveActiveAsync(CustomerLookupTypes.Module, type, code, key, ct);
            references[key] = new(value.Code, value.Value);
        }

        await AddLookup("group", customer.GroupCode, CustomerLookupTypes.Group);
        await AddLookup("industry", customer.IndustryCode, CustomerLookupTypes.Industry);
        await AddLookup("constitution", customer.ConstitutionCode, CustomerLookupTypes.Constitution);
        await AddLookup("title", customer.TitleCode, CustomerLookupTypes.Title);
        await AddLookup("marital", customer.MaritalStatusCode, CustomerLookupTypes.MaritalStatus);
        await AddLookup("businessType", customer.CustomerBusinessTypeCode, CustomerLookupTypes.CustomerBusinessType);
        await AddLookup("businessSubType", customer.CustomerBusinessSubTypeCode, CustomerLookupTypes.CustomerBusinessSubType);
        await AddLookup("sector", customer.IndustrySectorCode, CustomerLookupTypes.IndustrySector);
        await AddLookup("subSector", customer.SubSectorCode, CustomerLookupTypes.SubSector);

        if (!string.IsNullOrWhiteSpace(customer.NationalityCode))
        {
            var nationality = await nationalities.ResolveActiveAsync(customer.NationalityCode, ct);
            references["nationality"] = new(nationality.Code, nationality.Name);
        }

        if (!string.IsNullOrWhiteSpace(customer.CustomerPostingGroupCode))
        {
            var posting = await gl.ResolveAsync(customer.CompanyId, customer.CustomerPostingGroupCode,
                customer.GLAccountId, ct);
            references["posting"] = new(posting.Code, posting.Name);
        }

        return responseMapper.Response(customer,
            new MasterReferenceValidationResult(references, Array.Empty<CustomerDocumentResponse>(), null, null));
    }
}
