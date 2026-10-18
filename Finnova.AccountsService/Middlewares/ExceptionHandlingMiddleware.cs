using Finnova.Models.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;
using System.Data;

namespace Finnova.AccountsService.Middlewares;

/// <summary>
/// Translates GL domain exceptions and FluentValidation failures into RFC 7807
/// <see cref="ProblemDetails"/> responses with a stable <c>code</c> extension the frontend
/// branches on. GL error family is path-scoped to <c>/api/gl</c>:
/// <list type="bullet">
///   <item><see cref="GLNotFoundException"/> -> 404 (ERR-GL-404)</item>
///   <item><see cref="GLDuplicateCodeException"/> / <see cref="GLRetiredCodeException"/> /
///         <see cref="GLDuplicateNameException"/> / <see cref="GLConflictException"/> -> 409 (ERR-GL-409)</item>
///   <item><see cref="FluentValidation.ValidationException"/> -> 400 (ERR-GL-400)</item>
///   <item>any other exception -> 500 (ERR-GL-500)</item>
/// </list>
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;

    public ExceptionHandlingMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (Exception ex)
        {
            var isGl = ctx.Request.Path.StartsWithSegments("/api/gl", StringComparison.OrdinalIgnoreCase);
            var validationCode = isGl ? "ERR-GL-400" : "ERR-GL-400";
            var fallbackCode = isGl ? "ERR-GL-500" : "ERR-GL-500";

            var (status, code, detail) = ex switch
            {
                // ---- GL branch (typed, no message sniffing) ----
                GLNotFoundException => (StatusCodes.Status404NotFound, "ERR-GL-404", ex.Message),
                GLDuplicateCodeException => (StatusCodes.Status409Conflict, "ERR-GL-409", ex.Message),
                GLRetiredCodeException => (StatusCodes.Status409Conflict, "ERR-GL-409", ex.Message),
                GLDuplicateNameException => (StatusCodes.Status409Conflict, "ERR-GL-409", ex.Message),
                GLConflictException => (StatusCodes.Status409Conflict, "ERR-GL-409", ex.Message),

                // ---- shared: validation failures and unhandled fallback ----
                FluentValidation.ValidationException v => (StatusCodes.Status400BadRequest, validationCode, v.Message),
                _ => (StatusCodes.Status500InternalServerError, fallbackCode, "Unexpected error.")
            };

            var problem = new ProblemDetails { Status = status, Title = code, Detail = detail };
            problem.Extensions["code"] = code;
            ctx.Response.StatusCode = status;
            await ctx.Response.WriteAsJsonAsync(problem);
        }
    }
}
