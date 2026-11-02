using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Entities;

public sealed class VerificationSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CorrelationId { get; set; } = string.Empty;
    public VerificationWorkflowType WorkflowType { get; set; }
    public WorkflowInputMode InputMode { get; set; }
    public string ActorReference { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public string RequestedOperationsJson { get; set; } = "[]";
    public ICollection<ValidationRecord> Records { get; set; } = new List<ValidationRecord>();
}
