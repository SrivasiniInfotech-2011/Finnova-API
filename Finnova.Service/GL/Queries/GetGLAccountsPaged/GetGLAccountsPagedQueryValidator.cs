using FluentValidation;

namespace Finnova.Service.GL.Queries.GetGLAccountsPaged;

public class GetGLAccountsPagedQueryValidator : AbstractValidator<GetGLAccountsPagedQuery>
{
    public GetGLAccountsPagedQueryValidator()
    {
        // R8.5 — page >= 1, 1 <= pageSize <= 100
        RuleFor(x => x.Filter.Page).GreaterThanOrEqualTo(1).WithMessage("Page must be at least 1.");
        RuleFor(x => x.Filter.PageSize).InclusiveBetween(1, 100)
            .WithMessage("Page size must be between 1 and 100.");
    }
}
