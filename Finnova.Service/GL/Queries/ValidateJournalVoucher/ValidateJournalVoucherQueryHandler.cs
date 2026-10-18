using Finnova.Models.Contracts.GL;
using Finnova.Repository.Interfaces;
using Finnova.Service.GL.Internal;
using MediatR;

namespace Finnova.Service.GL.Queries.ValidateJournalVoucher;

public class ValidateJournalVoucherQueryHandler
    : IRequestHandler<ValidateJournalVoucherQuery, VoucherValidationResult>
{
    private readonly IGLAccountRepository _repository;
    public ValidateJournalVoucherQueryHandler(IGLAccountRepository repository) => _repository = repository;

    public async Task<VoucherValidationResult> Handle(ValidateJournalVoucherQuery request, CancellationToken ct)
    {
        var input = request.Input;
        var ctx = new JvVoucherContext(input.CompanyId, input.ValueDate, input.BranchId, input.Mode);

        var results = new List<JvLineResult>();
        foreach (var line in input.Lines)
        {
            var gl = await _repository.GetWithBranchesAsync(line.GLAccountId, ct);
            GLValidationSnapshot? snapshot = gl is null ? null : new GLValidationSnapshot(
                gl.Id, gl.CompanyId, gl.Status, gl.EffectiveFrom, gl.EffectiveTo, gl.IsPostable,
                gl.AllowManualJV, gl.BranchApplicabilities.Select(b => b.BranchId).ToList(),
                gl.CostCentreMandatory, gl.ProfitCentreMandatory, gl.CurrencyId, gl.GLType);

            var jvLine = new JvLine(line.LineNumber, line.GLAccountId, line.CurrencyId,
                line.CostCentreId, line.ProfitCentreId, line.SubLedgerReference);

            var reasons = JvLineValidator.Evaluate(jvLine, snapshot, ctx);
            results.Add(new JvLineResult(line.LineNumber, reasons.Count == 0, reasons));
        }

        return new VoucherValidationResult(results);
    }
}
