using Microsoft.Extensions.Configuration;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Applications.Ports;

namespace Finnova.Service.Applications.References;

public sealed class ApplicationReferenceValidator : IApplicationReferenceValidator
{
    private readonly ILineOfBusinessReferencePort _lob;
    private readonly ICustomerReferencePort _customer;
    private readonly IBranchReferencePort _branch;
    private readonly ILookupReferencePort _lookup;
    private readonly IConfiguration _configuration;
    public ApplicationReferenceValidator(ILineOfBusinessReferencePort lob, ICustomerReferencePort customer,
        IBranchReferencePort branch, ILookupReferencePort lookup, IConfiguration configuration)
        => (_lob, _customer, _branch, _lookup, _configuration) = (lob, customer, branch, lookup, configuration);

    public async Task ValidateAsync(Guid lobId, Guid customerId, Guid branchId, string productCode, string schemeCode, CancellationToken ct)
    {
        var module = _configuration["ApplicationProcessing:LookupModule"] ?? throw new ApplicationConfigurationException("ApplicationProcessing:LookupModule is required.");
        var productType = _configuration["ApplicationProcessing:ProductLookupType"] ?? throw new ApplicationConfigurationException("ApplicationProcessing:ProductLookupType is required.");
        var schemeType = _configuration["ApplicationProcessing:SchemeLookupType"] ?? throw new ApplicationConfigurationException("ApplicationProcessing:SchemeLookupType is required.");
        var checks = await Task.WhenAll(_lob.ExistsActiveAsync(lobId, ct), _customer.ExistsActiveAsync(customerId, ct),
            _branch.ExistsActiveAsync(branchId, ct), _lookup.ExistsActiveAsync(module, productType, productCode.Trim(), ct),
            _lookup.ExistsActiveAsync(module, schemeType, schemeCode.Trim(), ct));
        if (!checks[0]) throw new ApplicationReferenceInvalidException("Line of Business");
        if (!checks[1]) throw new ApplicationReferenceInvalidException("Customer");
        if (!checks[2]) throw new ApplicationReferenceInvalidException("Branch");
        if (!checks[3]) throw new ApplicationReferenceInvalidException("Product");
        if (!checks[4]) throw new ApplicationReferenceInvalidException("Scheme");
    }
}
