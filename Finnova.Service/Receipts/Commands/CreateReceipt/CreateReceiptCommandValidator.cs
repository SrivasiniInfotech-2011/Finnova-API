using System.Text.RegularExpressions;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Receipts;
using FluentValidation;

namespace Finnova.Service.Receipts.Commands.CreateReceipt;

/// <summary>Shape and field rules (HTTP 400). Reference and ledger rules live in the handler (HTTP 422).</summary>
public sealed class CreateReceiptCommandValidator : AbstractValidator<CreateReceiptCommand>
{
    private const int MaxRows = 50;
    private static readonly Regex SixDigits = new(@"^\d{6}$", RegexOptions.Compiled);
    private static readonly Regex NineDigits = new(@"^\d{9}$", RegexOptions.Compiled);

    public CreateReceiptCommandValidator()
    {
        RuleFor(x => x.Actor).NotEmpty().WithMessage("The acting user could not be determined.");
        RuleFor(x => x.Request).NotNull().WithMessage("Request body is required.");

        When(x => x.Request is not null, () =>
        {
            // ---- identity, enums, document number ----
            RuleFor(x => x.Request.ClientRequestId).NotEqual(Guid.Empty).WithMessage("ClientRequestId is required.");
            RuleFor(x => x.Request.EntryMode).IsInEnum().WithMessage("Entry Mode is invalid.");
            RuleFor(x => x.Request.ReceiptType).IsInEnum().WithMessage("Receipt Type is invalid.");
            RuleFor(x => x.Request.Mode).IsInEnum().WithMessage("Mode is invalid.");
            RuleFor(x => x.Request.ReceiptFrom).IsInEnum().WithMessage("Receipt From is invalid.");
            RuleFor(x => x.Request.DocNo)
                .NotEmpty().WithMessage("Doc No is required for Manual entry.")
                .MaximumLength(30).WithMessage("Doc No must be at most 30 characters.")
                .When(x => x.Request.EntryMode == ReceiptEntryMode.Manual);

            // ---- header ----
            RuleFor(x => x.Request.DocDate).NotEqual(default(DateOnly)).WithMessage("Doc Date is required.")
                .Must(d => d <= ReceiptClock.TodayIst()).WithMessage("Doc Date cannot be in the future.");
            RuleFor(x => x.Request.ValueDate).NotEqual(default(DateOnly)).WithMessage("Value Date is required.")
                .GreaterThanOrEqualTo(x => x.Request.DocDate).WithMessage("Value Date cannot be earlier than Doc Date.");
            RuleFor(x => x.Request.LineOfBusinessId).NotEqual(Guid.Empty).WithMessage("Line of Business is required.");
            RuleFor(x => x.Request.BranchId).NotEqual(Guid.Empty).WithMessage("Location is required.");
            RuleFor(x => x.Request.MarketingOfficerUserId).NotEqual(Guid.Empty).WithMessage("Marketing Officer is required.");
            RuleFor(x => x.Request.CustomerCode).NotEmpty().WithMessage("Customer Code is required.")
                .MaximumLength(20).WithMessage("Customer Code must be at most 20 characters.");
            RuleFor(x => x.Request.TrRefNo).MaximumLength(30).WithMessage("TR Ref No must be at most 30 characters.");
            RuleFor(x => x.Request.TrRefNo).Must(string.IsNullOrWhiteSpace)
                .When(x => x.Request.ReceiptType == ReceiptType.Temporary)
                .WithMessage("TR Ref No is not applicable to Temporary receipts.");
            RuleFor(x => x.Request.ReceiptFrom).NotEqual(ReceiptFrom.Customer)
                .When(x => x.Request.ReceiptType == ReceiptType.ThirdParty)
                .WithMessage("A Third-Party receipt requires a Receipt From other than Customer.");
            RuleFor(x => x.Request.DocAmount)
                .GreaterThan(0m).WithMessage("Doc Amount must be greater than zero.")
                .LessThanOrEqualTo(ReceiptCalculator.MaxAmount).WithMessage("Doc Amount must not exceed 99,99,99,999.99.")
                .Must(ReceiptCalculator.HasAtMostTwoDecimals).WithMessage("Doc Amount must have at most two decimal places.");

            // ---- Account Details lines ----
            RuleFor(x => x.Request.Lines)
                .Must(l => l is { Count: > 0 }).WithMessage("At least one Account Details line is required.")
                .Must(l => l is null || l.Count <= MaxRows).WithMessage($"At most {MaxRows} Account Details lines are allowed.");
            RuleForEach(x => x.Request.Lines).ChildRules(l =>
            {
                l.RuleFor(i => i.PrimeAccountNo).NotEmpty().WithMessage("Line: Prime Account No is required.")
                    .MaximumLength(100).WithMessage("Line: Prime Account No must be at most 100 characters.");
                l.RuleFor(i => i.SubAccountNo).MaximumLength(30).WithMessage("Line: Sub Account No must be at most 30 characters.");
                l.RuleFor(i => i.ComponentCode).NotEmpty().WithMessage("Line: Component is required.")
                    .MaximumLength(50).WithMessage("Line: Component must be at most 50 characters.");
                l.RuleFor(i => i.SlAccountCode).MaximumLength(20).WithMessage("Line: SL Account must be at most 20 characters.");
                l.RuleFor(i => i.Amount).GreaterThan(0m).WithMessage("Line: Amount must be greater than zero.")
                    .Must(ReceiptCalculator.HasAtMostTwoDecimals).WithMessage("Line: Amount must have at most two decimal places.");
            });
            RuleFor(x => x.Request).Must(r => (r.Lines ?? []).All(l => l.GlAccountCode is not null && NineDigits.IsMatch(l.GlAccountCode.Trim())))
                .When(x => x.Request.GlBased)
                .WithMessage("GL Based is Yes: every line requires a nine-digit GL Account.");

            // ---- Add/Less rows ----
            RuleFor(x => x.Request.AddLess)
                .Must(a => a is null || a.Count <= MaxRows).WithMessage($"At most {MaxRows} Add/Less rows are allowed.");
            RuleForEach(x => x.Request.AddLess).ChildRules(a =>
            {
                a.RuleFor(i => i.AccountNumber).NotEmpty().WithMessage("Add/Less: Account Number is required.")
                    .MaximumLength(100).WithMessage("Add/Less: Account Number must be at most 100 characters.");
                a.RuleFor(i => i.Kind).IsInEnum().WithMessage("Add/Less: Add or Less is required.");
                a.RuleFor(i => i.GlAccountCode).NotEmpty().WithMessage("Add/Less: GL Account is required.")
                    .Must(c => c is not null && NineDigits.IsMatch(c.Trim())).WithMessage("Add/Less: GL Account must be nine digits.");
                a.RuleFor(i => i.SlAccountCode).MaximumLength(20).WithMessage("Add/Less: SL Account must be at most 20 characters.");
                a.RuleFor(i => i.VehicleOrMachineNo).MaximumLength(50).WithMessage("Add/Less: Vehicle/Machine No must be at most 50 characters.");
                a.RuleFor(i => i.Remarks).MaximumLength(200).WithMessage("Add/Less: Remarks must be at most 200 characters.");
                a.RuleFor(i => i.Amount).GreaterThan(0m).WithMessage("Add/Less: Amount must be greater than zero.")
                    .Must(ReceiptCalculator.HasAtMostTwoDecimals).WithMessage("Add/Less: Amount must have at most two decimal places.");
            });

            // ---- Cash denominations ----
            RuleFor(x => x.Request).Must(r => (r.Denominations ?? []).Any(d => d.Count > 0))
                .When(x => x.Request.Mode == ReceiptMode.Cash)
                .WithMessage("At least one denomination with a count greater than zero is required for Cash.");
            RuleFor(x => x.Request).Must(r => !(r.Denominations ?? []).Any(d => d.Count > 0))
                .When(x => x.Request.Mode != ReceiptMode.Cash)
                .WithMessage(ReceiptMessages.DenominationsOnlyForCash);
            RuleForEach(x => x.Request.Denominations).ChildRules(d =>
            {
                d.RuleFor(i => i.Kind).IsInEnum().WithMessage("Denomination: kind is invalid.");
                d.RuleFor(i => i.Count).InclusiveBetween(0, 1_000_000)
                    .WithMessage("Denomination: count must be an integer from 0 to 1,000,000.");
                d.RuleFor(i => i).Must(i => CashDenominationCatalog.Contains(i.Kind, i.FaceValue))
                    .WithMessage("Denomination: the face value is not in the denomination catalog.");
            });
            RuleFor(x => x.Request.Denominations)
                .Must(d => d is null || d.Select(i => (i.Kind, i.FaceValue)).Distinct().Count() == d.Count)
                .WithMessage("Denomination: a face value appears more than once.");

            // ---- mode exclusivity ----
            RuleFor(x => x.Request).Must(r => r.Cheque is null && r.BankTransfer is null)
                .When(x => x.Request.Mode == ReceiptMode.Cash)
                .WithMessage(ReceiptMessages.BankDetailsNotApplicableToCash);
            RuleFor(x => x.Request.Cheque).NotNull().WithMessage("Cheque details are required for the Cheque mode.")
                .When(x => x.Request.Mode == ReceiptMode.Cheque);
            RuleFor(x => x.Request.BankTransfer).Null().WithMessage("Bank Transfer details are not applicable to Cheque.")
                .When(x => x.Request.Mode == ReceiptMode.Cheque);
            RuleFor(x => x.Request.BankTransfer).NotNull().WithMessage("Deposit Bank is required for the Bank Transfer mode.")
                .When(x => x.Request.Mode == ReceiptMode.BankTransfer);
            RuleFor(x => x.Request.Cheque).Null().WithMessage("Cheque details are not applicable to Bank Transfer.")
                .When(x => x.Request.Mode == ReceiptMode.BankTransfer);

            When(x => x.Request.Cheque is not null, () =>
            {
                RuleFor(x => x.Request.Cheque!.InstrumentNo).NotEmpty().WithMessage("Instrument No must be six digits.")
                    .Must(n => n is not null && SixDigits.IsMatch(n)).WithMessage("Instrument No must be six digits.");
                RuleFor(x => x.Request.Cheque!.DraweeBankId).NotEqual(Guid.Empty).WithMessage("Drawee Bank is required.");
                RuleFor(x => x.Request.Cheque!.DraweeBranchId).NotEqual(Guid.Empty).WithMessage("Drawee Branch is required.");
                RuleFor(x => x.Request.Cheque!.PaymentGatewayRefNo).MaximumLength(50)
                    .WithMessage("Payment Gateway Ref No must be at most 50 characters.");
                RuleFor(x => x.Request.Cheque!.AckNo).MaximumLength(50).WithMessage("ACK No must be at most 50 characters.");
            });

            When(x => x.Request.BankTransfer is not null, () =>
            {
                RuleFor(x => x.Request.BankTransfer!.FaBankAccountId).NotEqual(Guid.Empty).WithMessage("Deposit Bank is required.");
            });
        });
    }
}