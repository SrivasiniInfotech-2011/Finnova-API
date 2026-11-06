using Finnova.Models.Contracts.IdentityVerification;
using Finnova.Service.IdentityVerification.Commands.VerifyAadharAddress;
using Finnova.Service.IdentityVerification.Commands.VerifyPanName;

namespace Finnova.Service.IdentityVerification.Workflow;

public interface ILegacyVerificationWorkflowCoordinator
{
    Task<VerificationWorkflowResponse> RunPanAndNameWorkflowAsync(PanAndNameWorkflowRequest request, CancellationToken ct);
    Task<VerificationWorkflowResponse> RunAadhaarAndAddressWorkflowAsync(AadhaarAndAddressWorkflowRequest request, CancellationToken ct);
    Task<VerificationWorkflowResponse> RunFullWorkflowAsync(FullVerificationWorkflowRequest request, CancellationToken ct);
}
