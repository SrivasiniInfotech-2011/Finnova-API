namespace Finnova.Models.Domain.Enums;

/// <summary>Which kind of record is being provisioned (FS §9 R1). Exactly one per record.</summary>
public enum UserConfiguration
{
    User = 0,
    UserGroup = 1,
    FunctionalGroup = 2
}
