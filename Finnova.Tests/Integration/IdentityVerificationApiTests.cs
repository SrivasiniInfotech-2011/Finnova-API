using System.Linq.Expressions;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Finnova.Models.Contracts.IdentityVerification;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Interfaces;
using Finnova.Service.IdentityVerification.Adapters;
using Finnova.Service.IdentityVerification.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using AdapterOperationType = Finnova.Models.Domain.Enums.VerificationOperationType;
using ModelOperationType = Finnova.Models.Domain.Enums.VerificationOperationType;

namespace Finnova.Tests.Integration;

/// <summary>
/// Task 10.3 — API integration coverage for identity verification.
///
/// The derived factory replaces provider adapters, protected storage, and persistence with
/// deterministic in-memory registrations. The tests therefore exercise the real SystemAdmin host,
/// authentication policy, middleware, controllers, MediatR handlers, and route contracts without
/// making provider calls or requiring Azure credentials.
/// </summary>
public sealed class IdentityVerificationApiTests : IClassFixture<IdentityVerificationAppFactory>
{
    private readonly IdentityVerificationAppFactory factory;

    public IdentityVerificationApiTests(IdentityVerificationAppFactory factory) => this.factory = factory;

    private HttpClient Client(string? token = null)
    {
        var client = factory.CreateClient();
        if (token is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static readonly Func<HttpRequestMessage>[] ProtectedRequests =
    [
        () => new(HttpMethod.Post, "/api/identity-verification/verify-pan-name")
        {
            Content = new MultipartFormDataContent()
        },
        () => new(HttpMethod.Post, "/api/identity-verification/verify-aadhar-address")
        {
            Content = new MultipartFormDataContent()
        },
        () => new(HttpMethod.Post, "/api/identity-verification/workflows/full")
        {
            Content = JsonContent.Create(new { workflowType = "Full", inputMode = "Partial" })
        },
        () => new(HttpMethod.Get, "/api/identity-verification/records"),
        () => new(HttpMethod.Get, $"/api/identity-verification/documents/{Guid.NewGuid()}"),
        () => new(HttpMethod.Post, "/api/identity-verification/manual/comparison")
        {
            Content = JsonContent.Create(new { operationType = "PanNameVerification", outcome = "Match", sourceRecordId = Guid.NewGuid(), safeReason = "reviewed" })
        }
    ];

    [Fact]
    public async Task ProtectedEndpoints_WithoutToken_Return401()
    {
        foreach (var requestFactory in ProtectedRequests)
        {
            using var response = await Client().SendAsync(requestFactory());
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task ProtectedEndpoints_WithAuthenticatedNonSystemAdmin_Return403()
    {
        foreach (var requestFactory in ProtectedRequests)
        {
            using var response = await Client(DevJwt.NoRole()).SendAsync(requestFactory());
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("tampered")]
    public async Task ProtectedEndpoints_WithInvalidBearer_Return401(string tokenKind)
    {
        var token = tokenKind == "expired" ? DevJwt.Expired() : DevJwt.Tampered();
        using var response = await Client(token).SendAsync(ProtectedRequests[3]());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ComparisonRoutes_WithSystemAdmin_ReturnIndependentNormalizedOutcomes()
    {
        using var pan = await PostComparisonAsync(
            "/api/identity-verification/verify-pan-name", "Anita Sharma");
        using var address = await PostComparisonAsync(
            "/api/identity-verification/verify-aadhar-address", "Mumbai, Maharashtra");

        Assert.Equal(HttpStatusCode.OK, pan.StatusCode);
        Assert.Equal(HttpStatusCode.OK, address.StatusCode);

        var panResult = await JsonAsync(pan);
        var addressResult = await JsonAsync(address);
        Assert.Equal("PanNameVerification", panResult.GetProperty("operationType").GetString());
        Assert.Equal("Match", panResult.GetProperty("comparisonOutcome").GetString());
        Assert.Equal("AadhaarAddressVerification", addressResult.GetProperty("operationType").GetString());
        Assert.Equal("NonMatch", addressResult.GetProperty("comparisonOutcome").GetString());
        Assert.False(panResult.TryGetProperty("validationStatus", out _));
        Assert.False(addressResult.TryGetProperty("validationStatus", out _));
    }

    [Fact]
    public async Task PanAndAadhaarWorkflows_ReturnIndependentIdentifierAndComparisonResults()
    {
        using var panResponse = await Client(DevJwt.SystemAdmin()).PostAsJsonAsync(
            "/api/identity-verification/workflows/pan-and-name",
            new
            {
                workflowType = "PanAndName",
                inputMode = "Partial",
                pan = "ABCDE1234F",
                panSubmittedName = "Anita Sharma",
                panSource = new { extractedValues = new { documentName = "Anita Sharma" }, mappingProfile = "test" }
            });

        using var aadhaarResponse = await Client(DevJwt.SystemAdmin()).PostAsJsonAsync(
            "/api/identity-verification/workflows/aadhaar-and-address",
            new
            {
                workflowType = "AadhaarAndAddress",
                inputMode = "Partial",
                aadhaarNumber = "123456789012",
                aadhaarSubmittedAddress = "Mumbai, Maharashtra",
                aadhaarSource = new { extractedValues = new { documentAddress = "Pune, Maharashtra" }, mappingProfile = "test" }
            });

        Assert.Equal(HttpStatusCode.OK, panResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, aadhaarResponse.StatusCode);

        var pan = await JsonAsync(panResponse);
        var aadhaar = await JsonAsync(aadhaarResponse);
        Assert.Equal("Success", pan.GetProperty("panIdentifierResult").GetProperty("validationStatus").GetString());
        Assert.Equal("Match", pan.GetProperty("panNameResult").GetProperty("comparisonOutcome").GetString());
        Assert.Equal("Success", aadhaar.GetProperty("aadhaarIdentifierResult").GetProperty("validationStatus").GetString());
        Assert.Equal("NonMatch", aadhaar.GetProperty("aadhaarAddressResult").GetProperty("comparisonOutcome").GetString());
        Assert.NotEqual(Guid.Empty.ToString(), pan.GetProperty("sessionId").GetString());
        Assert.NotEqual(Guid.Empty.ToString(), aadhaar.GetProperty("sessionId").GetString());
    }

    [Fact]
    public async Task FullWorkflow_PartialInput_PreservesCompletedIndependentResults()
    {
        using var response = await Client(DevJwt.SystemAdmin()).PostAsJsonAsync(
            "/api/identity-verification/workflows/full",
            new
            {
                workflowType = "Full",
                inputMode = "Partial",
                pan = "ABCDE1234F",
                panSubmittedName = "Anita Sharma",
                panSource = new { extractedValues = new { documentName = "Anita Sharma" }, mappingProfile = "test" },
                aadhaarNumber = "123456789012"
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonAsync(response);
        Assert.True(body.TryGetProperty("panIdentifierResult", out _));
        Assert.True(body.TryGetProperty("panNameResult", out _));
        Assert.True(body.TryGetProperty("aadhaarIdentifierResult", out _));
        Assert.True(body.TryGetProperty("aadhaarAddressResult", out var aadhaarAddressResult));
        Assert.Equal(JsonValueKind.Null, aadhaarAddressResult.ValueKind);
    }

    [Fact]
    public async Task FullWorkflow_ReturnsAllFourAddressableResultDomains()
    {
        using var response = await Client(DevJwt.SystemAdmin()).PostAsJsonAsync(
            "/api/identity-verification/workflows/full",
            new
            {
                workflowType = "Full",
                inputMode = "Partial",
                pan = "ABCDE1234F",
                panSubmittedName = "Anita Sharma",
                panSource = new { extractedValues = new { documentName = "Anita Sharma" }, mappingProfile = "test" },
                aadhaarNumber = "123456789012",
                aadhaarSubmittedAddress = "Mumbai, Maharashtra",
                aadhaarSource = new { extractedValues = new { documentAddress = "Pune, Maharashtra" }, mappingProfile = "test" }
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonAsync(response);
        Assert.Equal("Success", body.GetProperty("panIdentifierResult").GetProperty("validationStatus").GetString());
        Assert.Equal("Match", body.GetProperty("panNameResult").GetProperty("comparisonOutcome").GetString());
        Assert.Equal("Success", body.GetProperty("aadhaarIdentifierResult").GetProperty("validationStatus").GetString());
        Assert.Equal("NonMatch", body.GetProperty("aadhaarAddressResult").GetProperty("comparisonOutcome").GetString());
    }

    [Fact]
    public async Task HistoryQuery_SupportsOperationAndOutcomeFilters()
    {
        using var comparison = await PostComparisonAsync(
            "/api/identity-verification/verify-pan-name", "Anita Sharma");
        Assert.Equal(HttpStatusCode.OK, comparison.StatusCode);

        using var response = await Client(DevJwt.SystemAdmin()).GetAsync(
            "/api/identity-verification/records?operationType=PanNameVerification&comparisonOutcome=Match&page=1&pageSize=20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonAsync(response);
        Assert.True(body.GetProperty("total").GetInt32() >= 1);
        Assert.All(body.GetProperty("data").EnumerateArray(), item =>
        {
            Assert.Equal("PanNameVerification", item.GetProperty("operationType").GetString());
            Assert.Equal("Match", item.GetProperty("comparisonOutcome").GetString());
        });
    }

    [Fact]
    public async Task History_IsImmutable_NoUpdateOrDeleteSurfaceAndExistingRowsRemain()
    {
        using var before = await Client(DevJwt.SystemAdmin()).GetAsync("/api/identity-verification/records?page=1&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        var beforeBody = await JsonAsync(before);
        var beforeTotal = beforeBody.GetProperty("total").GetInt32();

        using var update = await Client(DevJwt.SystemAdmin()).PutAsJsonAsync(
            $"/api/identity-verification/records/{Guid.NewGuid()}", new { safeMessage = "changed" });
        using var delete = await Client(DevJwt.SystemAdmin()).DeleteAsync(
            $"/api/identity-verification/records/{Guid.NewGuid()}");
        Assert.Contains(update.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });
        Assert.Contains(delete.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });

        using var after = await Client(DevJwt.SystemAdmin()).GetAsync("/api/identity-verification/records?page=1&pageSize=100");
        var afterBody = await JsonAsync(after);
        Assert.Equal(beforeTotal, afterBody.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task ManualProtectedDocumentRetrieval_ReturnsBytesOnlyForManualRecord()
    {
        using var response = await Client(DevJwt.SystemAdmin()).GetAsync(
            $"/api/identity-verification/documents/{factory.ManualRecordId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("%PDF-manual", Encoding.UTF8.GetString(await response.Content.ReadAsByteArrayAsync()));
        Assert.Equal("identity-document.pdf", response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
    }

    [Fact]
    public async Task ProtectedDocumentRetrieval_ForUnknownOrAutomatedRecord_Returns404Code()
    {
        using var response = await Client(DevJwt.SystemAdmin()).GetAsync(
            $"/api/identity-verification/documents/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await JsonAsync(response);
        Assert.Equal("ERR-IVF-404", problem.GetProperty("code").GetString());
        Assert.False(problem.TryGetProperty("detail", out var detail) && detail.GetString()?.Contains("PAN", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task MiddlewareValidationError_UsesIdentityCodeAndCorrelationId()
    {
        using var response = await PostComparisonAsync(
            "/api/identity-verification/verify-pan-name", " ");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await JsonAsync(response);
        Assert.Equal("ERR-IVF-400", problem.GetProperty("code").GetString());
        Assert.True(problem.TryGetProperty("correlationId", out var correlation));
        Assert.False(string.IsNullOrWhiteSpace(correlation.GetString()));
    }

    [Fact]
    public async Task BearerToken_IsAcceptedAndForwardableOnIdentityRoutes()
    {
        using var response = await PostComparisonAsync(
            "/api/identity-verification/verify-pan-name", "Anita Sharma");
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public void GatewayRouteExpectations_UseSystemAdminPrefixAndIdentityAlias()
    {
        const string systemAdminRoute = "/api/systemadmin/identity-verification/verify-pan-name";
        const string systemAdminWorkflowRoute = "/api/systemadmin/identity-verification/workflows/full";
        const string uiCompatibilityAlias = "/api/ua/api/identity-verification/verify-pan-name";

        Assert.StartsWith("/api/systemadmin/identity-verification/", systemAdminRoute);
        Assert.StartsWith("/api/systemadmin/identity-verification/", systemAdminWorkflowRoute);
        Assert.StartsWith("/api/ua/api/identity-verification/", uiCompatibilityAlias);
    }

    [Fact]
    public async Task IdentifierCompatibility_PreservesExistingNormalizedFieldsInWorkflowResponse()
    {
        using var response = await Client(DevJwt.SystemAdmin()).PostAsJsonAsync(
            "/api/identity-verification/workflows/full",
            new
            {
                workflowType = "Full",
                inputMode = "Partial",
                pan = "abcde1234f",
                panSubmittedName = "Anita Sharma",
                panSource = new { extractedValues = new { documentName = "Anita Sharma" }, mappingProfile = "test" },
                aadhaarNumber = "123456789012",
                aadhaarSubmittedAddress = "Mumbai, Maharashtra",
                aadhaarSource = new { extractedValues = new { documentAddress = "Mumbai, Maharashtra" }, mappingProfile = "test" }
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonAsync(response);
        var pan = body.GetProperty("panIdentifierResult");
        var aadhaar = body.GetProperty("aadhaarIdentifierResult");
        Assert.Equal("PanIdentifierValidation", pan.GetProperty("operationType").GetString());
        Assert.Equal("AadhaarIdentifierValidation", aadhaar.GetProperty("operationType").GetString());
        Assert.True(pan.TryGetProperty("validationStatus", out _));
        Assert.True(aadhaar.TryGetProperty("validationStatus", out _));
        Assert.True(pan.TryGetProperty("recordId", out _));
        Assert.True(aadhaar.TryGetProperty("recordId", out _));
        Assert.False(pan.TryGetProperty("comparisonOutcome", out _));
        Assert.False(aadhaar.TryGetProperty("comparisonOutcome", out _));
    }

    private async Task<HttpResponseMessage> PostComparisonAsync(string route, string submittedValue)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(submittedValue), "SubmittedValue");
        form.Add(new StringContent("{\"documentName\":\"Anita Sharma\",\"documentAddress\":\"Pune, Maharashtra\"}"), "ExtractedValuesJson");
        form.Add(new StringContent("test"), "MappingProfile");
        var client = Client(DevJwt.SystemAdmin());
        return await client.PostAsync(route, form);
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }
}

/// <summary>Deterministic SystemAdmin host registrations used only by task 10.3.</summary>
public sealed class IdentityVerificationAppFactory : SystemAdminAppFactory
{
    public Guid ManualRecordId { get; } = Guid.NewGuid();
    private readonly InMemoryIdentityVerificationRepository repository = new();
    private readonly InMemoryProtectedDocumentStore documentStore = new();

    public IdentityVerificationAppFactory()
    {
        var referenceId = Guid.NewGuid();
        var reference = new ProtectedDocumentReference
        {
            Id = referenceId,
            StorageKey = "identity-verification/manual-seeded.bin",
            ContentType = "application/pdf",
            SizeBytes = 11,
            ContentHash = "seeded",
            CreatedUtc = DateTime.UtcNow
        };
        documentStore.Seed(reference.StorageKey, Encoding.UTF8.GetBytes("%PDF-manual"));
        repository.Seed(new ValidationRecord
        {
            Id = ManualRecordId,
            OperationType = ModelOperationType.PanNameVerification,
            ComparisonOutcome = ComparisonOutcome.Match,
            Method = VerificationMethod.ManualOverride,
            ActorReference = "seeded-admin",
            CorrelationId = "seeded-correlation",
            ProtectedDocumentReferenceId = referenceId,
            ProtectedDocumentReference = reference,
            SourceSummary = new SourceSummary
            {
                SourceType = VerificationSourceType.DocumentFile,
                DocumentProvided = true,
                DocumentContentType = reference.ContentType,
                DocumentSizeBytes = reference.SizeBytes,
                ProtectedDocumentReferenceId = referenceId
            }
        });
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IIdentityVerificationRepository>();
            services.AddSingleton<IIdentityVerificationRepository>(repository);
            services.RemoveAll<IProtectedDocumentStore>();
            services.AddSingleton<IProtectedDocumentStore>(documentStore);
            services.RemoveAll<IComparisonAdapterResolver>();
            services.AddSingleton<IComparisonAdapterResolver, DeterministicComparisonResolver>();
            services.RemoveAll<IIdentifierValidationAdapter>();
            services.AddSingleton<IIdentifierValidationAdapter>(new DeterministicIdentifierAdapter(AdapterOperationType.PanIdentifierValidation));
            services.AddSingleton<IIdentifierValidationAdapter>(new DeterministicIdentifierAdapter(AdapterOperationType.AadhaarIdentifierValidation));
        });
    }
}

internal sealed class DeterministicIdentifierAdapter(AdapterOperationType operation) : IIdentifierValidationAdapter
{
    public AdapterOperationType SupportedOperation { get; } = operation;

    public Task<IdentifierAdapterResult> ValidateAsync(IdentifierValidationInput input, CancellationToken token)
        => Task.FromResult<IdentifierAdapterResult>(new IdentifierAdapterResult.Success(safeMessage: "Identifier validation successful."));
}

internal sealed class DeterministicComparisonResolver : IComparisonAdapterResolver
{
    private readonly IComparisonAdapter pan = new DeterministicComparisonAdapter(AdapterOperationType.PanNameVerification, match: true);
    private readonly IComparisonAdapter address = new DeterministicComparisonAdapter(AdapterOperationType.AadhaarAddressVerification, match: false);

    public IComparisonAdapter Resolve(AdapterOperationType operation, string configuration)
        => operation == AdapterOperationType.PanNameVerification ? pan : address;
}

internal sealed class DeterministicComparisonAdapter(AdapterOperationType operation, bool match) : IComparisonAdapter
{
    public AdapterOperationType SupportedOperation { get; } = operation;

    public Task<ComparisonAdapterResult> VerifyAsync(ProviderNeutralComparisonInput input, CancellationToken token)
        => Task.FromResult<ComparisonAdapterResult>(match
            ? new ComparisonAdapterResult.Match("Comparison completed.")
            : new ComparisonAdapterResult.NonMatch("Comparison completed."));
}

internal sealed class InMemoryProtectedDocumentStore : IProtectedDocumentStore
{
    private readonly Dictionary<string, byte[]> content = new(StringComparer.Ordinal);

    public void Seed(string key, byte[] bytes) => content[key] = bytes;

    public Task<ProtectedDocumentReference> PutAsync(Stream input, string contentType, long sizeBytes, CancellationToken ct = default)
    {
        using var copy = new MemoryStream();
        input.CopyTo(copy);
        var key = $"identity-verification/{Guid.NewGuid():N}.bin";
        content[key] = copy.ToArray();
        return Task.FromResult(new ProtectedDocumentReference
        {
            StorageKey = key,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            ContentHash = "test"
        });
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken ct = default)
        => Task.FromResult<Stream?>(content.TryGetValue(storageKey, out var bytes)
            ? new MemoryStream(bytes, writable: false)
            : null);

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        content.Remove(storageKey);
        return Task.CompletedTask;
    }
}

internal sealed class InMemoryIdentityVerificationRepository : IIdentityVerificationRepository
{
    private readonly List<ValidationRecord> records = [];
    private readonly List<VerificationSession> sessions = [];
    private readonly List<ProtectedDocumentReference> documents = [];
    private readonly object gate = new();

    public void Seed(ValidationRecord record)
    {
        lock (gate)
        {
            records.Add(record);
            if (record.ProtectedDocumentReference is not null)
                documents.Add(record.ProtectedDocumentReference);
        }
    }

    public Task AddSessionAsync(VerificationSession session, CancellationToken ct = default)
    {
        lock (gate) sessions.Add(session);
        return Task.CompletedTask;
    }

    public Task AppendRecordAsync(ValidationRecord record, CancellationToken ct = default)
    {
        lock (gate) records.Add(record);
        return Task.CompletedTask;
    }

    public Task AppendRecordsAsync(IReadOnlyCollection<ValidationRecord> values, CancellationToken ct = default)
    {
        lock (gate) records.AddRange(values);
        return Task.CompletedTask;
    }

    public Task<VerificationSession?> GetSessionAsync(Guid id, CancellationToken ct = default)
    {
        lock (gate) return Task.FromResult(sessions.SingleOrDefault(x => x.Id == id));
    }

    public Task<ValidationRecord?> GetRecordAsync(Guid id, CancellationToken ct = default)
    {
        lock (gate) return Task.FromResult(records.SingleOrDefault(x => x.Id == id));
    }

    public Task<ProtectedDocumentReference?> GetProtectedDocumentReferenceAsync(Guid id, CancellationToken ct = default)
    {
        lock (gate) return Task.FromResult(documents.SingleOrDefault(x => x.Id == id));
    }

    public Task<List<ValidationRecord>> GetBySessionAsync(Guid id, CancellationToken ct = default)
    {
        lock (gate) return Task.FromResult(records.Where(x => x.SessionId == id).OrderByDescending(x => x.CreatedUtc).ThenByDescending(x => x.Id).ToList());
    }

    public Task<(List<ValidationRecord> Items, int Total)> GetPagedAsync(VerificationRecordQuery query, CancellationToken ct = default)
    {
        lock (gate)
        {
            IEnumerable<ValidationRecord> filtered = records;
            if (query.OperationType is not null) filtered = filtered.Where(x => x.OperationType == query.OperationType);
            if (query.ValidationStatus is not null) filtered = filtered.Where(x => x.ValidationStatus == query.ValidationStatus);
            if (query.ComparisonOutcome is not null) filtered = filtered.Where(x => x.ComparisonOutcome == query.ComparisonOutcome);
            if (query.Method is not null) filtered = filtered.Where(x => x.Method == query.Method);
            if (query.SessionId is not null) filtered = filtered.Where(x => x.SessionId == query.SessionId);
            if (query.StartUtc is not null) filtered = filtered.Where(x => x.CreatedUtc >= query.StartUtc);
            if (query.EndUtc is not null) filtered = filtered.Where(x => x.CreatedUtc < query.EndUtc);
            var total = filtered.Count();
            var page = Math.Max(1, query.Page);
            var pageSize = query.PageSize == 0 ? 20 : query.PageSize;
            var items = filtered.OrderByDescending(x => x.CreatedUtc).ThenByDescending(x => x.Id)
                .Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return Task.FromResult((items, total));
        }
    }

    public Task<ValidationRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => GetRecordAsync(id, cancellationToken);

    public Task<List<ValidationRecord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        lock (gate) return Task.FromResult(records.ToList());
    }

    public Task<List<ValidationRecord>> FindAsync(Expression<Func<ValidationRecord, bool>> predicate, CancellationToken cancellationToken = default)
    {
        lock (gate) return Task.FromResult(records.AsQueryable().Where(predicate).ToList());
    }

    public Task AddAsync(ValidationRecord entity, CancellationToken cancellationToken = default)
        => AppendRecordAsync(entity, cancellationToken);

    public Task UpdateAsync(ValidationRecord entity, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Identity verification history is immutable.");

    public Task DeleteAsync(ValidationRecord entity, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Identity verification history is immutable.");

    public Task<int> CountAsync(Expression<Func<ValidationRecord, bool>>? predicate = null, CancellationToken cancellationToken = default)
    {
        lock (gate) return Task.FromResult(predicate is null ? records.Count : records.AsQueryable().Count(predicate));
    }
}
