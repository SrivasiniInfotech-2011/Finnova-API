using Finnova.Models.Contracts.JVPosting;
using Finnova.Service.JVPosting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finnova.SystemAdminService.Controllers;

[ApiController]
[Route("api/jv-posting/operations")]
[Authorize(Policy = "JveOperationsRead")]
public sealed class JvPostingOperationsController(JvPostingOperationsService service) : ControllerBase
{
    [HttpGet("queue-status")]
    public Task<IReadOnlyList<JvPostingQueueBucket>> QueueStatus(Guid? companyId, Guid? branchId, CancellationToken ct) => service.QueueStatusAsync(companyId, branchId, ct);

    [HttpGet("requests/{requestId:guid}")]
    public Task<JvPostingDetailResponse?> Detail(Guid requestId, CancellationToken ct) => service.DetailAsync(requestId, ct);

    [HttpPost("requests/{requestId:guid}/recover-expired-lease")]
    [Authorize(Policy = "JveOperationsRecovery")]
    public Task<IActionResult> RecoverExpiredLease(Guid requestId, [FromBody] JvPostingRecoveryRequest request, CancellationToken ct) => throw new NotImplementedException("Bind to compare-and-set JVE recovery service.");

    [HttpPost("requests/{requestId:guid}/reprocess")]
    [Authorize(Policy = "JveOperationsRecovery")]
    public Task<IActionResult> Reprocess(Guid requestId, [FromBody] JvPostingRecoveryRequest request, CancellationToken ct) => throw new NotImplementedException("Bind to compare-and-set JVE recovery service.");

    [HttpPost("requests/{requestId:guid}/cancel")]
    [Authorize(Policy = "JveOperationsRecovery")]
    public Task<IActionResult> Cancel(Guid requestId, [FromBody] JvPostingRecoveryRequest request, CancellationToken ct) => throw new NotImplementedException("Bind to pre-journal cancellation service.");
}
