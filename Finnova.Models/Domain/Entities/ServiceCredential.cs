namespace Finnova.Models.Domain.Entities;

/// <summary>Machine credential for the seeded SystemUser. Only a salted PBKDF2 hash is stored.</summary>
public sealed class ServiceCredential
{
    public Guid ClientId { get; set; }          // the SystemUser id
    public string SecretHash { get; set; } = string.Empty; // "salt.hash" (PasswordHasher format)
    public bool IsActive { get; set; } = true;
    public DateTime? LastUsedAtUtc { get; set; }
}