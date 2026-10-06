using Finnova.Models.Contracts.Auth;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;

namespace Finnova.Service.Auth;

public static class AuthUserMapper
{
    /// <summary>
    /// Maps the domain UserAccount to the UI-facing auth user shape.
    /// The UI expects roles as ADMIN / MANAGER / ANALYST / CLIENT.
    /// </summary>
    public static AuthUserDto ToAuthUser(this UserAccount user)
    {
        return new AuthUserDto(
            Id: user.Id.ToString(),
            Name: user.Name,
            Email: user.Email ?? string.Empty,
            Role: MapRole(user.Role));
    }

    // API roles (User/Admin/Manager/Auditor) -> UI roles (ADMIN/MANAGER/ANALYST/CLIENT).
    private static string MapRole(UserRole role) => role switch
    {
        UserRole.Admin => "ADMIN",
        UserRole.Manager => "MANAGER",
        UserRole.Auditor => "ANALYST",
        UserRole.User => "CLIENT",
        _ => "CLIENT",
    };
}
