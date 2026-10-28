using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;

namespace Finnova.Service.Customers.References;

public interface IDocumentDefinitionResolver
{
    Task<IReadOnlyCollection<DocumentDefinitionProjection>> GetApplicableAsync(string constitutionCode, string entityRole, CancellationToken ct = default);
    Task<DocumentDefinitionProjection> ResolveApplicableAsync(Guid documentId, string constitutionCode, string entityRole, CancellationToken ct = default);
}

public sealed class DocumentDefinitionResolver(IDocumentRepository repository) : IDocumentDefinitionResolver
{
    public async Task<IReadOnlyCollection<DocumentDefinitionProjection>> GetApplicableAsync(string constitutionCode, string entityRole, CancellationToken ct = default)
        => (await repository.GetActiveForConsumptionAsync(constitutionCode, entityRole, ct)).Select(x => new DocumentDefinitionProjection(x.Id, x.DocCode, x.DocCategory, x.Description, x.IsMandatory, null)).ToArray();

    public async Task<DocumentDefinitionProjection> ResolveApplicableAsync(Guid documentId, string constitutionCode, string entityRole, CancellationToken ct = default)
    {
        var row = (await repository.GetActiveForConsumptionAsync(constitutionCode, entityRole, ct)).FirstOrDefault(x => x.Id == documentId);
        return row is null ? throw new CustomerDocumentDefinitionUnavailableException(constitutionCode, entityRole) : new(row.Id, row.DocCode, row.DocCategory, row.Description, row.IsMandatory, null);
    }
}
