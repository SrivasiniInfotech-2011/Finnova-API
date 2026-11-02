using Finnova.Models.Contracts.IdentityVerification;
using MediatR;

namespace Finnova.Service.IdentityVerification.Commands.VerifyPanName;

public sealed record VerifyPanNameCommand(VerifyPanNameRequest Request, string ActorReference) : IRequest<ComparisonVerificationResult>;
