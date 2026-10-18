using Finnova.Models.Contracts.GL;
using Finnova.Service.GL.Commands.DryRunGLUpload;
using MediatR;

namespace Finnova.Service.GL.Commands.CommitGLUpload;

public record CommitGLUploadCommand(
    Guid CompanyId, Guid BatchId, IReadOnlyList<GLUploadRow> Rows, string ActingUser)
    : IRequest<GLUploadReportResponse>;
