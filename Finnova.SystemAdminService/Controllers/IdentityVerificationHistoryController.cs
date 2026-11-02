using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.IdentityVerification;
using Finnova.Service.IdentityVerification.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finnova.SystemAdminService.Controllers;

[ApiController]
[Route("api/identity-verification")]
[Authorize(Policy = "SystemAdmin")]
public sealed class IdentityVerificationHistoryController(IMediator mediator, IIdentityVerificationDocumentQuery documents) : ControllerBase
{
    [HttpGet("records")]
    public Task<PaginatedResponse<ValidationRecordResponse>> Records([FromQuery] VerificationRecordQuery query)
        => mediator.Send(new GetVerificationRecordsQuery(query), HttpContext.RequestAborted);

    [HttpGet("documents/{recordId:guid}")]
    public async Task<IActionResult> Document(Guid recordId)
    {
        var file = await documents.GetAsync(recordId, HttpContext.RequestAborted);
        return File(file.Content, file.ContentType, file.FileName, enableRangeProcessing: false);
    }
}
