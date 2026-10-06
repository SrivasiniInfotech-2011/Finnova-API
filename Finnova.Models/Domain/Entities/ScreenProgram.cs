namespace Finnova.Models.Domain.Entities;

/// <summary>
/// A screen (program) in the application registry. ProgramName is the stable screen key the UI
/// uses to resolve a user's per-screen permissions (R per-screen-permissions). Named ScreenProgram
/// (NOT Program) to avoid clashing with the host `public partial class Program` entry-point types.
/// Maps to table "programs". India-only: a single English DisplayName.
/// </summary>
public class ScreenProgram
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string ProgramName { get; set; } = string.Empty;  // unique screen key (e.g. "LookupMaster")
    public string DisplayName { get; set; } = string.Empty;  // English label (e.g. "Lookup Master")
    public string? Module { get; set; }                       // grouping module (e.g. "SystemAdmin"), optional
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
