namespace Finnova.Service.Applications.Ports;

public interface IApplicationReferenceValidator
{
    Task ValidateAsync(Guid lobId, Guid customerId, Guid branchId, string productCode, string schemeCode, CancellationToken ct);
}
