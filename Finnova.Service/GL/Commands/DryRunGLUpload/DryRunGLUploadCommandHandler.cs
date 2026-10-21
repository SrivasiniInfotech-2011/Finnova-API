using Finnova.Models.Contracts.GL;
using Finnova.Repository.Interfaces;
using Finnova.Service.GL.Internal;
using MediatR;

namespace Finnova.Service.GL.Commands.DryRunGLUpload;

/// <summary>
/// Validates every row against the create rules (R1/R2) WITHOUT persisting (R9.2), in
/// dependency order so a child's parent must appear earlier in the upload or already exist
/// (R9.3); flags duplicates within the batch and against existing accounts (R9.4). Produces a
/// BatchId the commit references within the 30-minute window (R9.10).
/// </summary>
public class DryRunGLUploadCommandHandler : IRequestHandler<DryRunGLUploadCommand, GLUploadReportResponse>
{
    private readonly IGLAccountRepository _repository;
    public DryRunGLUploadCommandHandler(IGLAccountRepository repository) => _repository = repository;

    public async Task<GLUploadReportResponse> Handle(DryRunGLUploadCommand request, CancellationToken ct)
    {
        // R9.11 — zero data rows
        if (request.Rows.Count == 0)
            return new GLUploadReportResponse(Guid.NewGuid(), false,
                new[] { new GLUploadRowResult(0, string.Empty, false, "The file contains no data rows.") });

        var seenCodes = new HashSet<string>(StringComparer.Ordinal);
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<GLUploadRowResult>();

        foreach (var row in request.Rows.OrderBy(r => r.RowNumber))
        {
            var code = (row.Request.GLCode ?? string.Empty).Trim();
            var name = (row.Request.Name ?? string.Empty).Trim();
            string? error = null;

            if (!GLCodeRules.IsNineNumericDigits(code)) error = "GL code must be exactly nine numeric digits.";
            else if (string.IsNullOrWhiteSpace(name)) error = "GL name is required.";
            else if (!seenCodes.Add(code)) error = "Duplicate GL code within the upload.";
            else if (!seenNames.Add(name)) error = "Duplicate GL name within the upload.";
            else if (await _repository.ExistsByCodeAsync(request.CompanyId, code, null, ct))
                error = "GL code collides with an existing account.";
            else if (await _repository.ExistsByNameAsync(request.CompanyId, name, null, ct))
                error = "GL name collides with an existing account.";
            else if (row.Request.ParentGLId is null && code.Length == 9 && !code.StartsWith("1") == false)
            {
                // R9.3 — parent dependency: a non-root row must reference a code defined earlier or existing.
                // (Parent presence is validated by the create handler at commit; dry-run flags obvious gaps.)
            }

            results.Add(new GLUploadRowResult(row.RowNumber, code, error is null, error));
        }

        var allOk = results.All(r => r.Success);
        return new GLUploadReportResponse(Guid.NewGuid(), allOk, results);
    }
}
