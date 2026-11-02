using Finnova.Models.Contracts.IdentityVerification;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.IdentityVerification.Adapters;
using Finnova.Service.IdentityVerification.Correlation;
using Finnova.Service.IdentityVerification.External;
using MediatR;

namespace Finnova.Service.IdentityVerification.Commands.ValidateIdentifiers;

public sealed record ValidatePanIdentifierCommand(string Pan, Guid? SessionId, string ActorReference) : IRequest<IdentifierValidationResult>;

public sealed record ValidateAadhaarIdentifierCommand(string AadhaarNumber, Guid? SessionId, string ActorReference) : IRequest<IdentifierValidationResult>;

public sealed class ValidatePanIdentifierCommandHandler(
    IEnumerable<IIdentifierValidationAdapter> adapters,
    IIdentityVerificationRepository repository,
    IRequestContextAccessor context,
    IProviderCallExecutor executor)
    : IRequestHandler<ValidatePanIdentifierCommand, IdentifierValidationResult>
{
    public Task<IdentifierValidationResult> Handle(
        ValidatePanIdentifierCommand request,
        CancellationToken ct) =>
        HandleCoreAsync(
            request.Pan,
            VerificationOperationType.PanIdentifierValidation,
            request.SessionId,
            request.ActorReference,
            IdentityVerificationRules.NormalizePan,
            ct);

    private async Task<IdentifierValidationResult> HandleCoreAsync(
        string rawIdentifier,
        VerificationOperationType operation,
        Guid? sessionId,
        string actorReference,
        Func<string, string> normalize,
        CancellationToken ct)
    {
        var requestContext = context.Current;
        var identifier = normalize(rawIdentifier);
        var adapter = ResolveAdapter(adapters, operation);
        var result = await ExecuteAsync(
            adapter,
            operation,
            identifier,
            actorReference,
            requestContext,
            executor,
            ct);

        var record = CreateRecord(
            operation,
            result,
            sessionId,
            actorReference,
            requestContext.CorrelationId,
            identifier);

        await repository.AppendRecordAsync(record, ct);
        return IdentityVerificationMapper.ToIdentifierResult(record);
    }

    private static Task<IdentifierAdapterResult> ExecuteAsync(
        IIdentifierValidationAdapter adapter,
        VerificationOperationType operation,
        string identifier,
        string actorReference,
        RequestContext requestContext,
        IProviderCallExecutor executor,
        CancellationToken ct) =>
        ExecuteWithUnavailableResultAsync(
            adapter,
            operation,
            identifier,
            actorReference,
            requestContext,
            executor,
            ct);

    private static async Task<IdentifierAdapterResult> ExecuteWithUnavailableResultAsync(
        IIdentifierValidationAdapter adapter,
        VerificationOperationType operation,
        string identifier,
        string actorReference,
        RequestContext requestContext,
        IProviderCallExecutor executor,
        CancellationToken ct)
    {
        try
        {
            return await executor.ExecuteAsync(
                providerToken => adapter.ValidateAsync(
                    new IdentifierValidationInput
                    {
                        OperationType = operation,
                        Identifier = identifier,
                        CorrelationId = requestContext.CorrelationId,
                        ActorReference = actorReference,
                        CancellationToken = providerToken
                    },
                    providerToken),
                operation,
                requestContext.CorrelationId,
                ct);
        }
        catch (IdentityVerificationProviderUnavailableException)
        {
            return new IdentifierAdapterResult.Unavailable(
                "ProviderUnavailable",
                "The verification provider is temporarily unavailable.");
        }
    }

    private static IIdentifierValidationAdapter ResolveAdapter(
        IEnumerable<IIdentifierValidationAdapter> adapters,
        VerificationOperationType operation)
    {
        var matchingAdapters = adapters
            .Where(adapter => adapter.SupportedOperation == operation)
            .Take(2)
            .ToArray();

        if (matchingAdapters.Length != 1)
        {
            throw new IdentityVerificationConfigurationException();
        }

        return matchingAdapters[0];
    }

    private static ValidationRecord CreateRecord(
        VerificationOperationType operation,
        IdentifierAdapterResult result,
        Guid? sessionId,
        string actorReference,
        string correlationId,
        string identifier) =>
        new()
        {
            SessionId = sessionId,
            CorrelationId = correlationId,
            ActorReference = actorReference,
            OperationType = operation,
            ValidationStatus = result.Status,
            Method = VerificationMethod.Auto,
            SafeMessage = result.SafeMessage,
            SafeReasonCode = result.SafeReasonCode,
            MaskedSubjectReference = IdentityVerificationRules.MaskIdentifier(identifier)
        };
}

public sealed class ValidateAadhaarIdentifierCommandHandler(
    IEnumerable<IIdentifierValidationAdapter> adapters,
    IIdentityVerificationRepository repository,
    IRequestContextAccessor context,
    IProviderCallExecutor executor)
    : IRequestHandler<ValidateAadhaarIdentifierCommand, IdentifierValidationResult>
{
    public async Task<IdentifierValidationResult> Handle(
        ValidateAadhaarIdentifierCommand request,
        CancellationToken ct)
    {
        var requestContext = context.Current;
        var identifier = IdentityVerificationRules.NormalizeAadhaar(request.AadhaarNumber);
        var operation = VerificationOperationType.AadhaarIdentifierValidation;
        var adapter = ResolveAdapter(adapters, operation);
        IdentifierAdapterResult result;

        try
        {
            result = await executor.ExecuteAsync(
                providerToken => adapter.ValidateAsync(
                    new IdentifierValidationInput
                    {
                        OperationType = operation,
                        Identifier = identifier,
                        CorrelationId = requestContext.CorrelationId,
                        ActorReference = request.ActorReference,
                        CancellationToken = providerToken
                    },
                    providerToken),
                operation,
                requestContext.CorrelationId,
                ct);
        }
        catch (IdentityVerificationProviderUnavailableException)
        {
            result = new IdentifierAdapterResult.Unavailable(
                "ProviderUnavailable",
                "The verification provider is temporarily unavailable.");
        }

        var record = new ValidationRecord
        {
            SessionId = request.SessionId,
            CorrelationId = requestContext.CorrelationId,
            ActorReference = request.ActorReference,
            OperationType = operation,
            ValidationStatus = result.Status,
            Method = VerificationMethod.Auto,
            SafeMessage = result.SafeMessage,
            SafeReasonCode = result.SafeReasonCode,
            MaskedSubjectReference = IdentityVerificationRules.MaskIdentifier(identifier)
        };

        await repository.AppendRecordAsync(record, ct);
        return IdentityVerificationMapper.ToIdentifierResult(record);
    }

    private static IIdentifierValidationAdapter ResolveAdapter(
        IEnumerable<IIdentifierValidationAdapter> adapters,
        VerificationOperationType operation)
    {
        var matchingAdapters = adapters
            .Where(adapter => adapter.SupportedOperation == operation)
            .Take(2)
            .ToArray();

        if (matchingAdapters.Length != 1)
        {
            throw new IdentityVerificationConfigurationException();
        }

        return matchingAdapters[0];
    }
}
