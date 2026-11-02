using Finnova.Models.Domain.Entities;

namespace Finnova.Service.IdentityVerification.Storage;

public sealed class ProtectedDocumentLifecycle(IProtectedDocumentStore store)
{
    public async Task<TResult> UploadAndExecuteAsync<TResult>(
        Stream content,
        string contentType,
        long sizeBytes,
        Func<ProtectedDocumentReference, Task<TResult>> action,
        CancellationToken ct)
    {
        var reference = await store.PutAsync(content, contentType, sizeBytes, ct);
        try
        {
            return await action(reference);
        }
        catch
        {
            try
            {
                await store.DeleteAsync(reference.StorageKey, CancellationToken.None);
            }
            catch
            {
                // Preserve the original provider or persistence failure.
            }

            throw;
        }
    }
}
