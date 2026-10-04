using Finnova.Models.Contracts.UserManagement;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.UserManagement.Helpers;
using Finnova.Service.UserManagement.Internal;
using MediatR;

namespace Finnova.Service.UserManagement.Commands.SaveUserAccess;

public class SaveUserAccessCommandHandler : IRequestHandler<SaveUserAccessCommand, UserAccessResponse>
{
    private readonly IUserManagementRepository _repository;

    public SaveUserAccessCommandHandler(IUserManagementRepository repository) => _repository = repository;

    public async Task<UserAccessResponse> Handle(SaveUserAccessCommand request, CancellationToken ct)
    {
        var user = await _repository.GetUserWithAccessAsync(request.Id, ct)
            ?? throw new UserNotFoundException(request.Id.ToString());   // R11.7

        // The selected LOB must be linked to at least one defined Role Code (R7.3/7.5).
        if (!LineOfBusinessCatalog.HasRoleCodes(request.LineOfBusiness))
            throw new UserValidationException("The selected Line of Business has no linked Role Codes.");

        var rows = request.Rows.ToList();
        var branches = request.BranchCodes.ToList();

        // Copy Profile (Create mode): append source rows/branches, de-dup with OR-merge (R10.2).
        if (request.CopyProfile is not null)
        {
            var source = await _repository.GetUserByCodeAsync(request.CopyProfile.SourceUserCode, ct)
                ?? throw new UserValidationException("Copy Profile source user was not found.");
            var (srcRows, srcBranches) = await _repository.GetAccessAsync(
                source.Id, request.CopyProfile.SourceLineOfBusiness, ct);

            var merged = AccessAssignmentMerger.Merge(
                (rows, branches),
                (srcRows.Select(r => new AccessRightRow(r.RoleCode, r.RoleCenterName, r.ProgramName,
                    r.CanAdd, r.CanModify, r.CanQuery, r.CanDelete)),
                 srcBranches.Select(b => b.BranchCode)));
            rows = merged.Rows;
            branches = merged.Branches;
        }

        var assignments = rows.Select(r => new UserAccessAssignment
        {
            UserAccountId = user.Id,
            LineOfBusiness = request.LineOfBusiness,
            RoleCenterName = r.RoleCenterName,
            ProgramName = r.ProgramName,
            RoleCode = r.RoleCode,
            CanAdd = r.CanAdd,
            CanModify = r.CanModify,
            CanQuery = r.CanQuery,
            CanDelete = r.CanDelete,
        }).ToList();

        var branchAssociations = branches.Select(b => new UserBranchAssociation
        {
            UserAccountId = user.Id,
            LineOfBusiness = request.LineOfBusiness,
            BranchCode = b,
            IsAll = string.Equals(b, "ALL", StringComparison.OrdinalIgnoreCase),   // R9.4
        }).ToList();

        await _repository.ReplaceAccessAsync(user.Id, request.LineOfBusiness, assignments, branchAssociations, ct);

        await _repository.AddAuditAsync(new UserManagementAuditEntry
        {
            RecordId = user.Id,
            RecordKind = UserConfiguration.User,
            Action = UserManagementAuditAction.Modify,
            NewValues = $"{{\"LOB\":\"{request.LineOfBusiness}\",\"Rows\":{assignments.Count},\"Branches\":{branchAssociations.Count}}}",
            Summary = $"Saved access for user '{user.UserCode}' (LOB {request.LineOfBusiness}).",
            ChangedBy = request.ActingAdmin,
            ChangedAtUtc = DateTime.UtcNow,
        }, ct);

        return new UserAccessResponse(request.LineOfBusiness, rows, branches);
    }
}
