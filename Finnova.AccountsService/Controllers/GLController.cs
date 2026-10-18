using System.Security.Claims;
using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.GL;
using Finnova.Service.GL.Commands.AuthorizeGLAccount;
using Finnova.Service.GL.Commands.AuthorizeGLBatch;
using Finnova.Service.GL.Commands.CommitGLUpload;
using Finnova.Service.GL.Commands.CreateGLAccount;
using Finnova.Service.GL.Commands.DeactivateGLAccount;
using Finnova.Service.GL.Commands.DeleteGLAccount;
using Finnova.Service.GL.Commands.DryRunGLUpload;
using Finnova.Service.GL.Commands.EnableJvPosting;
using Finnova.Service.GL.Commands.FreezeGLAccount;
using Finnova.Service.GL.Commands.MapSystemGL;
using Finnova.Service.GL.Commands.RejectGLAccount;
using Finnova.Service.GL.Commands.RejectGLBatch;
using Finnova.Service.GL.Commands.SubmitGLAccount;
using Finnova.Service.GL.Commands.UpdateGLAccount;
using Finnova.Service.GL.Queries.ExportGLAccounts;
using Finnova.Service.GL.Queries.GetGLAccountById;
using Finnova.Service.GL.Queries.GetGLAccountsPaged;
using Finnova.Service.GL.Queries.GetGLAuditTrail;
using Finnova.Service.GL.Queries.GetGLTree;
using Finnova.Service.GL.Queries.GetSystemGLCompleteness;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finnova.AccountsService.Controllers;

/// <summary>
/// GL Master endpoints (FINNOVA-31..38). Writes require GLMaker, authorization requires
/// GLChecker, system-GL/JV-enable require SystemAdmin, reads require GLRead (any GL role). All
/// work flows through MediatR so the ValidationBehavior pipeline runs before each handler. The
/// acting user is resolved from the JWT, never the body (R11).
/// </summary>
[ApiController]
[Route("api/gl")]
public class GLController : ControllerBase
{
    private readonly IMediator _mediator;
    public GLController(IMediator mediator) => _mediator = mediator;

    // ---------- Reads (GLRead) ----------

