using FluentValidation;

namespace Finnova.Service.GL.Queries.ValidateJournalVoucher;

public class ValidateJournalVoucherQueryValidator : AbstractValidator<ValidateJournalVoucherQuery>
{
    public ValidateJournalVoucherQueryValidator()
    {
        // R6.14 — required header attributes; 1..1000 lines
        RuleFor(x => x.Input.CompanyId).NotEmpty();
        RuleFor(x => x.Input.ValueDate).NotEmpty();
        RuleFor(x => x.Input.BranchId).NotEmpty();
        RuleFor(x => x.Input.Mode).IsInEnum();
        RuleFor(x => x.Input.Lines).NotEmpty().WithMessage("At least one line is required.");
        RuleFor(x => x.Input.Lines.Count).LessThanOrEqualTo(1000)
            .WithMessage("A voucher may not exceed 1000 lines.");
    }
}
