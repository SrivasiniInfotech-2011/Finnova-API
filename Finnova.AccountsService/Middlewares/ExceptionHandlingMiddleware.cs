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
            var isLoan = ctx.Request.Path.StartsWithSegments("/api/loan-accounts", StringComparison.OrdinalIgnoreCase);
            var isReceipt = ctx.Request.Path.StartsWithSegments("/api/receipts", StringComparison.OrdinalIgnoreCase);
            var (status, code, detail) = ex switch
            {
                // ---- GL branch (typed, no message sniffing) ----
                GLNotFoundException => (StatusCodes.Status404NotFound, "ERR-GL-404", ex.Message),
                GLDuplicateCodeException => (StatusCodes.Status409Conflict, "ERR-GL-409", ex.Message),
                GLRetiredCodeException => (StatusCodes.Status409Conflict, "ERR-GL-409", ex.Message),
                GLDuplicateNameException => (StatusCodes.Status409Conflict, "ERR-GL-409", ex.Message),
                GLConflictException => (StatusCodes.Status409Conflict, "ERR-GL-409", ex.Message),
                ApplicationProcessingException a => (a.ErrorCode switch
                {
                    "ERR-APP-404" => StatusCodes.Status404NotFound,
                    "ERR-APP-503" => StatusCodes.Status503ServiceUnavailable,
                    _ => StatusCodes.Status409Conflict
                }, a.ErrorCode, a.Message),

                // ---- shared: validation failures and unhandled fallback ----
                LoanAccountException l => (l.ErrorCode switch
                {
                    "ERR-ACC-404" => StatusCodes.Status404NotFound,
                    "ERR-ACC-403" => StatusCodes.Status403Forbidden,
                    "ERR-ACC-400" => StatusCodes.Status400BadRequest,
                    _ => StatusCodes.Status409Conflict
                }, l.ErrorCode, l.Message),
                ReceiptException rc => (rc.StatusCode, rc.ErrorCode, rc.Message),
                Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException =>
                    (StatusCodes.Status409Conflict, isReceipt ? "ERR-RCP-409" : isLoan ? "ERR-ACC-409" : "ERR-GL-409",
                     "The record was changed by another request. Refresh and retry."),
                DemandProcessingException d => (d.ErrorCode switch
                {
                    "ERR-DEMAND-NOT-FOUND" => StatusCodes.Status404NotFound,
                    "ERR-DEMAND-DATA-QUALITY" or "ERR-DEMAND-RECONCILIATION" or "ERR-BILLING-SCHEDULE-INCOMPLETE" => StatusCodes.Status422UnprocessableEntity,
                    "ERR-DEMAND-DEPENDENCY" => StatusCodes.Status503ServiceUnavailable,
                    "ERR-DEMAND-BILLING-INCOMPLETE" or "ERR-DEMAND-BILLING-SCOPE" or "ERR-BILLING-ALLOCATION-CONFLICT" or "ERR-BILLING-JOURNAL-INCOMPLETE" or "ERR-BILLING-SNAPSHOT-BLOCKED" or "ERR-DEMAND-IDEMPOTENCY-CONFLICT" or "ERR-DEMAND-CONCURRENT" or "ERR-DEMAND-SPOOL-NOT-READY" => StatusCodes.Status409Conflict,
                    "ERR-DEMAND-PDF-EXPORT" => StatusCodes.Status422UnprocessableEntity,
                    _ => StatusCodes.Status400BadRequest
                }, d.ErrorCode, d.Message),
                UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "ERR-AUTH-403", "You are not authorized for this Demand Processing scope."),
                Finnova.Models.Domain.Exceptions.ReceiptAppropriationException a => (a.ErrorCode switch
                {
                    "ERR-APPROP-NOT-FOUND" => StatusCodes.Status404NotFound,
                    "ERR-APPROP-403" => StatusCodes.Status403Forbidden,
                    "ERR-APPROP-RULE-CONFIGURATION" or "ERR-APPROP-ADVANCE-ACCOUNT" or "ERR-APPROP-RECONCILIATION" => StatusCodes.Status422UnprocessableEntity,
                    "ERR-APPROP-ALREADY-CONFIRMED" or "ERR-APPROP-IDEMPOTENCY-CONFLICT" or "ERR-APPROP-STALE-DATA" or "ERR-APPROP-CONCURRENCY" => StatusCodes.Status409Conflict,
                    "ERR-APPROP-DEPENDENCY" => StatusCodes.Status503ServiceUnavailable,
                    _ => StatusCodes.Status400BadRequest
                }, a.ErrorCode, a.Message),
                // ---- shared: validation failures and unhandled fallback ----
                FluentValidation.ValidationException v => (StatusCodes.Status400BadRequest,
                    isReceipt ? "ERR-RCP-400" : isLoan ? "ERR-ACC-400" : validationCode,
                    isReceipt ? string.Join("; ", v.Errors.Select(e => e.ErrorMessage).Distinct()) : v.Message),
            };

            var problem = new ProblemDetails { Status = status, Title = code, Detail = detail };
            problem.Extensions["code"] = code;
            problem.Extensions["message"] = detail;
            problem.Extensions["correlationId"] = ctx.TraceIdentifier;
            if (ex is ReceiptException { Rule: { } rule }) problem.Extensions["rule"] = rule;
            ctx.Response.StatusCode = status;
            await ctx.Response.WriteAsJsonAsync(problem);
        }
    }
}
