namespace Finnova.Models.Contracts.Auth;

/// <summary>
/// Minimal user shape returned to the UI on login.
/// Matches the UI's User model: { id, name, email, role }.
/// </summary>
public record AuthUserDto(
    string Id,
    string Name,
    string Email,
    string Role
);
