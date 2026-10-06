namespace Finnova.Models.Contracts.UserManagement;

// ---- Master reference LOVs sourced from the lines_of_business and programs tables ----

/// <summary>Active Line of Business reference item (sourced from lines_of_business).</summary>
public record LineOfBusinessRefResponse(Guid Id, string LobName, string LobDescription);

/// <summary>Active program reference item (sourced from programs).</summary>
public record ProgramRefResponse(Guid Id, string ProgramName, string DisplayName);
