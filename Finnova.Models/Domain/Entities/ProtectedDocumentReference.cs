namespace Finnova.Models.Domain.Entities;
public sealed class ProtectedDocumentReference
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string StorageKey { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? RetentionUntilUtc { get; set; }
    public ICollection<ValidationRecord> ValidationRecords { get; set; } = new List<ValidationRecord>();
}
