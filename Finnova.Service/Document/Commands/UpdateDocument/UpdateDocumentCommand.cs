using Finnova.Models.Contracts.Documents;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Document.Internal;
using Finnova.Service.Document.Ports;
using Finnova.Service.Mappers;
using FluentValidation;
using MediatR;
using DocEntity = Finnova.Models.Domain.Entities.Document;
using DocAudit = Finnova.Models.Domain.Entities.DocumentAuditEntry;

namespace Finnova.Service.Document.Commands.UpdateDocument;

public record UpdateDocumentCommand(
    Guid Id, string DocName, string? DocShortName, string? Description,
    string DocCategory, string ApplicableEntity, bool IsMandatory, bool HasExpiry,
    string ActingUser) : IRequest<DocumentResponse>;

public class UpdateDocumentCommandHandler : IRequestHandler<UpdateDocumentCommand, DocumentResponse>
{
    private readonly IDocumentRepository _repository;
    private readonly IDocumentAuditRepository _audit;
    private readonly ILookupValidationPort _lookups;

    public UpdateDocumentCommandHandler(IDocumentRepository repository,
        IDocumentAuditRepository audit, ILookupValidationPort lookups)
    { _repository = repository; _audit = audit; _lookups = lookups; }

    public async Task<DocumentResponse> Handle(UpdateDocumentCommand request, CancellationToken ct)
    {
        var entity = await _repository.GetByIdWithRulesAsync(request.Id, ct)
            ?? throw new DocumentNotFoundException();   // R3.3

        // R3.5 — a change is already pending; reject a further edit.
        if (entity.Status is DocumentStatus.PendingModification)
            throw new DocumentConflictException("A change is already pending authorization.");
        if (entity.Status is DocumentStatus.PendingApproval && entity.PendingValuesJson is not null)
            throw new DocumentConflictException("A change is already pending authorization.");

        // R1.8 lookup-backed fields (same rule as create; R3.4 routes failures to validation).
        if (!await _lookups.IsActiveLookupAsync("DOC_CATEGORY", request.DocCategory, ct))
            throw new ValidationException("Doc Category is not an active lookup value.");
        if (!await _lookups.IsActiveLookupAsync("APPLICABLE_ENTITY", request.ApplicableEntity, ct))
            throw new ValidationException("Applicable Entity is not an active lookup value.");

        var name = request.DocName.Trim();
        var shortName = request.DocShortName?.Trim();
        var description = request.Description?.Trim();

        if (entity.Status == DocumentStatus.Active)
        {
            // R3.1/R3.2 — stage the pending edit; live columns keep serving consumers.
            entity.PendingValuesJson = DocumentPendingPayload.ForAttributes(
                name, shortName, description, request.DocCategory, request.ApplicableEntity,
                request.IsMandatory, request.HasExpiry);
            entity.Status = DocumentStatus.PendingModification;
        }
        else
        {
            // Never-authorized PendingApproval or ReturnedToMaker — apply in place (R3.6).
            entity.DocName = name;
            entity.DocShortName = shortName;
            entity.Description = description;
            entity.DocCategory = request.DocCategory;
            entity.ApplicableEntity = request.ApplicableEntity;
            entity.IsMandatory = request.IsMandatory;
            entity.HasExpiry = request.HasExpiry;
            entity.Status = DocumentStatus.PendingApproval;   // R3.6 keeps it pending for a checker
        }

        entity.MakerId = request.ActingUser;
        entity.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(entity, ct);

        await _audit.AddAsync(new DocAudit
        {
            DocumentId = entity.Id,
            Action = DocumentAuditAction.Update,
            OldValues = DocumentAuditSnapshot.Editable(entity),
            NewValues = entity.PendingValuesJson ?? DocumentAuditSnapshot.Editable(entity),
            Summary = $"Submitted edit for '{entity.DocCode}'.",
            ChangedBy = request.ActingUser,
            ChangedAtUtc = DateTime.UtcNow,
        }, ct);

        return entity.ToResponse();
    }
}

public class UpdateDocumentCommandValidator : AbstractValidator<UpdateDocumentCommand>
{
    public UpdateDocumentCommandValidator()
    {
        RuleFor(x => x.DocName).NotEmpty().WithMessage("Doc Name is required.")
            .Must(n => n is not null && n.Trim().Length <= 150)
                .WithMessage("Doc Name must not exceed 150 characters.");   // R1.5
        RuleFor(x => x.DocCategory).NotEmpty().WithMessage("Doc Category is required.");
        RuleFor(x => x.ApplicableEntity).NotEmpty().WithMessage("Applicable Entity is required.");
    }
}