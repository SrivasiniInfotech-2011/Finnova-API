using FluentValidation;

namespace Finnova.Service.Entity.Queries.GetEntitiesPaged;

public class GetEntitiesPagedQueryValidator : AbstractValidator<GetEntitiesPagedQuery>
{
    public GetEntitiesPagedQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);           // R3.8
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);      // R3.8

        RuleFor(x => x.SearchTerm)
            .Must(t => t == null || t.Trim().Length <= 200)
            .WithMessage("Search term must not exceed 200 characters.");   // R3.5

        // Only validate the filter when supplied; null means "no filter" (R3.2).
        RuleFor(x => x.EntityType!.Value)
            .IsInEnum().WithMessage("Entity type filter is invalid.")      // R3.3
            .When(x => x.EntityType.HasValue);
    }
}