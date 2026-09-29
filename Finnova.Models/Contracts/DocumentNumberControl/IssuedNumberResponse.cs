namespace Finnova.Models.Contracts.DocumentNumberControl;

/// <summary>The result of an issuance: the formatted number and the raw sequence value used (R5.1).</summary>
public record IssuedNumberResponse(
    Guid SchemeId,
    string DocumentType,
    string Number,
    long SequenceValue,
    DateTime IssuedAtUtc);
