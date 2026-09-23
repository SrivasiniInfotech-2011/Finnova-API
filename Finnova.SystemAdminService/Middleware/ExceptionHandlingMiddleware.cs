using Microsoft.AspNetCore.Mvc;
using Finnova.Models.Domain.Exceptions;

namespace Finnova.SystemAdminService.Middleware;

/// <summary>
/// Translates domain and validation exceptions into RFC 7807 <see cref="ProblemDetails"/>
/// responses with a stable <c>code</c> extension the frontend branches on:
/// <list type="bullet">
///   <item><see cref="LookupLockedException"/> -> 409 (ERR-LKP-005)</item>
///   <item><see cref="LookupNotFoundException"/> -> 404 (ERR-LKP-404)</item>
///   <item><see cref="LookupInUseException"/> -> 409 (ERR-LKP-409)</item>
///   <item><see cref="FluentValidation.ValidationException"/> -> 400 (ERR-LKP-400)</item>
///   <item>any other exception -> 500 (ERR-LKP-500)</item>
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
            var (status, code, detail) = ex switch
            {
                LookupLockedException => (StatusCodes.Status409Conflict, LookupLockedException.ErrorCode, ex.Message),
                LookupNotFoundException => (StatusCodes.Status404NotFound, "ERR-LKP-404", ex.Message),
                LookupInUseException => (StatusCodes.Status409Conflict, "ERR-LKP-409", ex.Message),
                FluentValidation.ValidationException v => (StatusCodes.Status400BadRequest, "ERR-LKP-400", v.Message),
                _ => (StatusCodes.Status500InternalServerError, "ERR-LKP-500", "Unexpected error.")
            };

            var problem = new ProblemDetails { Status = status, Title = code, Detail = detail };
            problem.Extensions["code"] = code;
            ctx.Response.StatusCode = status;
            await ctx.Response.WriteAsJsonAsync(problem);
        }
    }
}
