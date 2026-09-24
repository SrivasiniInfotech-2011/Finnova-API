namespace Finnova.Models.Contracts.Nationalities;

// Update editable field (R3). Only Name is editable; Code is immutable post-create.
public record UpdateNationalityNameRequest(
    string Name
);
