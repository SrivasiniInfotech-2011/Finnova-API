using MediatR;

namespace Finnova.Service.Assets.MakeCodes.Commands.DeleteMakeCode;

/// <summary>Deactivates by default (IsActive=false, R6.1). HardDelete=true removes the row,
/// guarded by an in-use check (R6.4).</summary>
public record DeleteMakeCodeCommand(Guid Id, bool HardDelete = false) : IRequest;
