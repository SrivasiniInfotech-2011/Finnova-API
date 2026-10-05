using Finnova.Models.Contracts.UserManagement;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Interfaces;
using Finnova.Service.UserManagement.Helpers;
using Finnova.Service.UserManagement.Internal;
using Finnova.Service.UserManagement.Mappers;
using MediatR;

namespace Finnova.Service.UserManagement.Commands.CreateFunctionalGroup;

public class CreateFunctionalGroupCommandHandler : IRequestHandler<CreateFunctionalGroupCommand, FunctionalGroupResponse>
{
    private readonly IUserManagementRepository _repository;

    public CreateFunctionalGroupCommandHandler(IUserManagementRepository repository) => _repository = repository;

    public async Task<FunctionalGroupResponse> Handle(CreateFunctionalGroupCommand request, CancellationToken ct)
    {
        var code = UserCodeGenerator.Generate(
            request.RoleCenterName,
            candidate => _repository.FunctionalGroupCodeExistsAsync(candidate, ct).GetAwaiter().GetResult());

        // Resolve each catalog program NAME to its active programs row so each clubbed function
        // carries the FK (ProgramId); names with no active program row are skipped.
        var programsByName = (await _repository.GetActiveProgramsAsync(ct))
            .ToDictionary(p => p.ProgramName, StringComparer.OrdinalIgnoreCase);

        var functions = new List<FunctionalGroupFunction>();
        var programNamesById = new Dictionary<Guid, string>();
        foreach (var name in RoleCenterCatalog.ProgramsFor(request.RoleCenterName))
        {
            if (!programsByName.TryGetValue(name, out var program))
                continue;

            functions.Add(new FunctionalGroupFunction
            {
                ProgramId = program.Id,
                RoleCode = RoleCodeBuilder.Build(request.RoleCenterName, program.ProgramName),
            });
            programNamesById[program.Id] = program.ProgramName;
        }

        var fg = new FunctionalGroup
        {
            FunctionalGroupCode = code,
            RoleCenterName = request.RoleCenterName.Trim(),
            IsActive = request.IsActive ?? true,
            // Club all programs attached to the Role Center into the group (R6.1/6.3).
            Functions = functions,
        };

        await _repository.AddFunctionalGroupAsync(fg, ct);

        await _repository.AddAuditAsync(new UserManagementAuditEntry
        {
            RecordId = fg.Id,
            RecordKind = UserConfiguration.FunctionalGroup,
            Action = UserManagementAuditAction.Create,
            NewValues = $"{{\"FunctionalGroupCode\":\"{fg.FunctionalGroupCode}\",\"Functions\":{fg.Functions.Count}}}",
            Summary = $"Created functional group '{fg.FunctionalGroupCode}'.",
            ChangedBy = request.ActingAdmin,
            ChangedAtUtc = DateTime.UtcNow,
        }, ct);

        return fg.ToResponse(programNamesById);
    }
}
