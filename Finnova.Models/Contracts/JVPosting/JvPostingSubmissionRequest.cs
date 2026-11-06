using System.Text.Json;

namespace Finnova.Models.Contracts.JVPosting;

public sealed record JvPostingSubmissionRequest(
    Guid RequestId,
    string SourceSystem,
    string? EventType,
    string? RuleKey,
    Guid CompanyId,
    Guid BranchId,
    DateOnly ValueDate,
    DateOnly? PostingDate,
    string? CurrencyCode,
    string? Description,
    JsonElement Data,
    string InitiationMode,
    Guid? InitiatingUserId,
    string? AutomationContext);
