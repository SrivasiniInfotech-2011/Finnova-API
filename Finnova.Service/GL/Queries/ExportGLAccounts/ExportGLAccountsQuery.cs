using Finnova.Models.Contracts.GL;
using MediatR;

namespace Finnova.Service.GL.Queries.ExportGLAccounts;

/// <summary>Returns the export file bytes + content type + filename for the filtered result (R8.7/R8.10).</summary>
public record ExportGLAccountsQuery(ExportGLRequest Request)
    : IRequest<(byte[] Content, string ContentType, string FileName)>;
