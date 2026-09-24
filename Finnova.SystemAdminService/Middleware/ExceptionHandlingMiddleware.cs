using Microsoft.AspNetCore.Mvc;
using Finnova.Models.Domain.Exceptions;

namespace Finnova.SystemAdminService.Middleware;

/// <summary>
/// Translates domain and validation exceptions into RFC 7807 <see cref="ProblemDetails"/>
/// responses with a stable <c>code</c> extension the frontend branches on.
/// <para>Lookup branch:</para>
/// <list type="bullet">
///   <item><see cref="LookupLockedException"/> -> 409 (ERR-LKP-005)</item>
///   <item><see cref="LookupNotFoundException"/> -> 404 (ERR-LKP-404)</item>
///   <item><see cref="LookupInUseException"/> -> 409 (ERR-LKP-409)</item>
/// </list>
/// <para>Nationality branch:</para>
/// <list type="bullet">
///   <item><see cref="NationalityNotFoundException"/> -> 404 (ERR-NAT-404)</item>
///   <item><see cref="NationalityDuplicateCodeException"/> -> 409 (ERR-NAT-409)</item>
/// </list>
/// <para>Shared branch (both features route validation through the same MediatR
/// <c>ValidationBehavior</c>, so a <see cref="FluentValidation.ValidationException"/> is not
/// distinguishable by type). The <c>code</c> family is selected from the request path so a
/// nationality request yields ERR-NAT-400 / ERR-NAT-500 and every other request keeps the
/// existing ERR-LKP-400 / ERR-LKP-500 behavior):</para>
/// <list type="bullet">
///   <item><see cref="FluentValidation.ValidationException"/> -> 400 (ERR-NAT-400 / ERR-LKP-400)</item>
///   <item>any other exception -> 500 (ERR-NAT-500 / ERR-LKP-500)</item>
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
            // The shared ValidationException and the unhandled fallback are not distinguishable
            // by exception type across the Lookup and Nationality features (both flow through the
            // same MediatR ValidationBehavior). Select the error-code family from the request path
            // so nationality requests surface ERR-NAT-* while everything else keeps ERR-LKP-*.
            var isNationality = ctx.Request.Path.StartsWithSegments("/api/nationality",
                StringComparison.OrdinalIgnoreCase);
            var validationCode = isNationality ? "ERR-NAT-400" : "ERR-LKP-400";
            var fallbackCode = isNationality ? "ERR-NAT-500" : "ERR-LKP-500";

            var (status, code, detail) = ex switch
            {
                // ---- existing lookup branch (unchanged) ----
                LookupLockedException => (StatusCodes.Status409Conflict, LookupLockedException.ErrorCode, ex.Message),
                LookupNotFoundException => (StatusCodes.Status404NotFound, "ERR-LKP-404", ex.Message),
                LookupInUseException => (StatusCodes.Status409Conflict, "ERR-LKP-409", ex.Message),

                // ---- nationality branch (mapped by dedicated exception type) ----
                NationalityNotFoundException => (StatusCodes.Status404NotFound, "ERR-NAT-404", ex.Message),
                NationalityDuplicateCodeException => (StatusCodes.Status409Conflict, NationalityDuplicateCodeException.ErrorCode, ex.Message),

                // ---- shared: validation failures and unhandled fallback (path-scoped code) ----
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
