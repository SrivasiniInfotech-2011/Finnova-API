namespace Finnova.Models.Domain.Entities;

public sealed class CustomerDocumentReference
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid CustomerId { get; private set; }
    public string ConstitutionCode { get; private set; } = string.Empty;
    public Guid DocumentDefinitionId { get; private set; }
    public string DocumentCode { get; private set; } = string.Empty;
    public string? DocumentCategory { get; private set; }
    public string? DocumentDescription { get; private set; }
    public bool IsMandatory { get; private set; }
    public bool? IsImageRequired { get; private set; }
    public string? Remarks { get; private set; }
    public bool IsCollected { get; private set; }
    public bool IsScanned { get; private set; }
    public string? Value { get; private set; }
    public string? ValueSource { get; private set; }
    public string? DmsReference { get; private set; }
    public string? ViewMetadata { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public void Initialize(
        string constitutionCode, Guid documentDefinitionId, string documentCode, string? documentCategory,
        string? documentDescription, bool isMandatory, bool? isImageRequired, string? remarks,
        bool isCollected, bool isScanned, string? value, string? valueSource, string? dmsReference,
        string? viewMetadata)
    {
        ConstitutionCode = constitutionCode;
        DocumentDefinitionId = documentDefinitionId;
        DocumentCode = documentCode;
        DocumentCategory = documentCategory;
        DocumentDescription = documentDescription;
        IsMandatory = isMandatory;
        IsImageRequired = isImageRequired;
        Remarks = remarks;
        IsCollected = isCollected;
        IsScanned = isScanned;
        Value = value;
        ValueSource = valueSource;
        DmsReference = dmsReference;
        ViewMetadata = viewMetadata;
        UpdatedAtUtc = DateTime.UtcNow;
    }
    public DateTime UpdatedAtUtc { get; private set; } = DateTime.UtcNow;
}
