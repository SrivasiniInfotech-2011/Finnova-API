using Finnova.Models.Contracts.IdentityVerification;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.IdentityVerification.Storage;
using MediatR;

namespace Finnova.Service.IdentityVerification.Queries;

public interface IIdentityVerificationDocumentQuery
{
    Task<ProtectedDocumentResponse> GetAsync(Guid recordId, CancellationToken ct);
}

public sealed record GetProtectedDocumentQuery(Guid RecordId) : IRequest<ProtectedDocumentResponse>;

public sealed class ProtectedDocumentQuery(
    IIdentityVerificationRepository repository,
    IProtectedDocumentStore store)
    : IRequestHandler<GetProtectedDocumentQuery, ProtectedDocumentResponse>,
      IIdentityVerificationDocumentQuery
{
    public async Task<ProtectedDocumentResponse> Handle(GetProtectedDocumentQuery request, CancellationToken ct)
    {
        var record = await repository.GetRecordAsync(request.RecordId, ct);
        if (record is null || record.ProtectedDocumentReference is null)
            throw new IdentityVerificationRecordNotFoundException();

        var protectedDoc = record.ProtectedDocumentReference;

        var stream = await store.OpenReadAsync(protectedDoc.StorageKey, ct);
        if (stream is null)
            throw new IdentityVerificationRecordNotFoundException();

        var fileName = $"document_{record.OperationType}_{record.Id}.{GetExtension(protectedDoc.ContentType)}";
        return new(stream, protectedDoc.ContentType, fileName);
    }

    public async Task<ProtectedDocumentResponse> GetAsync(Guid recordId, CancellationToken ct)
    {
        var record = await repository.GetRecordAsync(recordId, ct);
        if (record is null || record.Method != VerificationMethod.ManualOverride || record.ProtectedDocumentReference is null)
            throw new IdentityVerificationRecordNotFoundException();

        var protectedDoc = record.ProtectedDocumentReference;
        var stream = await store.OpenReadAsync(protectedDoc.StorageKey, ct);
        if (stream is null)
            throw new IdentityVerificationRecordNotFoundException();

        return new(stream, protectedDoc.ContentType, SafeName(protectedDoc.ContentType));
    }

    private static string GetExtension(string contentType)
    {
        return contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => "jpg",
            "image/png" => "png",
            "application/pdf" => "pdf",
            _ => "dat"
        };
    }

    private static string SafeName(string contentType)
    {
        return contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => "identity-document.jpg",
            "image/png" => "identity-document.png",
            "application/pdf" => "identity-document.pdf",
            _ => throw new IdentityVerificationRecordNotFoundException()
        };
    }
}
