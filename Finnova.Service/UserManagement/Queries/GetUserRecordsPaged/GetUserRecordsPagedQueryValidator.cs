using FluentValidation;

namespace Finnova.Service.UserManagement.Queries.GetUserRecordsPaged;

public class GetUserRecordsPagedQueryValidator : AbstractValidator<GetUserRecordsPagedQuery>
{
    public GetUserRecordsPagedQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1)
            .WithMessage("Page number is out of range.");                      // R13.6
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100)
            .WithMessage("Page size is out of range.");                        // R13.7
        RuleFor(x => x.Kind!.Value).IsInEnum().When(x => x.Kind.HasValue);
    }
}
