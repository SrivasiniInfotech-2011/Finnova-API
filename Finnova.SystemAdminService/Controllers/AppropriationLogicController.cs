using System.Security.Claims;
using Finnova.Models.Contracts.AppropriationLogic;
using Finnova.Service.AppropriationLogic;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finnova.SystemAdminService.Controllers;

[ApiController]
[Route("api/appropriation-logic")]
[Authorize(Policy = "AppropriationLogicRead")]
public sealed class AppropriationLogicController(IMediator mediator) : ControllerBase
{
    private string Actor => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? User.Identity?.Name ?? "authenticated-user";
    private string Correlation => Request.Headers.TryGetValue("X-Correlation-Id", out var value) && !string.IsNullOrWhiteSpace(value) ? value.ToString() : HttpContext.TraceIdentifier;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AppropriationLogicListItem>>> List([FromQuery] Guid? companyId, [FromQuery] Guid? lineOfBusinessId, [FromQuery] bool includeInactive = false, CancellationToken ct = default) => Ok(await mediator.Send(new ListAppropriationLogicQuery(companyId, lineOfBusinessId, includeInactive, User), ct));

    [HttpGet("{ruleId:guid}")]
    public async Task<ActionResult<AppropriationLogicResponse>> Get(Guid ruleId, CancellationToken ct) => Ok(await mediator.Send(new GetAppropriationLogicQuery(ruleId, User), ct));

    [HttpPost]
    [Authorize(Policy = "AppropriationLogicManage")]
    public async Task<ActionResult<AppropriationLogicResponse>> Create(CreateAppropriationLogicRequest request, CancellationToken ct) => StatusCode(StatusCodes.Status201Created, await mediator.Send(new CreateAppropriationLogicCommand(request, User, Actor, Correlation), ct));

    [HttpGet("resolve")]
    [AllowAnonymous]
    public async Task<ActionResult<AppropriationResolutionResponse>> Resolve([FromQuery] Guid companyId, [FromQuery] Guid lineOfBusinessId, CancellationToken ct) => Ok(await mediator.Send(new ResolveAppropriationLogicQuery(companyId, lineOfBusinessId), ct));
}