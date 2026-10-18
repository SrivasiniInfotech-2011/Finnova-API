using Finnova.Models.Contracts.GL;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.GL.Internal;
using Finnova.Service.GL.Ports;
using MediatR;

namespace Finnova.Service.GL.Commands.MapSystemGL;

public class MapSystemGLCommandHandler : IRequestHandler<MapSystemGLCommand, SystemGLCompletenessResponse>
{
    private readonly IGLSystemAccountMappingRepository _mappings;
    private readonly IGLAccountRepository _accounts;
    private readonly IGLAuditRepository _audit;
    private readonly ICompanyQueryPort _company;

    public MapSystemGLCommandHandler(
        IGLSystemAccountMappingRepository mappings, IGLAccountRepository accounts,
        IGLAuditRepository audit, ICompanyQueryPort company)
    {
        _mappings = mappings;
        _accounts = accounts;
        _audit = audit;
        _company = company;
    }

    public async Task<SystemGLCompletenessResponse> Handle(MapSystemGLCommand request, CancellationToken ct)
    {
        // R7.2 — the mapped GL must belong to the company and be Active
        var gl = await _accounts.GetByIdAsync(request.GLAccountId, ct)
            ?? throw new GLConflictException("Mapped GL account must belong to the company and be Active.");
        if (gl.CompanyId != request.CompanyId || gl.Status != GLStatus.Active)
            throw new GLConflictException("Mapped GL account must belong to the company and be Active.");

        // R7.7 — at most one active mapping per (company, type)
        if (await _mappings.GetActiveAsync(request.CompanyId, request.Type, ct) is not null)
            throw new GLConflictException("The system GL type is already mapped for this company.");

        var mapping = new GLSystemAccountMapping
        {
            CompanyId = request.CompanyId,
            SystemGLType = request.Type,
            GLAccountId = request.GLAccountId,
            IsActive = true
        };
        await _mappings.AddAsync(mapping, ct);

        await _audit.AddAsync(new GLAuditEntry
        {
            GLAccountId = request.GLAccountId,
            Action = GLAuditAction.SystemGLMap,
            OldValues = null,
            NewValues = GLAuditSnapshot.Editable(gl),
            Summary = $"Mapped system GL '{request.Type}' for company {request.CompanyId}.",
            ChangedBy = request.ActingUser,
            ChangedAtUtc = DateTime.UtcNow
        }, ct);

        return await Completeness.ComputeAsync(_mappings, _company, request.CompanyId, ct);
    }
}
