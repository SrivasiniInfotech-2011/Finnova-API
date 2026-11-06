using System.Text.Json;
using Finnova.Models.Contracts.IdentityVerification;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.IdentityVerification.Adapters;
using Finnova.Service.IdentityVerification.Correlation;
using Finnova.Service.IdentityVerification.External;
using Finnova.Service.IdentityVerification.Storage;

namespace Finnova.Service.IdentityVerification.Workflow;

public interface IVerificationWorkflowCoordinator
{
    Task<VerificationWorkflowResponse> ExecuteAsync(VerificationWorkflowRequest request, CancellationToken ct);
}

public sealed class VerificationWorkflowCoordinator(
    IIdentityVerificationRepository repository,
    IProtectedDocumentStore store,
    IProviderCallExecutor executor,
    IEnumerable<IIdentifierValidationAdapter> identifiers,
    IComparisonAdapterResolver comparisons,
    IRequestContextAccessor context) : IVerificationWorkflowCoordinator
{
    public async Task<VerificationWorkflowResponse> ExecuteAsync(VerificationWorkflowRequest request, CancellationToken ct)
    {
        var c = context.Current;
        ValidateWorkflowShape(request);

        var session = new VerificationSession
        {
            CorrelationId = c.CorrelationId,
            ActorReference = c.ActorReference,
            WorkflowType = request.WorkflowType,
            InputMode = request.InputMode,
            RequestedOperationsJson = JsonSerializer.Serialize(GetOperations(request))
        };

        await repository.AddSessionAsync(session, ct);

        var failures = new List<WorkflowFailureSummary>();
        IdentifierValidationResult? pan = null, aadhaar = null;
        ComparisonVerificationResult? panName = null, address = null;

        var jobs = new List<Task>
        {
            RunIdentifier(VerificationOperationType.PanIdentifierValidation, request.Pan, session, x => pan = x, failures, c, ct),
            RunComparison(VerificationOperationType.PanNameVerification, request.PanSubmittedName, request.PanSource, session, x => panName = x, failures, c, ct),
            RunIdentifier(VerificationOperationType.AadhaarIdentifierValidation, request.AadhaarNumber, session, x => aadhaar = x, failures, c, ct),
            RunComparison(VerificationOperationType.AadhaarAddressVerification, request.AadhaarSubmittedAddress, request.AadhaarSource, session, x => address = x, failures, c, ct)
        };

        await Task.WhenAll(jobs);

        return new VerificationWorkflowResponse(session.Id, c.CorrelationId, DateTime.UtcNow, pan, panName, aadhaar, address, failures);
    }

    private async Task RunIdentifier(
        VerificationOperationType op,
        string? raw,
        VerificationSession s,
        Action<IdentifierValidationResult> set,
        List<WorkflowFailureSummary> failures,
        RequestContext c,
        CancellationToken ct)
    {
        if (raw is null) return;

        try
        {
            var id = op == VerificationOperationType.PanIdentifierValidation 
                ? IdentityVerificationRules.NormalizePan(raw) 
                : IdentityVerificationRules.NormalizeAadhaar(raw);
            
            var a = identifiers.Single(x => x.SupportedOperation == op);
            
            var result = await executor.ExecuteAsync(
                t => a.ValidateAsync(new(op, id, c.CorrelationId), t),
                op,
                c.CorrelationId,
                ct);

            var record = new ValidationRecord
            {
                SessionId = s.Id,
                CorrelationId = c.CorrelationId,
                ActorReference = c.ActorReference,
                OperationType = op,
                ValidationStatus = result.Status,
                Method = VerificationMethod.Auto,
                SafeMessage = result.SafeMessage,
                SafeReasonCode = result.SafeReasonCode,
                MaskedSubjectReference = IdentityVerificationRules.MaskIdentifier(id)
            };

            await repository.AppendRecordAsync(record, ct);
            set(IdentityVerificationMapper.ToIdentifierResult(record));
        }
        catch (IdentityVerificationProviderUnavailableException ex)
        {
            failures.Add(new(op, "ERR-IVF-503", ex.Message));
        }
        catch (IdentityVerificationValidationException)
        {
            failures.Add(new(op, "ERR-IVF-400", "The identifier input is invalid."));
        }
    }

    private async Task RunComparison(
        VerificationOperationType op,
        string? raw,
        ComparisonSourceContract? source,
        VerificationSession s,
        Action<ComparisonVerificationResult> set,
        List<WorkflowFailureSummary> failures,
        RequestContext c,
        CancellationToken ct)
    {
        if (raw is null || source is null) return;

        try
        {
            var value = IdentityVerificationRules.RequireText(
                raw,
                op == VerificationOperationType.PanNameVerification ? "PanSubmittedName" : "AadhaarSubmittedAddress");

            var a = comparisons.Resolve(op, source.MappingProfile ?? "");

            var reference = source.ProtectedDocumentReferenceId is null 
                ? null 
                : await repository.GetProtectedDocumentReferenceAsync(source.ProtectedDocumentReferenceId.Value, ct) ?? 
                  throw new IdentityVerificationValidationException(["ProtectedDocumentReferenceId"]);

            await using var stream = reference is null ? null : await store.OpenReadAsync(reference.StorageKey, ct);

            var result = await executor.ExecuteAsync(
                t => a.VerifyAsync(
                    new(op, value, null, stream, source.ExtractedValues ?? new Dictionary<string, string>(), c.CorrelationId, source.MappingProfile ?? ""), 
                    t),
                op,
                c.CorrelationId,
                ct);

            var record = IdentityVerificationMapper.CreateComparisonRecord(
                op, result, c.ActorReference, c.CorrelationId, s.Id, new(), null);

            await repository.AppendRecordAsync(record, ct);
            set(IdentityVerificationMapper.ToComparisonResult(record));
        }
        catch (IdentityVerificationProviderUnavailableException ex)
        {
            failures.Add(new(op, "ERR-IVF-503", ex.Message));
        }
        catch (IdentityVerificationValidationException)
        {
            failures.Add(new(op, "ERR-IVF-400", "The comparison input is invalid."));
        }
    }

    private static void ValidateWorkflowShape(VerificationWorkflowRequest r)
    {
        if (r.InputMode == WorkflowInputMode.Atomic &&
            (r.WorkflowType == VerificationWorkflowType.Full &&
            (r.Pan is null || r.PanSubmittedName is null || r.AadhaarNumber is null || r.AadhaarSubmittedAddress is null)))
        {
            throw new IdentityVerificationValidationException(["Workflow"]);
        }
    }

    private static IReadOnlyList<VerificationOperationType> GetOperations(VerificationWorkflowRequest r)
    {
        return Enum.GetValues<VerificationOperationType>()
            .Where(x => (x == VerificationOperationType.PanIdentifierValidation && r.Pan is not null) ||
                       (x == VerificationOperationType.PanNameVerification && r.PanSubmittedName is not null) ||
                       (x == VerificationOperationType.AadhaarIdentifierValidation && r.AadhaarNumber is not null) ||
                       (x == VerificationOperationType.AadhaarAddressVerification && r.AadhaarSubmittedAddress is not null))
            .ToArray();
    }
}
