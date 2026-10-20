using Finnova.Models.Contracts.Documents;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Document.Internal;
using Finnova.Service.Mappers;
using MediatR;
using DocAudit = Finnova.Models.Domain.Entities.DocumentAuditEntry;

namespace Finnova.Service.Document.Commands.AuthorizeDocument;

public record AuthorizeDocumentCommand(Guid Id, string ActingUser) : IRequest<DocumentResponse>;

public class AuthorizeDocumentCommandHandler : IRequestHandler<AuthorizeDocumentCommand, DocumentResponse>
{
    private readonly IDocumentRepository _repository;
    private readonly IDocumentAuditRepository _audit;

    public AuthorizeDocumentCommandHandler(IDocumentRepository repository, IDocumentAuditRepository audit)
    { _repository = repository; _audit = audit; }

    public async Task<DocumentResponse> Handle(AuthorizeDocumentCommand request, CancellationToken ct)
    {
        var entity = await _repository.GetByIdWithRulesAsync(request.Id, ct)
            ?? throw new DocumentNotFoundException();

        // R5.9 — must be awaiting authorization.
        if (entity.Status is not (DocumentStatus.PendingApproval or DocumentStatus.PendingModification))
            throw new DocumentConflictException("There is no pending change to act upon.");

        // R5.5 / R12.6 — self-authorization prohibited, independent of role membership.
        if (string.Equals(entity.MakerId, request.ActingUser, StringComparison.Ordinal))
            throw new DocumentSelfAuthorizationException();   // ERR-DOC-001

        var wasModification = entity.Status == DocumentStatus.PendingModification;

        // Copy pending edit -> live columns, then clear the pending payload (R5.3).
        if (wasModification && entity.PendingValuesJson is not null)
            DocumentPendingPayload.ApplyTo(entity, entity.PendingValuesJson);
        entity.PendingValuesJson = null;

        entity.Status = DocumentStatus.Active;       // R5.4 now consumable
        entity.CheckerId = request.ActingUser;
        if (wasModification) entity.AuthorizedVersionNumber += 1;   // R4.5-style versioning
        entity.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(entity, ct);

        await _audit.AddAsync(new DocAudit
        {
            DocumentId = entity.Id,
            Action = DocumentAuditAction.Approve,
            OldValues = DocumentAuditSnapshot.Editable(entity),
            NewValues = DocumentAuditSnapshot.Editable(entity),
            Summary = $"Authorized document '{entity.DocCode}'.",
            ChangedBy = request.ActingUser,
            ChangedAtUtc = DateTime.UtcNow,
        }, ct);

        return entity.ToResponse();
    }
}