using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Contracts.Internal;

public sealed record InternalIssueRequest(string DocumentType, Guid LineOfBusinessId, Guid BranchId, string CorrelationId);
public sealed record InternalIssueResponse(string IssuedNumber, Guid ResolvedSchemeId, string ResolvedSchemeCode, string ResolvedScopeLevel);

public sealed record CustomerNameParts(string? First, string? Second, string? Third, string? Fourth, string? Fifth, string? Sixth);
public sealed record CustomerAddressParts(string? House, string? Block, string? Way, string? Area, string? Landmark,
    string? City, string? State, string? PostalCode, string? Country);
public sealed record MappedVerificationValues(string Name, string Address);

public sealed record InternalKycRequest(Guid CustomerId, string? Pan, string? Aadhaar,
    CustomerNameParts Name, CustomerAddressParts? Address, string CorrelationId, string Mode);

public sealed record InternalKycResponse(
    string PanValidation, string PanNameMatch, string AadhaarValidation, string AadhaarAddressMatch,
    Guid? VerificationSessionId, string? ProviderReference, string? SafeReason, DateTime TimestampUtc);

public sealed record ServiceTokenRequest(Guid ClientId, string ClientSecret);
public sealed record ServiceTokenResponse(string AccessToken, int ExpiresInSeconds);

/// <summary>Provider-neutral request to a KYC aggregator (no provider-specific fields).</summary>
public sealed record ProviderNeutralKycRequest(Guid CustomerId, string? Pan, string? Aadhaar,
    string CustomerName, string Address, string CorrelationId);

/// <summary>Provider-independent aggregator result.</summary>
public sealed record NormalizedKycResult(
    KycCheckResult PanValidation, KycCheckResult PanNameMatch,
    KycCheckResult AadhaarValidation, KycCheckResult AadhaarAddressMatch,
    KycCheckResult Overall, string? ProviderReference, string? SafeReason, DateTime TimestampUtc);