using Finnova.Models.Contracts.Documents;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Document.Internal;
using Finnova.Service.Mappers;
using FluentValidation;
using MediatR;
using DocAudit = Finnova.Models.Domain.Entities.DocumentAuditEntry;

namespace Finnova.Service.Document.Commands.RejectDocument;

public record RejectDocumentCommand(Guid Id, string Reason, string ActingUser) : IRequest<DocumentResponse>;

public class RejectDocumentCommandHandler : IRequestHandler<RejectDocumentCommand, DocumentResponse>
{
    private readonly IDocumentRepository _repository;
    private readonly IDocumentAuditRepository _audit;

    public RejectDocumentCommandHandler(IDocumentRepository repository, IDocumentAuditRepository audit)
    { _repository = repository; _audit = audit; }

    public async Task<DocumentResponse> Handle(RejectDocumentCommand request, CancellationToken ct)
    {
        var entity = await _repository.GetByIdWithRulesAsync(request.Id, ct)
            ?? throw new DocumentNotFoundException();

        if (entity.Status is not (DocumentStatus.PendingApproval or DocumentStatus.PendingModification))
            throw new DocumentConflictException("There is no pending change to act upon.");  // R5.9

        if (string.Equals(entity.MakerId, request.ActingUser, StringComparison.Ordinal))
            throw new DocumentSelfAuthorizationException();  // ERR-DOC-001 (self-review on reject too)

        // R5.7 — remark 1..500 after trim (also enforced by the validator as a fast-fail).
        var remark = (request.Reason ?? string.Empty).Trim();
        if (remark.Length is < 1 or > 500)
            throw new ValidationException("A rejection remark of 1 to 500 characters is required.");

        // R5.6 — return to maker, retain pending values, record the remark; live unchanged.
        entity.Status = DocumentStatus.ReturnedToMaker;
        entity.CheckerId = request.ActingUser;
        entity.RejectionReason = remark;
        entity.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(entity, ct);

        await _audit.AddAsync(new DocAudit
        {
            DocumentId = entity.Id,
            Action = DocumentAuditAction.Reject,
            OldValues = DocumentAuditSnapshot.Editable(entity),
            NewValues = DocumentAuditSnapshot.Editable(entity),
            Summary = $"Rejected document '{entity.DocCode}'.",
            Remarks = remark,                                 // R8.2
            ChangedBy = request.ActingUser,
            ChangedAtUtc = DateTime.UtcNow,
        }, ct);

        return entity.ToResponse();
    }
}

public class RejectDocumentCommandValidator : AbstractValidator<RejectDocumentCommand>
{
    public RejectDocumentCommandValidator()
    {
        RuleFor(x => x.Reason)
            .Must(r => !string.IsNullOrWhiteSpace(r) && r.Trim().Length is >= 1 and <= 500)
            .WithMessage("A rejection remark of 1 to 500 characters is required.");  // R5.7
    }
}