    [HttpGet]
    [Authorize(Policy = "GLRead")]
    [ProducesResponseType(typeof(PaginatedResponse<GLAccountResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedResponse<GLAccountResponse>>> GetPaged([FromQuery] GLAccountFilter filter)
        => Ok(await _mediator.Send(new GetGLAccountsPagedQuery(filter)));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "GLRead")]
    [ProducesResponseType(typeof(GLAccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GLAccountResponse>> GetById(Guid id)
        => Ok(await _mediator.Send(new GetGLAccountByIdQuery(id)));

    [HttpGet("tree/{companyId:guid}")]
    [Authorize(Policy = "GLRead")]
    [ProducesResponseType(typeof(IReadOnlyList<GLTreeNodeResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<GLTreeNodeResponse>>> GetTree(Guid companyId)
        => Ok(await _mediator.Send(new GetGLTreeQuery(companyId)));

    [HttpGet("{id:guid}/audit")]
    [Authorize(Policy = "GLRead")]
    [ProducesResponseType(typeof(List<GLAuditEntryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<GLAuditEntryResponse>>> GetAudit(Guid id)
        => Ok(await _mediator.Send(new GetGLAuditTrailQuery(id)));

    [HttpGet("systemgl/{companyId:guid}/completeness")]
    [Authorize(Policy = "GLRead")]
    [ProducesResponseType(typeof(SystemGLCompletenessResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SystemGLCompletenessResponse>> GetCompleteness(Guid companyId)
        => Ok(await _mediator.Send(new GetSystemGLCompletenessQuery(companyId)));

    [HttpPost("export")]
    [Authorize(Policy = "GLRead")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Export([FromBody] ExportGLRequest request)
    {
        var (content, contentType, fileName) = await _mediator.Send(new ExportGLAccountsQuery(request));
        return File(content, contentType, fileName);
    }

    // ---------- Writes (GLMaker) ----------

    [HttpPost]
    [Authorize(Policy = "GLMaker")]
    [ProducesResponseType(typeof(GLAccountResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GLAccountResponse>> Create([FromBody] CreateGLAccountRequest r)
    {
        var result = await _mediator.Send(new CreateGLAccountCommand(r, GetActingUser()));
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "GLMaker")]
    [ProducesResponseType(typeof(GLAccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GLAccountResponse>> Update(Guid id, [FromBody] UpdateGLAccountRequest r)
        => Ok(await _mediator.Send(new UpdateGLAccountCommand(id, r, GetActingUser())));

    [HttpPost("{id:guid}/submit")]
    [Authorize(Policy = "GLMaker")]
    [ProducesResponseType(typeof(GLAccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GLAccountResponse>> Submit(Guid id)
        => Ok(await _mediator.Send(new SubmitGLAccountCommand(id, GetActingUser())));

    [HttpPost("{id:guid}/freeze")]
    [Authorize(Policy = "GLMaker")]
    [ProducesResponseType(typeof(GLAccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GLAccountResponse>> Freeze(Guid id, [FromBody] FreezeGLRequest r)
        => Ok(await _mediator.Send(new FreezeGLAccountCommand(id, r.Reason, GetActingUser())));

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Policy = "GLMaker")]
    [ProducesResponseType(typeof(GLAccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GLAccountResponse>> Deactivate(Guid id, [FromBody] DeactivateGLRequest r)
        => Ok(await _mediator.Send(new DeactivateGLAccountCommand(id, r.Reason, GetActingUser())));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "GLMaker")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _mediator.Send(new DeleteGLAccountCommand(id, GetActingUser()));
        return NoContent();
    }

    [HttpPost("bulk/dry-run")]
    [Authorize(Policy = "GLMaker")]
    [ProducesResponseType(typeof(GLUploadReportResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<GLUploadReportResponse>> DryRun([FromBody] GLBulkUploadRequest r)
    {
        var rows = r.Rows.Select(x => new GLUploadRow(x.RowNumber, x.Row)).ToList();
        return Ok(await _mediator.Send(new DryRunGLUploadCommand(r.CompanyId, rows, GetActingUser())));
    }

    [HttpPost("bulk/commit")]
    [Authorize(Policy = "GLMaker")]
    [ProducesResponseType(typeof(GLUploadReportResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<GLUploadReportResponse>> Commit([FromBody] GLBulkCommitRequest r)
    {
        var rows = r.Rows.Select(x => new GLUploadRow(x.RowNumber, x.Row)).ToList();
        return Ok(await _mediator.Send(new CommitGLUploadCommand(r.CompanyId, r.BatchId, rows, GetActingUser())));
    }

    // ---------- Authorization (GLChecker) ----------

    [HttpPost("{id:guid}/authorize")]
    [Authorize(Policy = "GLChecker")]
    [ProducesResponseType(typeof(GLAccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GLAccountResponse>> Authorize(Guid id)
        => Ok(await _mediator.Send(new AuthorizeGLAccountCommand(id, GetActingUser())));

    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = "GLChecker")]
    [ProducesResponseType(typeof(GLAccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GLAccountResponse>> Reject(Guid id, [FromBody] RejectGLRequest r)
        => Ok(await _mediator.Send(new RejectGLAccountCommand(id, r.Reason, GetActingUser())));

    [HttpPost("bulk/{batchId:guid}/authorize")]
    [Authorize(Policy = "GLChecker")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    public async Task<ActionResult<int>> AuthorizeBatch(Guid batchId)
        => Ok(await _mediator.Send(new AuthorizeGLBatchCommand(batchId, GetActingUser())));

    [HttpPost("bulk/{batchId:guid}/reject")]
    [Authorize(Policy = "GLChecker")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<int>> RejectBatch(Guid batchId, [FromBody] RejectGLRequest r)
        => Ok(await _mediator.Send(new RejectGLBatchCommand(batchId, r.Reason, GetActingUser())));

    // ---------- System-GL / JV posting enablement (SystemAdmin) ----------

    [HttpPost("systemgl")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(SystemGLCompletenessResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SystemGLCompletenessResponse>> MapSystemGL([FromBody] MapSystemGLRequest r)
        => Ok(await _mediator.Send(new MapSystemGLCommand(r.CompanyId, r.Type, r.GLAccountId, GetActingUser())));

    [HttpPost("systemgl/{companyId:guid}/enable-posting")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EnablePosting(Guid companyId)
    {
        await _mediator.Send(new EnableJvPostingCommand(companyId, GetActingUser()));
        return NoContent();
    }

    /// <summary>
    /// Resolves the acting user id from the validated JWT principal so the audit actor and
    /// maker/checker identity cannot be spoofed. Prefers NameIdentifier (sub), falling back to Name.
    /// </summary>
    private string GetActingUser()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? User.FindFirstValue("sub")
           ?? User.FindFirstValue(ClaimTypes.Name)
           ?? User.Identity?.Name
           ?? string.Empty;
}
