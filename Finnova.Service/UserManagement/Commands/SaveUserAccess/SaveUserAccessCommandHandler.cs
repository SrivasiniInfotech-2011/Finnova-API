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

        // Resolve the selected LOB (FK -> lines_of_business).
        var lob = await _repository.GetLineOfBusinessByIdAsync(request.LineOfBusinessId, ct)
            ?? throw new UserValidationException("The selected Line of Business was not found.");

        // The selected LOB must be linked to at least one defined Role Code (R7.3/7.5).
        if (!LineOfBusinessCatalog.HasRoleCodes(lob.LOB_Name))
            throw new UserValidationException("The selected Line of Business has no linked Role Codes.");

        // Program metadata lookup: resolve each ProgramId -> ProgramName/DisplayName server-side so
        // RoleCode cannot drift from a client-sent program name (RoleCode parity).
        var programsById = (await _repository.GetActiveProgramsAsync(ct)).ToDictionary(p => p.Id);

        var rows = request.Rows.ToList();
        var branches = request.BranchCodes.ToList();

        // Copy Profile (Create mode): append source rows/branches, de-dup with OR-merge (R10.2).
        if (request.CopyProfile is not null)
        {
            var source = await _repository.GetUserByCodeAsync(request.CopyProfile.SourceUserCode, ct)
                ?? throw new UserValidationException("Copy Profile source user was not found.");
            var (srcRows, srcBranches) = await _repository.GetAccessAsync(
                source.Id, request.CopyProfile.SourceLineOfBusinessId, ct);

            var merged = AccessAssignmentMerger.Merge(
                (rows, branches),
                (srcRows.Select(r => ToRow(r, programsById)),
                 srcBranches.Select(b => b.BranchCode)));
            rows = merged.Rows;
            branches = merged.Branches;
        }

        var assignments = rows.Select(r =>
        {
            var programName = ResolveProgramName(r, programsById);
            return new UserAccessAssignment
            {
                UserAccountId = user.Id,
                LineOfBusinessId = lob.Id,
                RoleCenterName = r.RoleCenterName,
                ProgramId = r.ProgramId,
                RoleCode = RoleCodeBuilder.Build(r.RoleCenterName, programName),   // server-resolved name
                CanAdd = r.CanAdd,
                CanModify = r.CanModify,
                CanQuery = r.CanQuery,
                CanDelete = r.CanDelete,
            };
        }).ToList();

        var branchAssociations = branches.Select(b => new UserBranchAssociation
        {
            UserAccountId = user.Id,
            LineOfBusinessId = lob.Id,
            BranchCode = b,
            IsAll = string.Equals(b, "ALL", StringComparison.OrdinalIgnoreCase),   // R9.4
        }).ToList();

        await _repository.ReplaceAccessAsync(user.Id, lob.Id, assignments, branchAssociations, ct);

        await _repository.AddAuditAsync(new UserManagementAuditEntry
        {
            RecordId = user.Id,
            RecordKind = UserConfiguration.User,
            Action = UserManagementAuditAction.Modify,
            NewValues = $"{{\"LOB\":\"{lob.LOB_Name}\",\"Rows\":{assignments.Count},\"Branches\":{branchAssociations.Count}}}",
            Summary = $"Saved access for user '{user.UserCode}' (LOB {lob.LOB_Name}).",
            ChangedBy = request.ActingAdmin,
            ChangedAtUtc = DateTime.UtcNow,
        }, ct);

        return new UserAccessResponse(lob.Id, lob.LOB_Name, rows, branches);
    }

    // Build an AccessRightRow from a stored assignment, enriching program name/display from the
    // active-programs lookup (so copied rows carry the same display shape as request rows).
    private static AccessRightRow ToRow(UserAccessAssignment r, IReadOnlyDictionary<Guid, ScreenProgram> programsById)
    {
        var hasProgram = programsById.TryGetValue(r.ProgramId, out var program);
        return new AccessRightRow(
            r.RoleCode, r.RoleCenterName, r.ProgramId,
            hasProgram ? program!.ProgramName : string.Empty,
            hasProgram ? program!.DisplayName : string.Empty,
            r.CanAdd, r.CanModify, r.CanQuery, r.CanDelete);
    }

    private static string ResolveProgramName(AccessRightRow row, IReadOnlyDictionary<Guid, ScreenProgram> programsById)
        => programsById.TryGetValue(row.ProgramId, out var program) ? program.ProgramName : row.ProgramName;
}
