using Finnova.Models.Contracts.OrganizationHierarchy;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;
using MediatR;

namespace Finnova.Service.OrganizationHierarchy.Commands.CreateOrganizationNode;

public class CreateOrganizationNodeCommandHandler : IRequestHandler<CreateOrganizationNodeCommand, OrganizationNodeResponse>
{
    /// <summary>Maximum supported hierarchy depth (root = level 1).</summary>
    private const int MaxDepth = 10;

    private readonly IOrganizationNodeRepository _repository;
    private readonly IOrganizationNodeAuditRepository _auditRepository;

    public CreateOrganizationNodeCommandHandler(
        IOrganizationNodeRepository repository,
        IOrganizationNodeAuditRepository auditRepository)
    {
        _repository = repository;
        _auditRepository = auditRepository;
    }

    /// <summary>
    /// Handles the creation of a new organization node. Rejects duplicate codes, resolves the
    /// node level from its parent (root = 1, child = parent.Level + 1), persists the node, and
    /// records exactly one Create audit entry in the same scope.
    /// </summary>
    /// <exception cref="OrganizationNodeDuplicateCodeException">Code already exists.</exception>
    public async Task<OrganizationNodeResponse> Handle(CreateOrganizationNodeCommand request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim();

        if (await _repository.ExistsByCodeAsync(code, null, cancellationToken))
            throw new OrganizationNodeDuplicateCodeException();

        // Resolve level from the parent. A missing parent is a client error (400); depth is bounded.
        var level = 1;
        if (request.ParentId is Guid parentId)
        {
            var parent = await _repository.GetByIdAsync(parentId, cancellationToken)
                ?? throw new OrganizationNodeValidationException("Parent node does not exist.");
            level = parent.Level + 1;
            if (level > MaxDepth)
                throw new OrganizationNodeValidationException($"Maximum hierarchy depth of {MaxDepth} exceeded.");
        }

        var entity = new OrganizationNode
        {
            Code = code,
            Name = request.Name.Trim(),
            ParentId = request.ParentId,
            Level = level,
            IsActive = request.IsActive ?? true,   // default true
        };

        await _repository.AddAsync(entity, cancellationToken);

        // Record exactly one Create audit entry in the same scope.
        var audit = new OrganizationNodeAuditEntry
        {
            NodeId = entity.Id,
            Action = OrganizationNodeAuditAction.Create,
            OldName = null,
            NewName = entity.Name,
            OldParentId = null,
            NewParentId = entity.ParentId,
            ChangedBy = request.ActingAdmin,
            ChangedAtUtc = DateTime.UtcNow,
        };
        await _auditRepository.AddAsync(audit, cancellationToken);

        return entity.ToResponse();
    }
}


