using System.Globalization;
using Finnova.Models.Contracts.Receipts;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Models.Domain.Receipts;
using Finnova.Repository.Interfaces;
using Finnova.Service.DocumentNumberControl.Commands.IssueNumber;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Finnova.Service.Receipts.Commands.CreateReceipt;

/// <summary>
/// Saves a receipt (R2 to R11). All reads and rule checks run before the Document Number is issued so a rejected
/// request never consumes a sequence value (R2.12). Receipt, children and audit entry commit in one SaveChanges.
/// </summary>
public sealed class CreateReceiptCommandHandler(
    IReceiptRepository receipts,
    IReceiptAuditRepository audits,
    IReceiptReferenceRepository refs,
    IApplicationUnitOfWork uow,
    IMediator mediator)
    : IRequestHandler<CreateReceiptCommand, CreateReceiptResult>
{
    public async Task<CreateReceiptResult> Handle(CreateReceiptCommand command, CancellationToken ct)
    {
        var r = command.Request;

        // 1. Idempotency (R2.9)
        var existing = await receipts.GetByClientRequestIdAsync(r.ClientRequestId, ct);
        if (existing is not null) return new CreateReceiptResult(ReceiptMapper.ToResponse(existing, null), true);

        // 2. Header references (R3)
        var lob = await refs.GetActiveLobAsync(r.LineOfBusinessId, ct)
            ?? throw new ReceiptValidationException("Line of Business does not exist or is inactive.");
        var branch = await refs.GetActiveBranchAsync(r.BranchId, ct)
            ?? throw new ReceiptValidationException("Location does not exist, is inactive, or is not a branch.");
        var officer = await refs.GetActiveOfficerAsync(r.MarketingOfficerUserId, ct)
            ?? throw new ReceiptValidationException("Marketing Officer is not an active user.");
        var customer = await refs.GetActiveCustomerByCodeAsync(r.CustomerCode.Trim(), ct)
            ?? throw new ReceiptNotFoundException($"Customer '{r.CustomerCode.Trim()}' was not found or is inactive.");

        // 3. Amounts (R9)
        var addLess = r.AddLess ?? Array.Empty<ReceiptAddLessRequest>();
        var computed = ReceiptCalculator.DocAmount(
            r.Lines.Select(l => l.Amount), addLess.Select(a => (a.Kind, a.Amount)));
        if (computed <= 0m)
            throw new ReceiptBusinessRuleException(ReceiptRuleKeys.DocAmountNotPositive, "The computed Document Amount must be greater than zero.");
        if (computed != r.DocAmount)
            throw new ReceiptBusinessRuleException(ReceiptRuleKeys.DocAmountMismatch, ReceiptMessages.DocAmountMismatch);

        // 4. Cash denominations (R4)
        var denominations = new List<ReceiptCashDenomination>();
        if (r.Mode == ReceiptMode.Cash)
        {
            var rows = (r.Denominations ?? Array.Empty<CashDenominationRequest>()).Where(d => d.Count > 0).ToList();
            var total = ReceiptCalculator.DenominationTotal(rows.Select(d => (d.FaceValue, d.Count)));
            if (total != r.DocAmount)
                throw new ReceiptBusinessRuleException(ReceiptRuleKeys.CashDenominationMismatch, ReceiptMessages.DenominationMismatch);
            denominations.AddRange(rows.Select(d => new ReceiptCashDenomination
            {
                Kind = d.Kind,
                FaceValue = d.FaceValue,
                Count = d.Count,
                Amount = ReceiptCalculator.DenominationAmount(d.FaceValue, d.Count)
            }));
        }

        // 5. Contracts (R7)
        var accountNumbers = r.Lines.Select(l => l.PrimeAccountNo.Trim())
            .Concat(addLess.Select(a => a.AccountNumber.Trim()))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var contracts = await refs.GetContractsAsync(accountNumbers, ct);
        foreach (var no in accountNumbers)
        {
            if (!contracts.TryGetValue(no, out var c))
                throw new ReceiptBusinessRuleException(ReceiptRuleKeys.AccountNotFound, $"Account {no} was not found.");
            if (c.Status != AccountStatus.Active || c.ClosedAt is not null)
                throw new ReceiptBusinessRuleException(ReceiptRuleKeys.AccountNotLive, $"Account {no} is not live.");
            if (c.CustomerId != customer.Id)
                throw new ReceiptBusinessRuleException(ReceiptRuleKeys.AccountCustomerMismatch,
                    $"Account {no} does not belong to customer {customer.Code}.");
        }

        // 6. Mode-specific details (R5, R6)
        ReceiptChequeDetail? cheque = null;
        ReceiptBankTransferDetail? transfer = null;
        if (r.Mode == ReceiptMode.Cheque)
        {
            var c = r.Cheque!;
            var bank = await refs.GetActiveDraweeBankAsync(c.DraweeBankId, ct)
                ?? throw new ReceiptValidationException("Drawee Bank does not exist or is inactive.");
            var place = await refs.GetDraweeBranchAsync(c.DraweeBankId, c.DraweeBranchId, ct);
            if (place is null || !place.IsEffectiveOn(r.ValueDate))
                throw new ReceiptValidationException("Drawee Branch does not belong to the Drawee Bank or is not effective on the Value Date.");
            cheque = new ReceiptChequeDetail
            {
                InstrumentNo = c.InstrumentNo.Trim(),
                InstrumentDate = c.InstrumentDate ?? ReceiptClock.TodayIst(),
                DraweeBankId = bank.Id,
                DraweeBankCode = bank.Code,
                DraweeBankName = bank.Name,
                DraweeBranchId = place.Id,
                DraweePlaceName = place.PlaceName,
                PaymentGatewayRefNo = Blank(c.PaymentGatewayRefNo),
                AckNo = Blank(c.AckNo)
            };
        }
        else if (r.Mode == ReceiptMode.BankTransfer)
        {
            var fa = await refs.GetActiveFaBankAccountAsync(r.BankTransfer!.FaBankAccountId, ct)
                ?? throw new ReceiptValidationException("Deposit Bank does not exist or is inactive.");
            transfer = new ReceiptBankTransferDetail
            {
                FaBankAccountId = fa.Id,
                BankName = fa.BankName,
                AccountNo = fa.AccountNumber,
                MicrCode = fa.MicrCode,
                BankBranch = fa.BankBranch,
                GlCode = fa.GlCode,
                SlCode = fa.SlCode
            };
        }

        // 7. Line components (R8.3)
        var components = (await refs.GetActiveComponentsAsync(ct)).Select(x => x.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var l in r.Lines)
            if (!components.Contains(l.ComponentCode.Trim()))
                throw new ReceiptValidationException($"Component '{l.ComponentCode.Trim()}' is not an active receipt component.");

        // 8. GL credit rule (R8) and Add/Less GL (R9.6)
        var glCache = new Dictionary<string, RefGl>(StringComparer.Ordinal);
        async Task<RefGl> ResolveGlAsync(string code)
        {
            var key = code.Trim();
            if (glCache.TryGetValue(key, out var hit)) return hit;
            var gl = await refs.GetPostableGlByCodeAsync(customer.CompanyId, key, ct)
                ?? throw new ReceiptBusinessRuleException(ReceiptRuleKeys.GlNotPostable,
                    $"GL account {key} is not an active, approved, postable account.");
            glCache[key] = gl;
            return gl;
        }

        RefGl? customerGl = null;
        if (!r.GlBased)
        {
            customerGl = customer.GlAccountId is { } gid
                ? await refs.GetPostableGlByIdAsync(customer.CompanyId, gid, ct)
                : null;
            if (customerGl is null)
                throw new ReceiptBusinessRuleException(ReceiptRuleKeys.CustomerGlNotConfigured,
                    "The customer GL account is not configured or is not an active, approved, postable account.");
        }

        var lineEntities = new List<ReceiptLine>();
        var lineNo = 0;
        foreach (var l in r.Lines)
        {
            lineNo++;
            var contract = contracts[l.PrimeAccountNo.Trim()];
            var gl = r.GlBased ? await ResolveGlAsync(l.GlAccountCode!) : customerGl!;
            lineEntities.Add(new ReceiptLine
            {
                LineNo = lineNo,
                LoanAccountId = contract.Id,
                PrimeAccountNo = contract.AccountNumber,
                SubAccountNo = Blank(l.SubAccountNo),
                AccountDescription = contract.AccountName,
                ComponentCode = l.ComponentCode.Trim(),
                CreditGlAccountId = gl.Id,
                CreditGlCode = gl.Code,
                SlAccountCode = r.GlBased ? Blank(l.SlAccountCode) : null,   // ignored when GL Based = No (A12)
                Amount = l.Amount
            });
        }

        var addLessEntities = new List<ReceiptAddLess>();
        var adjNo = 0;
        foreach (var a in addLess)
        {
            adjNo++;
            var contract = contracts[a.AccountNumber.Trim()];
            var gl = await ResolveGlAsync(a.GlAccountCode);
            addLessEntities.Add(new ReceiptAddLess
            {
                LineNo = adjNo,
                LoanAccountId = contract.Id,
                AccountNumber = contract.AccountNumber,
                VehicleOrMachineNo = Blank(a.VehicleOrMachineNo),
                Kind = a.Kind,
                AccountDescription = contract.AccountName,
                GlAccountId = gl.Id,
                GlAccountCode = gl.Code,
                SlAccountCode = Blank(a.SlAccountCode),
                Amount = a.Amount,
                Remarks = Blank(a.Remarks)
            });
        }

        // 9. Document Number (R2.1, R2.5, R2.6, R2.8)
        string docNo;
        long? sequence = null;
        if (r.EntryMode == ReceiptEntryMode.Manual)
        {
            docNo = r.DocNo!.Trim();
            if (await receipts.DocNoExistsAsync(r.ReceiptType, docNo, ct)) throw new ReceiptDuplicateDocNoException();
        }
        else
        {
            try
            {
                var issued = await mediator.Send(new IssueNumberCommand(
                    DocumentTypeFor(r.ReceiptType), NumberScope.Global, null, command.Actor,
                    r.LineOfBusinessId, r.BranchId, UseFallback: true), ct);
                docNo = issued.Number;
                sequence = issued.SequenceValue;
            }
            catch (Exception ex) when (ex is Finnova.Models.Domain.Exceptions.NumberingSchemeNotFoundException
                                          or Finnova.Models.Domain.Exceptions.NumberingSchemeInactiveException
                                          or Finnova.Models.Domain.Exceptions.NumberingSchemeValidationException)
            {
                throw new ReceiptBusinessRuleException(ReceiptRuleKeys.NumberingSchemeNotConfigured,
                    $"No active numbering scheme is configured for {r.ReceiptType} receipts.");
            }
        }

        // 10. Persist atomically (R2.3, R11.1)
        var now = DateTime.UtcNow;
        var receipt = new Receipt
        {
            ClientRequestId = r.ClientRequestId,
            DocNo = docNo,
            SequenceNumber = sequence,
            EntryMode = r.EntryMode,
            ReceiptType = r.ReceiptType,
            Status = ReceiptStatus.PendingAuthorization,
            DocDate = r.DocDate,
            ValueDate = r.ValueDate,
            LineOfBusinessId = lob.Id,
            LineOfBusinessName = lob.Name,
            BranchId = branch.Id,
            BranchCode = branch.Code,
            BranchName = branch.Name,
            MarketingOfficerUserId = officer.Id,
            MarketingOfficerName = officer.Name,
            Mode = r.Mode,
            ReceiptFrom = r.ReceiptFrom,
            GlBased = r.GlBased,
            TrRefNo = Blank(r.TrRefNo),
            CustomerId = customer.Id,
            CustomerCode = customer.Code,
            CustomerName = customer.Name,
            CompanyId = customer.CompanyId,
            CurrencyCode = "INR",
            DocAmount = computed,
            AmountInWords = AmountInWords.ToWords(computed),
            CreatedBy = command.Actor,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Lines = lineEntities,
            AddLess = addLessEntities,
            Denominations = denominations,
            Cheque = cheque,
            BankTransfer = transfer
        };

        var audit = new ReceiptAuditEntry
        {
            ReceiptId = receipt.Id,
            Action = "Created",
            DocNo = docNo,
            SequenceNumber = sequence,
            Actor = command.Actor,
            CorrelationId = command.CorrelationId,
            OccurredAtUtc = now,
            // Non-sensitive only: no instrument numbers, account numbers, or contact data (R11.6).
            ChangeSummary = string.Create(CultureInfo.InvariantCulture,
                $"Receipt {docNo} created: {r.ReceiptType} {r.Mode}, amount {computed:0.00} INR, {lineEntities.Count} line(s), {addLessEntities.Count} adjustment(s).")
        };

        await receipts.AddAsync(receipt, ct);
        await audits.AddAsync(audit, ct);
        try
        {
            await uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // A concurrent double submit or Manual Doc No race hit a unique index: resolve to the stored outcome.
            uow.Context.ChangeTracker.Clear();
            var raced = await receipts.GetByClientRequestIdAsync(r.ClientRequestId, ct);
            if (raced is not null) return new CreateReceiptResult(ReceiptMapper.ToResponse(raced, null), true);
            if (r.EntryMode == ReceiptEntryMode.Manual && await receipts.DocNoExistsAsync(r.ReceiptType, docNo, ct))
                throw new ReceiptDuplicateDocNoException();
            throw;
        }

        return new CreateReceiptResult(ReceiptMapper.ToResponse(receipt, null), false);
    }

    private static string DocumentTypeFor(ReceiptType type) => type switch
    {
        ReceiptType.Temporary => ReceiptDocumentTypes.Temporary,
        ReceiptType.Regular => ReceiptDocumentTypes.Regular,
        _ => ReceiptDocumentTypes.ThirdParty
    };

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}