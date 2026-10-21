namespace Finnova.Models.Contracts.GL;

/// <summary>A single parsed upload row the UI sends for dry-run/commit (R9).</summary>
public record GLUploadRowRequest(int RowNumber, CreateGLAccountRequest Row);

public record GLBulkUploadRequest(Guid CompanyId, IReadOnlyList<GLUploadRowRequest> Rows);

public record GLBulkCommitRequest(Guid CompanyId, Guid BatchId, IReadOnlyList<GLUploadRowRequest> Rows);
