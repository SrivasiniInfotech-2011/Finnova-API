using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Entities;

public sealed class CustomerAuditEntry
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid CustomerId { get; private set; }
    public CustomerAuditAction Action { get; private set; }
    public string ActorId { get; private set; } = string.Empty;
    public string ChangedFieldsJson { get; private set; } = "{}";
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    public static CustomerAuditEntry Create(Guid customerId, CustomerAuditAction action, string actorId, string changedFieldsJson)
        => new()
        {
            CustomerId = customerId,
            Action = action,
            ActorId = actorId,
            ChangedFieldsJson = changedFieldsJson,
            CreatedAtUtc = DateTime.UtcNow,
        };}
