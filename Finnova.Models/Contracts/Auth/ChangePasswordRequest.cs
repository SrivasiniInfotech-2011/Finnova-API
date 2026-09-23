namespace Finnova.Models.Contracts.Auth;

public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword
);
