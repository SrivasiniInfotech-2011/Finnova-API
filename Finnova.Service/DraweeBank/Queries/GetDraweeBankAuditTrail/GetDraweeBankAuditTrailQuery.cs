using MediatR;
using Finnova.Models.Contracts.DraweeBanks;

namespace Finnova.Service.DraweeBank.Queries.GetDraweeBankAuditTrail;

/// <summary>Reads the full audit trail for one drawee bank, newest first (R7.4). A missing id
/// yields an empty list rather than an error (R7.5).</summary>
public record GetDraweeBankAuditTrailQuery(
    Guid DraweeBankId
) : IRequest<List<DraweeBankAuditEntryResponse>>;
