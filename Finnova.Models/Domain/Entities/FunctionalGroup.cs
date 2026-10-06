namespace Finnova.Models.Domain.Entities;

/// <summary>A grouping of program functions clubbed from a Role Center (FS §9 R6). Table "functional_groups".</summary>
public class FunctionalGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FunctionalGroupCode { get; set; } = string.Empty; // generated from Role Center Name (R2.3)
    public string RoleCenterName { get; set; } = string.Empty;      // source Role Center (R6.1)
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<FunctionalGroupFunction> Functions { get; set; } = new List<FunctionalGroupFunction>();
}
