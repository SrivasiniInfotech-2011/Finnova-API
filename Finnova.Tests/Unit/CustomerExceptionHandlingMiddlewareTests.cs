using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Finnova.Models.Domain.Exceptions;
using Finnova.SystemAdminService.Middleware;
using Microsoft.AspNetCore.Http;
using Xunit;

public sealed class CustomerExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task Customer_validation_uses_customer_400_code()
        => await AssertResponseAsync("/api/customer-onboarding", new ValidationException(new[] { new ValidationFailure("GroupCode", "invalid") }), 400, "ERR-CUSTOMER-400");

    [Fact]
    public async Task Customer_not_found_uses_customer_404_code()
        => await AssertResponseAsync("/api/customer-onboarding/C001", new CustomerNotFoundException("C001"), 404, "ERR-CUSTOMER-404");

    [Fact]
    public async Task Customer_reference_failure_uses_semantic_code()
        => await AssertResponseAsync("/api/customer-onboarding", new CustomerLookupReferenceNotFoundException("GroupCode", "Customer", "Group", "1"), 400, "ERR-CUSTOMER-LOOKUP-REF");

    [Fact]
    public async Task Customer_conflict_uses_duplicate_code()
        => await AssertResponseAsync("/api/customer-onboarding", new CustomerDuplicateIdentityException(), 409, "ERR-CUSTOMER-DUPLICATE");

    [Fact]
    public async Task Customer_dependency_failure_uses_503_code()
        => await AssertResponseAsync("/api/customer-onboarding", new CustomerBoundaryUnavailableException("Lookup"), 503, "ERR-CUSTOMER-503");

    [Fact]
    public async Task Customer_unexpected_failure_uses_customer_500_code()
        => await AssertResponseAsync("/api/customer-onboarding", new InvalidOperationException("internal"), 500, "ERR-CUSTOMER-500");

    [Fact]
    public async Task Non_customer_path_keeps_existing_lookup_family()
        => await AssertResponseAsync("/api/lookup", new InvalidOperationException("internal"), 500, "ERR-LKP-500");

    private static async Task AssertResponseAsync(string path, Exception exception, int status, string code)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        await using var body = new MemoryStream();
        context.Response.Body = body;

        var middleware = new ExceptionHandlingMiddleware(_ => throw exception);
        await middleware.InvokeAsync(context);

        Assert.Equal(status, context.Response.StatusCode);
        body.Position = 0;
        using var json = await JsonDocument.ParseAsync(body);
        Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
        Assert.Equal(code, json.RootElement.GetProperty("title").GetString());
    }
}
