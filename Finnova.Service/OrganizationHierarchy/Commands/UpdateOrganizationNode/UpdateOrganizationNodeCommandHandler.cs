using Finnova.Models.Contracts.OrganizationHierarchy;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;
using MediatR;

namespace Finnova.Service.OrganizationHierarchy.Commands.UpdateOrganizationNode;

/// <summary>
/// Handles renaming and/or re-parenting an organization node. Enforces structural validity
/// (self-parent, cycle, parent-exists, bounded depth), recomputes the moved node's and every
/// descendant's Level so the level invariant (root = 1, child = parent + 1) holds across the
/// whole subtree, and records exactly one Update audit entry. A same-name-and-same-parent
/// submission is a successful no-op with no audit entry. The moved node, its recomputed
/// descendants, and the audit entry are persisted atomically.
/// </summary>
public class UpdateOrganizationNodeCommandHandler
    : IRequestHandler<UpdateOrganizationNodeCommand, OrganizationNodeResponse>
{
    /// <summary>Maximum supported hierarchy depth (root = level 1).</summary>
    private const int MaxDepth = 10;

    private readonly IOrganizationNodeRepository _repository;
    private readonly IOrganizationNodeAuditRepository _auditRepository;

    public UpdateOrganizationNodeCommandHandler(
        IOrganizationNodeRepository repository,
        IOrganizationNodeAuditRepository auditRepository)
    {
        _repository = repository;
        _auditRepository = auditRepository;
    }

    public async Task<OrganizationNodeResponse> Handle(
        UpdateOrganizationNodeCommand request,
        CancellationToken cancellationToken)
    {
        // Unknown id -> not found; nothing changed, no audit.
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new OrganizationNodeNotFoundException(request.Id);

        var newName = request.Name.Trim();
        var newParentId = request.ParentId;

        // No-op: same name AND same parent -> return unchanged, no field/timestamp change, no audit.
        if (string.Equals(entity.Name, newName, StringComparison.Ordinal) && entity.ParentId == newParentId)
            return entity.ToResponse();

        var oldName = entity.Name;
        var oldParentId = entity.ParentId;
        var parentChanged = entity.ParentId != newParentId;

        // Rename-only path: no structural work needed.
        if (!parentChanged)
        {
            entity.Name = newName;
            entity.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(entity, cancellationToken);

            await _auditRepository.AddAsync(BuildAudit(entity.Id, oldName, newName, oldParentId, newParentId, request.ActingAdmin), cancellationToken);
            return entity.ToResponse();
        }

        // ---- Re-parent path ----

        // Self-parent.
        if (newParentId == entity.Id)
            throw new OrganizationNodeValidationException("A node cannot be its own parent.");

        // Load the descendant set once (used for cycle detection and the level recompute).
        var descendants = await _repository.GetDescendantsAsync(entity.Id, cancellationToken);

        // Cycle: the new parent cannot be the node itself or any of its descendants.
        if (newParentId is Guid targetId && descendants.Any(d => d.Id == targetId))
            throw new OrganizationNodeValidationException("Re-parenting would create a cycle.");

        // Resolve the new level for the moved node (root = 1; child = parent.Level + 1).
        var newLevel = 1;
        if (newParentId is Guid parentId)
        {
            var parent = await _repository.GetByIdAsync(parentId, cancellationToken)
                ?? throw new OrganizationNodeValidationException("Parent node does not exist.");
            newLevel = parent.Level + 1;
        }

        // Depth bound: reject if the moved node or any descendant would exceed MaxDepth.
        var delta = newLevel - entity.Level;
        var maxSubtreeLevel = descendants.Count == 0
            ? entity.Level
            : Math.Max(entity.Level, descendants.Max(d => d.Level));
        if (maxSubtreeLevel + delta > MaxDepth)
            throw new OrganizationNodeValidationException($"Maximum hierarchy depth of {MaxDepth} exceeded.");

        // Apply changes in memory: moved node + every descendant's recomputed level.
        var now = DateTime.UtcNow;
        entity.Name = newName;
        entity.ParentId = newParentId;
        entity.Level = newLevel;
        entity.UpdatedAt = now;

        foreach (var descendant in descendants)
        {
            descendant.Level += delta;
            descendant.UpdatedAt = now;
        }

        // Persist the moved node, its recomputed descendants, and the audit entry atomically.
        var audit = BuildAudit(entity.Id, oldName, newName, oldParentId, newParentId, request.ActingAdmin);
        await _repository.ReparentAsync(entity, descendants, audit, cancellationToken);

        return entity.ToResponse();
    }

    private static OrganizationNodeAuditEntry BuildAudit(
        Guid nodeId, string oldName, string newName, Guid? oldParentId, Guid? newParentId, string actingAdmin) =>
        new()
        {
            NodeId = nodeId,
            Action = OrganizationNodeAuditAction.Update,
            OldName = oldName,
            NewName = newName,
            OldParentId = oldParentId,
            NewParentId = newParentId,
            ChangedBy = actingAdmin,
            ChangedAtUtc = DateTime.UtcNow,
        };
}
