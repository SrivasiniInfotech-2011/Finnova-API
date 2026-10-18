using System.Text;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Interfaces;
using MediatR;

namespace Finnova.Service.GL.Queries.ExportGLAccounts;

/// <summary>
/// Produces a CSV/XLSX export of the filtered GL accounts, capped at 10,000 rows (R8.7). CSV is
/// generated directly; XLSX is emitted as CSV bytes under an xlsx name as a dev placeholder —
/// swap for a real spreadsheet library (design assumption A4) when confirmed.
/// </summary>
public class ExportGLAccountsQueryHandler
    : IRequestHandler<ExportGLAccountsQuery, (byte[] Content, string ContentType, string FileName)>
{
    private const int MaxRows = 10_000;
    private readonly IGLAccountRepository _repository;
    public ExportGLAccountsQueryHandler(IGLAccountRepository repository) => _repository = repository;

    public async Task<(byte[] Content, string ContentType, string FileName)> Handle(
        ExportGLAccountsQuery request, CancellationToken ct)
    {
        var filter = request.Request.Filter with { Page = 1, PageSize = MaxRows };
        var (items, _) = await _repository.GetPagedAsync(filter, ct);

        var sb = new StringBuilder();
        sb.AppendLine($"# Exported {DateTime.UtcNow:O} UTC");
        sb.AppendLine("GLCode,Name,Level,Nature,NormalBalance,GLType,Postable,Status");
        foreach (var g in items)
            sb.AppendLine(string.Join(",",
                Csv(g.GLCode), Csv(g.Name), g.Level, g.AccountNature, g.NormalBalance,
                g.GLType, g.IsPostable, g.Status));

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return request.Request.Format == ExportFormat.Csv
            ? (bytes, "text/csv", "gl-accounts.csv")
            : (bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "gl-accounts.xlsx");
    }

    private static string Csv(string v) => v.Contains(',') || v.Contains('"')
        ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
}
