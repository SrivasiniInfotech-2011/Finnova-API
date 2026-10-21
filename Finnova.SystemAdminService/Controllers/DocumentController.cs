using System.Security.Claims;
using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.Documents;
using Finnova.Service.Document.Commands.AuthorizeDocument;
using Finnova.Service.Document.Commands.CreateDocument;
using Finnova.Service.Document.Commands.DeleteDocument;
using Finnova.Service.Document.Commands.RejectDocument;
using Finnova.Service.Document.Commands.ResubmitDocument;
using Finnova.Service.Document.Commands.SetDocumentStatus;
using Finnova.Service.Document.Commands.UpdateApplicabilityRules;
using Finnova.Service.Document.Commands.UpdateDocument;
using Finnova.Service.Document.Commands.UpdateValidationConfig;
using Finnova.Service.Document.Queries.GetActiveDocumentsForConsumption;
using Finnova.Service.Document.Queries.GetDocumentAuditTrail;
using Finnova.Service.Document.Queries.GetDocumentById;
using Finnova.Service.Document.Queries.GetDocumentsPaged;
using Finnova.Service.Document.Queries.GetPendingReview;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finnova.SystemAdminService.Controllers;

/// <summary>Document Master endpoints (FINNOVA-49/50/52). Maker/Checker split per R12.</summary>
[ApiController]
[Route("api/document")]
public class DocumentController : ControllerBase
{
    private readonly IMediator _mediator;
    public DocumentController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize]                                   // any authenticated SystemAdmin user (R10, R12)
    [ProducesResponseType(typeof(PaginatedResponse<DocumentResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedResponse<DocumentResponse>>> GetPaged(
        [FromQuery] string? search, [FromQuery] string? category, [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await _mediator.Send(new GetDocumentsPagedQuery(search, category, status, page, pageSize)));

    [HttpGet("{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(DocumentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentResponse>> GetById(Guid id)
        => Ok(await _mediator.Send(new GetDocumentByIdQuery(id)));

    [HttpGet("{id:guid}/review")]
    [Authorize(Policy = "DOC_MASTER_CHECKER")]
    [ProducesResponseType(typeof(DocumentPendingReviewResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DocumentPendingReviewResponse>> GetReview(Guid id)
        => Ok(await _mediator.Send(new GetPendingReviewQuery(id)));

    [HttpGet("{id:guid}/audit")]
    [Authorize]
    [ProducesResponseType(typeof(List<DocumentAuditEntryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<DocumentAuditEntryResponse>>> GetAudit(Guid id)
        => Ok(await _mediator.Send(new GetDocumentAuditTrailQuery(id)));

    [HttpGet("consume")]
    [Authorize]
    [ProducesResponseType(typeof(List<ActiveDocumentForConsumptionResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ActiveDocumentForConsumptionResponse>>> Consume(
        [FromQuery] string constitution, [FromQuery] string entityRole)
        => Ok(await _mediator.Send(new GetActiveDocumentsForConsumptionQuery(constitution, entityRole)));

    [HttpPost]
    [Authorize(Policy = "DOC_MASTER_MAKER")]
    [ProducesResponseType(typeof(DocumentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DocumentResponse>> Create([FromBody] CreateDocumentRequest r)
    {
        var result = await _mediator.Send(new CreateDocumentCommand(
            r.DocCode, r.DocName, r.DocShortName, r.Description, r.DocCategory, r.ApplicableEntity,
            r.IsMandatory, r.HasExpiry, r.RequiresMasking, r.IsAadhaarDocument, r.MaxFileSizeMb,
            GetActingUser()));
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "DOC_MASTER_MAKER")]
    public async Task<ActionResult<DocumentResponse>> Update(Guid id, [FromBody] UpdateDocumentRequest r)
        => Ok(await _mediator.Send(new UpdateDocumentCommand(
            id, r.DocName, r.DocShortName, r.Description, r.DocCategory, r.ApplicableEntity,
            r.IsMandatory, r.HasExpiry, GetActingUser())));

    [HttpPut("{id:guid}/applicability")]
    [Authorize(Policy = "DOC_MASTER_MAKER")]
    public async Task<ActionResult<DocumentResponse>> UpdateApplicability(
        Guid id, [FromBody] UpdateApplicabilityRulesRequest r)
        => Ok(await _mediator.Send(new UpdateApplicabilityRulesCommand(id, r.Rules, GetActingUser())));

    [HttpPut("{id:guid}/validation-config")]
    [Authorize(Policy = "DOC_MASTER_MAKER")]
    public async Task<ActionResult<DocumentResponse>> UpdateValidationConfig(
        Guid id, [FromBody] UpdateValidationConfigRequest r)
        => Ok(await _mediator.Send(new UpdateValidationConfigCommand(
            id, r.ValidationEngine, r.FormatRegex, r.RequiresMasking, r.MaskPattern,
            r.MaxFileSizeMb, r.AllowedMimeTypes, GetActingUser())));

    [HttpPost("{id:guid}/authorize")]
    [Authorize(Policy = "DOC_MASTER_CHECKER")]
    [ProducesResponseType(typeof(DocumentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]   // ERR-DOC-001 / not-pending
    public async Task<ActionResult<DocumentResponse>> Authorize(Guid id)
        => Ok(await _mediator.Send(new AuthorizeDocumentCommand(id, GetActingUser())));

    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = "DOC_MASTER_CHECKER")]
    public async Task<ActionResult<DocumentResponse>> Reject(Guid id, [FromBody] RejectDocumentRequest r)
        => Ok(await _mediator.Send(new RejectDocumentCommand(id, r.Reason, GetActingUser())));

    [HttpPost("{id:guid}/resubmit")]
    [Authorize(Policy = "DOC_MASTER_MAKER")]
    public async Task<ActionResult<DocumentResponse>> Resubmit(Guid id)
        => Ok(await _mediator.Send(new ResubmitDocumentCommand(id, GetActingUser())));

    [HttpPost("{id:guid}/inactivate")]
    [Authorize(Policy = "DOC_MASTER_MAKER")]
    public async Task<ActionResult<DocumentResponse>> Inactivate(Guid id)
        => Ok(await _mediator.Send(new SetDocumentStatusCommand(id, Active: false, GetActingUser())));

    [HttpPost("{id:guid}/reactivate")]
    [Authorize(Policy = "DOC_MASTER_MAKER")]
    public async Task<ActionResult<DocumentResponse>> Reactivate(Guid id)
        => Ok(await _mediator.Send(new SetDocumentStatusCommand(id, Active: true, GetActingUser())));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "DOC_MASTER_MAKER")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]   // ERR-DOC-002 / active delete
    public async Task<IActionResult> Delete(Guid id)
    {
        await _mediator.Send(new DeleteDocumentCommand(id, GetActingUser()));
        return NoContent();
    }

    /// <summary>Resolves the acting user from the validated JWT principal (mirrors
    /// CourtController.GetActingAdmin), so the audit actor and self-auth check cannot be
    /// spoofed by the request body.</summary>
    private string GetActingUser()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? User.FindFirstValue("sub")
           ?? User.FindFirstValue(ClaimTypes.Name)
           ?? User.Identity?.Name
           ?? string.Empty;
}