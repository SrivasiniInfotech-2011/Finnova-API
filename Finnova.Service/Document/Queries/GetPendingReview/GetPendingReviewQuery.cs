using Finnova.Models.Contracts.Documents;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Document.Internal;
using Finnova.Service.Mappers;
using MediatR;
using FluentValidation;
using DocEntity = Finnova.Models.Domain.Entities.Document;

namespace Finnova.Service.Document.Queries.GetPendingReview;

public record GetPendingReviewQuery(Guid Id) : IRequest<DocumentPendingReviewResponse>;

public class GetPendingReviewQueryHandler
    : IRequestHandler<GetPendingReviewQuery, DocumentPendingReviewResponse>
{
    private readonly IDocumentRepository _repository;
    public GetPendingReviewQueryHandler(IDocumentRepository repository) => _repository = repository;

    public async Task<DocumentPendingReviewResponse> Handle(GetPendingReviewQuery request, CancellationToken ct)
    {
        var entity = await _repository.GetByIdWithRulesAsync(request.Id, ct);

        // R5.2 — missing or not pending => no comparison payload.
        if (entity is null
            || entity.Status is not (DocumentStatus.PendingApproval or DocumentStatus.PendingModification))
            throw new ValidationException("There is no pending change to review.");

        var liveResponse = entity.ToResponse();

        DocumentResponse pendingResponse;
        if (entity.Status == DocumentStatus.PendingModification && entity.PendingValuesJson is not null)
        {
            // Build a shallow clone of the live entity and apply the pending payload onto it.
            var clone = Clone(entity);
            DocumentPendingPayload.ApplyTo(clone, entity.PendingValuesJson);
            pendingResponse = clone.ToResponse();
        }
        else
        {
            // Never-authorized PendingApproval: the row itself holds the proposed values.
            pendingResponse = liveResponse;
        }

        return new DocumentPendingReviewResponse(
            entity.Id, entity.Status.ToString(), liveResponse, pendingResponse);  // R5.1
    }

    private static DocEntity Clone(DocEntity d) => new()
    {
        Id = d.Id, DocCode = d.DocCode, DocName = d.DocName, DocShortName = d.DocShortName,
        Description = d.Description, DocCategory = d.DocCategory, ApplicableEntity = d.ApplicableEntity,
        IsMandatory = d.IsMandatory, HasExpiry = d.HasExpiry, RequiresMasking = d.RequiresMasking,
        IsAadhaarDocument = d.IsAadhaarDocument, ValidationEngine = d.ValidationEngine,
        FormatRegex = d.FormatRegex, MaskPattern = d.MaskPattern, MaxFileSizeMb = d.MaxFileSizeMb,
        AllowedMimeTypes = d.AllowedMimeTypes, Status = d.Status, MakerId = d.MakerId,
        CheckerId = d.CheckerId, AuthorizedVersionNumber = d.AuthorizedVersionNumber,
        RejectionReason = d.RejectionReason, CreatedAt = d.CreatedAt, UpdatedAt = d.UpdatedAt,
        ApplicabilityRules = d.ApplicabilityRules.Select(r => new Finnova.Models.Domain.Entities.DocumentApplicabilityRule
        {
            Id = r.Id, DocumentId = r.DocumentId, Constitution = r.Constitution,
            EntityRole = r.EntityRole, IsMandatory = r.IsMandatory
        }).ToList()
    };
}