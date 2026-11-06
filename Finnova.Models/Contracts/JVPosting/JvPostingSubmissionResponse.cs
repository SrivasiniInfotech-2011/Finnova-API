namespace Finnova.Models.Contracts.JVPosting;

public sealed record JvPostingSubmissionResponse(
    Guid RequestId,
    string Status,
    bool ExistingEquivalentRequest);
