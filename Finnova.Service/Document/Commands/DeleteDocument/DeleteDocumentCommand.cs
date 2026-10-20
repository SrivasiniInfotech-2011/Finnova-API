using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Document.Internal;
using Finnova.Service.Document.Ports;
using MediatR;
using DocAudit = Finnova.Models.Domain.Entities.DocumentAuditEntry;

namespace Finnova.Service.Document.Commands.DeleteDocument;

public record DeleteDocumentCommand(Guid Id, string ActingUser) : IRequest<Unit>;

public class DeleteDocumentCommandHandler : IRequestHandler<DeleteDocumentCommand, Unit>
{
    private readonly IDocumentRepository _repository;
    private readonly IDocumentAuditRepository _audit;
    private readonly IDocumentUsageChecker _usage;

    public DeleteDocumentCommandHandler(IDocumentRepository repository,
        IDocumentAuditRepository audit, IDocumentUsageChecker usage)
    { _repository = repository; _audit = audit; _usage = usage; }

    public async Task<Unit> Handle(DeleteDocumentCommand request, CancellationToken ct)
    {
        var entity = await _repository.GetByIdWithRulesAsync(request.Id, ct)
            ?? throw new DocumentNotFoundException();   // R7.4

        // R7.3 — an active document must be inactivated, not deleted.
        if (entity.Status == DocumentStatus.Active)
            throw new DocumentConflictException("An active document must be inactivated rather than deleted.");

        // R7.2 — only a never-authorized PendingApproval doc is deletable. AuthorizedVersionNumber
        // == 0 confirms it was never authorized.
        if (entity.Status != DocumentStatus.PendingApproval || entity.AuthorizedVersionNumber > 0)
            throw new DocumentConflictException("Only a never-authorized pending document can be deleted.");

        // R7.1 / R7.5 — usage check with a 5-second fail-safe timeout.
        bool referenced;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));
            referenced = await _usage.IsReferencedByCustomerMasterAsync(entity.Id, cts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new DocumentConflictException("In-use status could not be confirmed. Deletion was blocked."); // R7.5
        }
        catch (Exception)
        {
            throw new DocumentConflictException("In-use status could not be confirmed. Deletion was blocked."); // R7.5
        }

        if (referenced)
            throw new DocumentInUseException();   // ERR-DOC-002 (R7.1)

        await _repository.DeleteAsync(entity, ct);   // R7.2 hard delete

        await _audit.AddAsync(new DocAudit
        {
            DocumentId = entity.Id,
            Action = DocumentAuditAction.Delete,
            OldValues = DocumentAuditSnapshot.Editable(entity),
            NewValues = null,
            Summary = $"Deleted document '{entity.DocCode}'.",
            ChangedBy = request.ActingUser,
            ChangedAtUtc = DateTime.UtcNow,
        }, ct);

        return Unit.Value;
    }
}