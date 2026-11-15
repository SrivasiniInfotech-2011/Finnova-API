using Finnova.Models.Contracts.IdentityVerification;
using Finnova.Models.Contracts.Internal;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Service.IdentityVerification.Mapping;
using Finnova.Service.IdentityVerification.Workflow;

namespace Finnova.Service.LoanAccounts.Kyc;

/// <summary>Provider-neutral aggregator contract. Provider-specific fields stay inside adapters.</summary>
public interface IKycAggregatorProvider
{
    Task<NormalizedKycResult> VerifyAsync(ProviderNeutralKycRequest request, CancellationToken ct);
}

/// <summary>Default registration: no provider configured (R7.6). Aggregator mode then fails with ERR-KYC-503.</summary>
public sealed class NullKycAggregatorProvider : IKycAggregatorProvider
{
    public Task<NormalizedKycResult> VerifyAsync(ProviderNeutralKycRequest request, CancellationToken ct)
        => throw new KycAggregatorNotConfiguredException();
}

public interface IKycVerificationFacade
{
    Task<InternalKycResponse> VerifyAsync(InternalKycRequest request, CancellationToken ct);
}

public sealed class KycVerificationFacade : IKycVerificationFacade
{
    private readonly IVerificationWorkflowCoordinator _coordinator;
    private readonly IKycAggregatorProvider _aggregator;

    public KycVerificationFacade(IVerificationWorkflowCoordinator coordinator, IKycAggregatorProvider aggregator)
        => (_coordinator, _aggregator) = (coordinator, aggregator);

    public async Task<InternalKycResponse> VerifyAsync(InternalKycRequest r, CancellationToken ct)
    {
        var mapped = CustomerMasterMappingProfile.Map(r.Name, r.Address);
        var pan = string.IsNullOrWhiteSpace(r.Pan) ? null : r.Pan.Trim();
        var aadhaar = string.IsNullOrWhiteSpace(r.Aadhaar) ? null : r.Aadhaar.Trim();

        if (string.Equals(r.Mode, nameof(KycMode.Aggregator), StringComparison.OrdinalIgnoreCase))
        {
            var n = await _aggregator.VerifyAsync(new ProviderNeutralKycRequest(
                r.CustomerId, pan, aadhaar, mapped.Name, mapped.Address, r.CorrelationId), ct);
            return new InternalKycResponse(n.PanValidation.ToString(), n.PanNameMatch.ToString(),
                n.AadhaarValidation.ToString(), n.AadhaarAddressMatch.ToString(), null, n.ProviderReference, n.SafeReason, n.TimestampUtc);
        }

        // Internal mode. A check is run only when its inputs exist; the rest are reported as NotChecked (R5.7, R17.4).
        var panPart = pan is not null && mapped.Name.Length > 0;
        var aadhaarPart = aadhaar is not null && mapped.Address.Length > 0;
        if (!panPart && !aadhaarPart)
        {
            var nc = KycCheckResult.NotChecked.ToString();
            return new InternalKycResponse(nc, nc, nc, nc, null, null, "No verifiable identity data.", DateTime.UtcNow);
        }

        ComparisonSourceContract Source() => new(null, null, null,
            new Dictionary<string, string> { ["documentName"] = mapped.Name, ["documentAddress"] = mapped.Address },
            CustomerMasterMappingProfile.Name);

        var type = panPart && aadhaarPart ? VerificationWorkflowType.Full
                 : panPart ? VerificationWorkflowType.PanAndName : VerificationWorkflowType.AadhaarAndAddress;
        var res = await _coordinator.ExecuteAsync(new VerificationWorkflowRequest(
            type, WorkflowInputMode.Partial,
            panPart ? pan : null, panPart ? mapped.Name : null, panPart ? Source() : null,
            aadhaarPart ? aadhaar : null, aadhaarPart ? mapped.Address : null, aadhaarPart ? Source() : null), ct);

        KycCheckResult Resolve(bool requested, KycCheckResult? value, VerificationOperationType op) =>
            !requested ? KycCheckResult.NotChecked
            : value is { } v ? v
            : res.PartialFailures.Any(f => f.OperationType == op) ? KycCheckResult.Unavailable : KycCheckResult.NotChecked;

        return new InternalKycResponse(
            Resolve(panPart, res.PanIdentifierResult is null ? null : KycResultMapper.FromValidation(res.PanIdentifierResult.ValidationStatus), VerificationOperationType.PanIdentifierValidation).ToString(),
            Resolve(panPart, res.PanNameResult is null ? null : KycResultMapper.FromComparison(res.PanNameResult.ComparisonOutcome), VerificationOperationType.PanNameVerification).ToString(),
            Resolve(aadhaarPart, res.AadhaarIdentifierResult is null ? null : KycResultMapper.FromValidation(res.AadhaarIdentifierResult.ValidationStatus), VerificationOperationType.AadhaarIdentifierValidation).ToString(),
            Resolve(aadhaarPart, res.AadhaarAddressResult is null ? null : KycResultMapper.FromComparison(res.AadhaarAddressResult.ComparisonOutcome), VerificationOperationType.AadhaarAddressVerification).ToString(),
            res.SessionId, null, res.PartialFailures.Count == 0 ? null : "Some checks could not be completed.", res.TimestampUtc);
    }
}
