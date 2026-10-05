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
            var isDcn = ctx.Request.Path.StartsWithSegments("/api/dcn",
                StringComparison.OrdinalIgnoreCase);
            var validationCode = isDcn ? "ERR-DCN-400" : isNationality ? "ERR-NAT-400" : "ERR-LKP-400";
            var fallbackCode = isDcn ? "ERR-DCN-500" : isNationality ? "ERR-NAT-500" : "ERR-LKP-500";

            var isCourt = ctx.Request.Path.StartsWithSegments("/api/court",StringComparison.OrdinalIgnoreCase);
            var validationCourtCode = isCourt ? "ERR-CRT-400" : isDcn ? "ERR-DCN-400" : isNationality ? "ERR-NAT-400" : "ERR-LKP-400";
            var fallbackCourtCode = isCourt ? "ERR-CRT-500" : isDcn ? "ERR-DCN-500" : isNationality ? "ERR-NAT-500" : "ERR-LKP-500";
            var isEntity = ctx.Request.Path.StartsWithSegments("/api/entity",    StringComparison.OrdinalIgnoreCase);
            var validationEntityCode = isEntity ? "ERR-ENT-400" : isCourt ? "ERR-CRT-400" : isDcn ? "ERR-DCN-400" : isNationality ? "ERR-NAT-400" : "ERR-LKP-400";
            var fallbackEntityCode = isEntity ? "ERR-ENT-500" : isCourt ? "ERR-CRT-500" : isDcn ? "ERR-DCN-500" : isNationality ? "ERR-NAT-500" : "ERR-LKP-500";
            var isDraweeBank = ctx.Request.Path.StartsWithSegments("/api/draweebank",StringComparison.OrdinalIgnoreCase);
            var validationDraweeBankCode = isDraweeBank ? "ERR-DRB-400" : validationCode;
            var fallbackDraweeBankCode = isDraweeBank ? "ERR-DRB-500" : fallbackCode;
            var (status, code, detail) = ex switch
            {
                // ---- existing lookup branch (unchanged) ----
                LookupLockedException => (StatusCodes.Status409Conflict, LookupLockedException.ErrorCode, ex.Message),
                LookupNotFoundException => (StatusCodes.Status404NotFound, "ERR-LKP-404", ex.Message),
                LookupInUseException => (StatusCodes.Status409Conflict, "ERR-LKP-409", ex.Message),

                // ---- nationality branch (mapped by dedicated exception type) ----
                NationalityNotFoundException => (StatusCodes.Status404NotFound, "ERR-NAT-404", ex.Message),
                NationalityDuplicateCodeException => (StatusCodes.Status409Conflict, NationalityDuplicateCodeException.ErrorCode, ex.Message),

                // ---- new org-hierarchy branch (typed, no message sniffing) ----
                OrganizationNodeNotFoundException => (404, "ERR-ORG-404", ex.Message),
                OrganizationNodeDuplicateCodeException => (409, "ERR-ORG-409", ex.Message),
                OrganizationNodeHasChildrenException => (409, "ERR-ORG-409", ex.Message),
                OrganizationNodeValidationException => (400, "ERR-ORG-400", ex.Message), // self-parent / cycle / depth / parent-not-exists

                // ---- new DCN branch (typed, no message sniffing) ----
                NumberingSchemeNotFoundException => (404, "ERR-DCN-404", ex.Message),
                NumberingSchemeDuplicateCodeException => (409, "ERR-DCN-409", ex.Message),
                NumberingSchemeScopeConflictException => (409, "ERR-DCN-409", ex.Message),
                NumberingSchemeInactiveException => (409, "ERR-DCN-409", ex.Message),
                NumberSequenceExhaustedException => (409, "ERR-DCN-409", ex.Message),
                NumberingSchemeValidationException => (400, "ERR-DCN-400", ex.Message), // template / scope-id consistency

                // ---- new Court branch (typed, no message sniffing) ----
                CourtNotFoundException => (404, "ERR-CRT-404", ex.Message),
                CourtDuplicateCodeException => (409, "ERR-CRT-409", ex.Message),

                // ---- new Entity branch (typed, no message sniffing) ----
                EntityNotFoundException => (404, "ERR-ENT-404", ex.Message),
                EntityDuplicateCodeException => (409, "ERR-ENT-409", ex.Message),

                // ---- new Drawee Bank branch (typed, no message sniffing) ----
                DraweeBankNotFoundException => (404, "ERR-DRB-404", ex.Message),
                ChallanRuleNotFoundException => (404, "ERR-DRB-404", ex.Message),
                DraweeBankDuplicateCodeException => (409, "ERR-DRB-409", ex.Message),
                DraweeBranchDuplicatePlaceCodeException => (409, "ERR-DRB-409", ex.Message),
                ChallanRuleDuplicateCodeException => (409, "ERR-DRB-409", ex.Message),
                DraweeBankValidationException => (400, "ERR-DRB-400", ex.Message),

                // ---- shared: validation failures and unhandled fallback (path-scoped code) ----
                FluentValidation.ValidationException v => (StatusCodes.Status400BadRequest, validationDraweeBankCode, v.Message),
                _ => (StatusCodes.Status500InternalServerError, fallbackDraweeBankCode, "Unexpected error.")

            };

            var problem = new ProblemDetails { Status = status, Title = code, Detail = detail };
            problem.Extensions["code"] = code;
            ctx.Response.StatusCode = status;
            await ctx.Response.WriteAsJsonAsync(problem);
        }
    }
}


