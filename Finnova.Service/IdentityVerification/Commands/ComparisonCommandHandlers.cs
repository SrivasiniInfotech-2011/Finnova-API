using Finnova.Models.Contracts.IdentityVerification;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.IdentityVerification.Adapters;
using Finnova.Service.IdentityVerification.Commands.VerifyAadharAddress;
using Finnova.Service.IdentityVerification.Commands.VerifyPanName;
using Finnova.Service.IdentityVerification.Correlation;
using Finnova.Service.IdentityVerification.External;
using Finnova.Service.IdentityVerification.Storage;
using MediatR;

namespace Finnova.Service.IdentityVerification.Commands.Comparison;

public sealed class VerifyPanNameCommandHandler(
    IComparisonAdapterResolver adapters,
    IIdentityVerificationRepository repository,
    IProtectedDocumentStore store,
    IRequestContextAccessor context,
    IProviderCallExecutor executor) : IRequestHandler<VerifyPanNameCommand, ComparisonVerificationResult>
{
    public async Task<ComparisonVerificationResult> Handle(VerifyPanNameCommand request, CancellationToken ct)
    {
        var c = context.Current;
        var value = IdentityVerificationRules.RequireText(request.Request.SubmittedName, "SubmittedName");
        
        var adapter = adapters.Resolve(VerificationOperationType.PanNameVerification, request.Request.Source.MappingProfile ?? "");
        
        ProtectedDocumentReference? reference = null;
        if (request.Request.Source.ProtectedDocumentReferenceId is not null)
        {
            reference = await repository.GetProtectedDocumentReferenceAsync(request.Request.Source.ProtectedDocumentReferenceId.Value, ct)
                ?? throw new IdentityVerificationRecordNotFoundException();
        }

        ComparisonAdapterResult result;
        await using (var stream = reference is null
            ? null
            : await store.OpenReadAsync(reference.StorageKey, ct)
                ?? throw new IdentityVerificationRecordNotFoundException())
        {
            result = await executor.ExecuteAsync(
                t => adapter.VerifyAsync(
                    new ProviderNeutralComparisonInput(
                        VerificationOperationType.PanNameVerification,
                        value,
                        reference?.Id,
                        stream,
                        request.Request.Source.ExtractedValues ?? new Dictionary<string, string>(),
                        c.CorrelationId,
                        request.Request.Source.MappingProfile ?? ""),
                    t),
                VerificationOperationType.PanNameVerification,
                c.CorrelationId,
                ct);
        }

        var sourceSummary = new SourceSummary
        {
            MappingProfile = request.Request.Source.MappingProfile,
            DocumentProvided = reference is not null,
            DocumentContentType = reference?.ContentType,
            DocumentSizeBytes = reference?.SizeBytes,
            ProtectedDocumentReferenceId = reference?.Id,
            ExtractionCompleted = request.Request.Source.ExtractedValues is { Count: > 0 },
            SourceType = reference is null ? VerificationSourceType.ExtractedValues : VerificationSourceType.DocumentAndExtractedValues
        };

        var record = IdentityVerificationMapper.CreateComparisonRecord(
            VerificationOperationType.PanNameVerification,
            result,
            request.ActorReference,
            c.CorrelationId,
            request.Request.Source.SessionId,
            sourceSummary,
            IdentityVerificationRules.MaskIdentifier(value));

        await repository.AppendRecordAsync(record, ct);
        return IdentityVerificationMapper.ToComparisonResult(record);
    }
}

public sealed class VerifyAadharAddressCommandHandler(
    IComparisonAdapterResolver adapters,
    IIdentityVerificationRepository repository,
    IProtectedDocumentStore store,
    IRequestContextAccessor context,
    IProviderCallExecutor executor) : IRequestHandler<VerifyAadharAddressCommand, ComparisonVerificationResult>
{
    public async Task<ComparisonVerificationResult> Handle(VerifyAadharAddressCommand request, CancellationToken ct)
    {
        var c = context.Current;
        var value = IdentityVerificationRules.RequireText(request.Request.SubmittedAddress, "SubmittedAddress");
        
        var adapter = adapters.Resolve(VerificationOperationType.AadhaarAddressVerification, request.Request.Source.MappingProfile ?? "");
        
        ProtectedDocumentReference? reference = null;
        if (request.Request.Source.ProtectedDocumentReferenceId is not null)
        {
            reference = await repository.GetProtectedDocumentReferenceAsync(request.Request.Source.ProtectedDocumentReferenceId.Value, ct)
                ?? throw new IdentityVerificationRecordNotFoundException();
        }

        ComparisonAdapterResult result;
        await using (var stream = reference is null
            ? null
            : await store.OpenReadAsync(reference.StorageKey, ct)
                ?? throw new IdentityVerificationRecordNotFoundException())
        {
            result = await executor.ExecuteAsync(
                t => adapter.VerifyAsync(
                    new ProviderNeutralComparisonInput(
                        VerificationOperationType.AadhaarAddressVerification,
                        value,
                        reference?.Id,
                        stream,
                        request.Request.Source.ExtractedValues ?? new Dictionary<string, string>(),
                        c.CorrelationId,
                        request.Request.Source.MappingProfile ?? ""),
                    t),
                VerificationOperationType.AadhaarAddressVerification,
                c.CorrelationId,
                ct);
        }

        var sourceSummary = new SourceSummary
        {
            MappingProfile = request.Request.Source.MappingProfile,
            DocumentProvided = reference is not null,
            DocumentContentType = reference?.ContentType,
            DocumentSizeBytes = reference?.SizeBytes,
            ProtectedDocumentReferenceId = reference?.Id,
            ExtractionCompleted = request.Request.Source.ExtractedValues is { Count: > 0 },
            SourceType = reference is null ? VerificationSourceType.ExtractedValues : VerificationSourceType.DocumentAndExtractedValues
        };

        var record = IdentityVerificationMapper.CreateComparisonRecord(
            VerificationOperationType.AadhaarAddressVerification,
            result,
            request.ActorReference,
            c.CorrelationId,
            request.Request.Source.SessionId,
            sourceSummary,
            null);

        await repository.AppendRecordAsync(record, ct);
        return IdentityVerificationMapper.ToComparisonResult(record);
    }
}
