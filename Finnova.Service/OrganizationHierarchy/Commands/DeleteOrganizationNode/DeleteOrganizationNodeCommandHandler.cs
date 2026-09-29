using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using MediatR;

namespace Finnova.Service.OrganizationHierarchy.Commands.DeleteOrganizationNode;

/// <summary>
/// Handles leaf-only deletion of an organization node. Rejects a missing node (404) and a node
/// with children (409), otherwise deletes it and records exactly one Delete audit entry (whose
/// Old* fields capture the deleted node's prior values) in the same scope.
/// </summary>
public class DeleteOrganizationNodeCommandHandler : IRequestHandler<DeleteOrganizationNodeCommand, Unit>
{
    private readonly IOrganizationNodeRepository _repository;
    private readonly IOrganizationNodeAuditRepository _auditRepository;

    public DeleteOrganizationNodeCommandHandler(
        IOrganizationNodeRepository repository,
        IOrganizationNodeAuditRepository auditRepository)
    {
        _repository = repository;
        _auditRepository = auditRepository;
    }

    public async Task<Unit> Handle(DeleteOrganizationNodeCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new OrganizationNodeNotFoundException(request.Id);

        if (await _repository.HasChildrenAsync(request.Id, cancellationToken))
            throw new OrganizationNodeHasChildrenException(request.Id);

        var priorName = entity.Name;
        var priorParentId = entity.ParentId;

        await _repository.DeleteAsync(entity, cancellationToken);

        await _auditRepository.AddAsync(new OrganizationNodeAuditEntry
        {
            NodeId = request.Id,
            Action = OrganizationNodeAuditAction.Delete,
            OldName = priorName,
            NewName = null,
            OldParentId = priorParentId,
            NewParentId = null,
            ChangedBy = request.ActingAdmin,
            ChangedAtUtc = DateTime.UtcNow,
        }, cancellationToken);

        return Unit.Value;
    }
}
