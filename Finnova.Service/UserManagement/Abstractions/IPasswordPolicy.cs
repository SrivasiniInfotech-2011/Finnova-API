namespace Finnova.Service.UserManagement.Abstractions;

/// <summary>
/// GPS password-policy evaluation (owned externally; consumed here). Enforced in the create/reset
/// handlers before hashing (R3.5/R11.6). The real policy is injected when available; a config-driven
/// default ships for development.
/// </summary>
public interface IPasswordPolicy
{
    bool IsCompliant(string password);
}
