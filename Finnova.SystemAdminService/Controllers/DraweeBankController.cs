using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.DraweeBanks;
using Finnova.Service.DraweeBank.Commands.CreateDraweeBank;
using Finnova.Service.DraweeBank.Commands.UpdateDraweeBank;
using Finnova.Service.DraweeBank.Commands.CreateChallanRule;
using Finnova.Service.DraweeBank.Commands.UpdateChallanRule;
using Finnova.Service.DraweeBank.Queries.GetDraweeBanksPaged;
using Finnova.Service.DraweeBank.Queries.GetDraweeBankAuditTrail;
using Finnova.Service.DraweeBank.Queries.GetChallanRulesByBankId;

namespace Finnova.SystemAdminService.Controllers;

/// <summary>
/// Drawee Bank &amp; Challan Rules Master endpoints (FINNOVA-16). Every action is SystemAdmin-only
/// (R9.1-R9.3, read included). All work flows through MediatR so the existing ValidationBehavior
/// pipeline runs before each handler.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DraweeBankController : ControllerBase
{
    private readonly IMediator _mediator;
    public DraweeBankController(IMediator mediator) => _mediator = mediator;

    /// <summary>Paged, filtered admin list (R8). SystemAdmin only.</summary>
    [HttpGet]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(PaginatedResponse<DraweeBankResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedResponse<DraweeBankResponse>>> GetPaged(
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await _mediator.Send(new GetDraweeBanksPagedQuery(search, page, pageSize)));

    /// <summary>Create a drawee bank aggregate (R1-R4). SystemAdmin only.</summary>
    [HttpPost]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(DraweeBankResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DraweeBankResponse>> Create([FromBody] CreateDraweeBankRequest r)
    {
        var result = await _mediator.Send(new CreateDraweeBankCommand(
            r.BankCode, r.BankName, r.IsActive, r.Branches, r.Restriction, GetActingAdmin()));
        return CreatedAtAction(nameof(GetPaged), new { search = result.BankCode }, result);
    }

    /// <summary>Update a drawee bank's name, branches, and restriction (R5, R3, R4). 404 when missing.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(DraweeBankResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DraweeBankResponse>> Update(Guid id, [FromBody] UpdateDraweeBankRequest r)
        => Ok(await _mediator.Send(new UpdateDraweeBankCommand(
            id, r.BankName, r.Branches, r.Restriction, GetActingAdmin())));

    /// <summary>Read a bank's challan rules (R6). Decoupled from the bank response. SystemAdmin only.
    /// Always 200 with a possibly-empty list.</summary>
    [HttpGet("{id:guid}/challan-rules")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(List<ChallanRuleResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ChallanRuleResponse>>> GetChallanRules(Guid id)
        => Ok(await _mediator.Send(new GetChallanRulesByBankIdQuery(id)));

    /// <summary>Create a challan rule under a bank (R6.1-R6.4, R6.7). SystemAdmin only.</summary>
    [HttpPost("{id:guid}/challan-rules")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(ChallanRuleResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ChallanRuleResponse>> CreateChallanRule(
        Guid id, [FromBody] CreateChallanRuleRequest r)
    {
        var result = await _mediator.Send(new CreateChallanRuleCommand(
            id, r.RuleCode, r.FormatPattern, r.ValidationExpression, r.RoutingTarget, r.IsActive, GetActingAdmin()));
        return CreatedAtAction(nameof(GetPaged), new { search = result.RuleCode }, result);
    }

    /// <summary>Update a challan rule (R6.5, R6.6). 404 when the rule id is missing.</summary>
    [HttpPut("{id:guid}/challan-rules/{ruleId:guid}")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(ChallanRuleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChallanRuleResponse>> UpdateChallanRule(
        Guid id, Guid ruleId, [FromBody] UpdateChallanRuleRequest r)
        => Ok(await _mediator.Send(new UpdateChallanRuleCommand(
            id, ruleId, r.FormatPattern, r.ValidationExpression, r.RoutingTarget, r.IsActive, GetActingAdmin())));

    /// <summary>Audit trail for a bank, newest first (R7.4). Missing id yields [] (R7.5).</summary>
    [HttpGet("{id:guid}/audit")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(List<DraweeBankAuditEntryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<DraweeBankAuditEntryResponse>>> GetAuditTrail(Guid id)
        => Ok(await _mediator.Send(new GetDraweeBankAuditTrailQuery(id)));

    /// <summary>
    /// Resolves the acting administrator id from the validated JWT principal so the audit actor
    /// cannot be spoofed by the request body. Prefers NameIdentifier (sub), falls back to Name.
    /// </summary>
    private string GetActingAdmin()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? User.FindFirstValue("sub")
           ?? User.FindFirstValue(ClaimTypes.Name)
           ?? User.Identity?.Name
           ?? string.Empty;
}
