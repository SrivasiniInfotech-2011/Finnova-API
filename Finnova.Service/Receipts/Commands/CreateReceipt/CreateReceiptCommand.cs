using Finnova.Models.Contracts.Receipts;
using MediatR;

namespace Finnova.Service.Receipts.Commands.CreateReceipt;

/// <summary>Actor and correlation id come from the validated token and the HTTP context, never the body (R14.4).</summary>
public sealed record CreateReceiptCommand(CreateReceiptRequest Request, string Actor, string CorrelationId)
    : IRequest<CreateReceiptResult>;

/// <summary>AlreadyExisted is true when the ClientRequestId matched a saved receipt (HTTP 200 instead of 201).</summary>
public sealed record CreateReceiptResult(ReceiptResponse Receipt, bool AlreadyExisted);