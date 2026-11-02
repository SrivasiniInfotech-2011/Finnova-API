using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.IdentityVerification.Storage;
namespace Finnova.Service.IdentityVerification.Adapters;
public interface IProviderDocumentAccess { Task<Stream> OpenReadAsync(Guid protectedDocumentReferenceId, CancellationToken ct); }
public sealed class ProviderDocumentAccess(IIdentityVerificationRepository repository, IProtectedDocumentStore store) : IProviderDocumentAccess
{
    public async Task<Stream> OpenReadAsync(Guid protectedDocumentReferenceId, CancellationToken ct) { var reference = await repository.GetProtectedDocumentReferenceAsync(protectedDocumentReferenceId, ct) ?? throw new IdentityVerificationRecordNotFoundException(); return await store.OpenReadAsync(reference.StorageKey, ct) ?? throw new IdentityVerificationRecordNotFoundException(); }
}
