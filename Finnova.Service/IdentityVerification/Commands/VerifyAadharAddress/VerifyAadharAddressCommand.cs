using Finnova.Models.Contracts.IdentityVerification;
using MediatR;

namespace Finnova.Service.IdentityVerification.Commands.VerifyAadharAddress;

public sealed record VerifyAadharAddressCommand(VerifyAadharAddressRequest Request, string ActorReference) : IRequest<ComparisonVerificationResult>;
