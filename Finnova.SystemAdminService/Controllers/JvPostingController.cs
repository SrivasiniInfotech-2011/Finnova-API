using Finnova.Models.Contracts.JVPosting;
using Finnova.Service.JVPosting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finnova.SystemAdminService.Controllers;

[ApiController]
[Route("api/jv-posting")]
[Authorize]
public sealed class JvPostingController(JvPostingSubmissionService service) : ControllerBase
{
    [HttpPost("requests")]
    public async Task<ActionResult<JvPostingSubmissionResponse>> Submit(
        [FromBody] JvPostingSubmissionRequest request,
        CancellationToken cancellationToken)
        => Ok(await service.SubmitAsync(request, User, cancellationToken));
}
