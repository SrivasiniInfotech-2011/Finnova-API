using Finnova.Models.Contracts.Documents;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Document.Internal;
using Finnova.Service.Mappers;
using MediatR;
using DocAudit = Finnova.Models.Domain.Entities.DocumentAuditEntry;

namespace Finnova.Service.Document.Commands.ResubmitDocument;

public record ResubmitDocumentCommand(Guid Id, string ActingUser) : IRequest<DocumentResponse>;

public class ResubmitDocumentCommandHandler : IRequestHandler<ResubmitDocumentCommand, DocumentResponse>
{
    private readonly IDocumentRepository _repository;
    private readonly IDocumentAuditRepository _audit;

    public ResubmitDocumentCommandHandler(IDocumentRepository repository, IDocumentAuditRepository audit)
    { _repository = repository; _audit = audit; }

    public async Task<DocumentResponse> Handle(ResubmitDocumentCommand request, CancellationToken ct)
    {
        var entity = await _repository.GetByIdWithRulesAsync(request.Id, ct)
            ?? throw new DocumentNotFoundException();

        // R5.10 — only a returned document can be resubmitted.
        if (entity.Status != DocumentStatus.ReturnedToMaker)
            throw new DocumentConflictException("Only a returned document can be resubmitted.");

        entity.Status = DocumentStatus.PendingApproval;   // R5.10
        entity.MakerId = request.ActingUser;              // last editor for self-auth prohibition
        entity.RejectionReason = null;
        entity.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(entity, ct);

        await _audit.AddAsync(new DocAudit
        {
            DocumentId = entity.Id,
            Action = DocumentAuditAction.Submit,
            OldValues = DocumentAuditSnapshot.Editable(entity),
            NewValues = DocumentAuditSnapshot.Editable(entity),
            Summary = $"Resubmitted document '{entity.DocCode}'.",
            ChangedBy = request.ActingUser,
            ChangedAtUtc = DateTime.UtcNow,
        }, ct);

        return entity.ToResponse();
    }
}