using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.GL.Internal;
using Finnova.Service.GL.Ports;
using MediatR;

namespace Finnova.Service.GL.Commands.EnableJvPosting;

public class EnableJvPostingCommandHandler : IRequestHandler<EnableJvPostingCommand, Unit>
{
    private readonly IGLSystemAccountMappingRepository _mappings;
    private readonly ICompanyPostingStateRepository _state;
    private readonly ICompanyQueryPort _company;

    public EnableJvPostingCommandHandler(
        IGLSystemAccountMappingRepository mappings, ICompanyPostingStateRepository state, ICompanyQueryPort company)
    {
        _mappings = mappings;
        _state = state;
        _company = company;
    }

    public async Task<Unit> Handle(EnableJvPostingCommand request, CancellationToken ct)
    {
        var completeness = await Completeness.ComputeAsync(_mappings, _company, request.CompanyId, ct);
        // R7.3 — refuse enablement while any required mapping is missing/invalid
        if (!completeness.IsComplete)
            throw new GLConflictException(
                "Missing or invalid system GL mappings: " + string.Join(", ", completeness.MissingOrInvalid));

        var existing = await _state.GetByCompanyAsync(request.CompanyId, ct);
        if (existing is null)
        {
            await _state.AddAsync(new CompanyPostingState
            {
                CompanyId = request.CompanyId,
                JvPostingEnabled = true,
                UpdatedAt = DateTime.UtcNow
            }, ct);
        }
        else
        {
            existing.JvPostingEnabled = true;
            existing.UpdatedAt = DateTime.UtcNow;
            await _state.UpdateAsync(existing, ct);
        }

        return Unit.Value;
    }
}
