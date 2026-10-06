namespace Finnova.Models.Domain.Entities;

/// <summary>One clubbed program function under a functional group (FS §9 R6.3). Table "functional_group_functions".</summary>
public class FunctionalGroupFunction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FunctionalGroupId { get; set; }               // FK -> functional_groups
    public Guid ProgramId { get; set; }                       // FK -> programs
    public string RoleCode { get; set; } = string.Empty;      // RoleCenterName + ProgramName (R8.4)
}
