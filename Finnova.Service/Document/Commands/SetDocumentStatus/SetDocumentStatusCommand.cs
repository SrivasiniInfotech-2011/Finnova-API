using Finnova.Models.Contracts.Documents;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Document.Internal;
using Finnova.Service.Mappers;
using MediatR;
using DocAudit = Finnova.Models.Domain.Entities.DocumentAuditEntry;

namespace Finnova.Service.Document.Commands.SetDocumentStatus;

public record SetDocumentStatusCommand(Guid Id, bool Active, string ActingUser) : IRequest<DocumentResponse>;

public class SetDocumentStatusCommandHandler : IRequestHandler<SetDocumentStatusCommand, DocumentResponse>
{
    private readonly IDocumentRepository _repository;
    private readonly IDocumentAuditRepository _audit;

    public SetDocumentStatusCommandHandler(IDocumentRepository repository, IDocumentAuditRepository audit)
    { _repository = repository; _audit = audit; }

    public async Task<DocumentResponse> Handle(SetDocumentStatusCommand request, CancellationToken ct)
    {
        var entity = await _repository.GetByIdWithRulesAsync(request.Id, ct)
            ?? throw new DocumentNotFoundException();

        if (request.Active)
        {
            // R6.4 — reactivate only from Inactive.
            if (entity.Status != DocumentStatus.Inactive)
                throw new DocumentConflictException("Only an inactive document can be reactivated.");
            entity.Status = DocumentStatus.Active;
        }
        else
        {
            // R6.1 — inactivate only from Active.
            if (entity.Status != DocumentStatus.Active)
                throw new DocumentConflictException("Only an active document can be inactivated.");
            entity.Status = DocumentStatus.Inactive;
        }

        entity.CheckerId = request.ActingUser;
        entity.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(entity, ct);

        await _audit.AddAsync(new DocAudit
        {
            DocumentId = entity.Id,
            Action = request.Active ? DocumentAuditAction.Reactivate : DocumentAuditAction.Inactivate,
            OldValues = DocumentAuditSnapshot.Editable(entity),
            NewValues = DocumentAuditSnapshot.Editable(entity),
            Summary = $"{(request.Active ? "Reactivated" : "Inactivated")} document '{entity.DocCode}'.",
            ChangedBy = request.ActingUser,
            ChangedAtUtc = DateTime.UtcNow,
        }, ct);

        return entity.ToResponse();
    }
}