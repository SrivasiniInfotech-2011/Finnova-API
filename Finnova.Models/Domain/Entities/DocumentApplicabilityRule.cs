namespace Finnova.Models.Domain.Entities;

/// <summary>
/// Maps a document to one Constitution and one Entity Role with an Is Mandatory flag (R4).
/// The (DocumentId, Constitution, EntityRole) combination is unique.
/// </summary>
public class DocumentApplicabilityRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }

    public string Constitution { get; set; } = string.Empty;   // Lookup CONSTITUTION code
    public string EntityRole { get; set; } = string.Empty;     // Lookup APPLICABLE_ENTITY code
    public bool IsMandatory { get; set; }

    public Document? Document { get; set; }
}
