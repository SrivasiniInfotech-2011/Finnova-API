using Finnova.Models.Contracts.GL;
using MediatR;

namespace Finnova.Service.GL.Commands.DryRunGLUpload;

public record GLUploadRow(int RowNumber, CreateGLAccountRequest Request);

public record DryRunGLUploadCommand(Guid CompanyId, IReadOnlyList<GLUploadRow> Rows, string ActingUser)
    : IRequest<GLUploadReportResponse>;
