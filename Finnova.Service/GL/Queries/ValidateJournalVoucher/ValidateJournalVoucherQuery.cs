using Finnova.Models.Contracts.GL;
using MediatR;

namespace Finnova.Service.GL.Queries.ValidateJournalVoucher;

/// <summary>In-process validation request issued by the JV posting engine (FINNOVA-25). No public endpoint.</summary>
public record ValidateJournalVoucherQuery(ValidateJournalVoucherInput Input)
    : IRequest<VoucherValidationResult>;
