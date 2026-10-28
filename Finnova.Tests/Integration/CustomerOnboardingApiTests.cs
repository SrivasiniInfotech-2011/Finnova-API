using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Finnova.Models.Contracts.Customers;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Context;
using Finnova.Service.Customers.References;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Finnova.Tests.Integration;

/// <summary>
/// Task 9.3 — WebApplicationFactory coverage for Customer Onboarding.
///
/// The fixture reuses the existing SystemAdmin host, JWT helper, and EF Core InMemory database.
/// It replaces only the deferred India Address Control adapter with the repository's existing
/// test fake. Master rows are seeded through the same in-memory FinnovaDbContext used by the host,
/// so the requests exercise the real HTTP, authorization, resolver, handler, repository, audit,
/// and projection boundaries rather than unit-test doubles for the Customer slice.
/// </summary>
public sealed class CustomerOnboardingApiTests : IClassFixture<CustomerOnboardingAppFactory>
{
    private readonly CustomerOnboardingAppFactory _factory;
    private static int _masterSequence = 1000;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public CustomerOnboardingApiTests(CustomerOnboardingAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Unauthorized_create_is_rejected()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/customer-onboarding", new { });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_lookup_returns_customer_validation_problem()
    {
        using var client = AdminClient();
        var masters = SeedMasters();
        var request = ValidCreate(masters) with { GroupCode = "0000" };

        var response = await client.PostAsJsonAsync("/api/customer-onboarding", request);

        await AssertCustomerProblemAsync(response, HttpStatusCode.BadRequest, "ERR-CUSTOMER-LOOKUP-REF");
    }

    [Fact]
    public async Task Create_modify_query_flow_uses_generated_immutable_code()
    {
        using var client = AdminClient();
        var masters = SeedMasters();

        var createResponse = await client.PostAsJsonAsync("/api/customer-onboarding", ValidCreate(masters));
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<CustomerResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal(12, created!.CustomerCode.Length);
        Assert.True(created.CustomerCode.All(char.IsLetterOrDigit));
        Assert.Equal(masters.GroupCode, created.Group?.Code);
        Assert.Equal("India", created.NationalityName);
        Assert.Equal("Customer Posting Group", created.GLName);
        Assert.Single(created.Addresses);

        var modifyResponse = await client.PutAsJsonAsync(
            $"/api/customer-onboarding/{Uri.EscapeDataString(created.CustomerCode)}",
            ValidModify(masters, created.CustomerCode) with { FirstName = "Modified" });
        Assert.Equal(HttpStatusCode.OK, modifyResponse.StatusCode);
        var modified = await modifyResponse.Content.ReadFromJsonAsync<CustomerResponse>(JsonOptions);
        Assert.NotNull(modified);
        Assert.Equal(created.CustomerCode, modified!.CustomerCode);
        Assert.Equal("Modified", modified.FirstName);

        var queryResponse = await client.GetAsync(
            $"/api/customer-onboarding?customerCode={Uri.EscapeDataString(created.CustomerCode)}");
        Assert.Equal(HttpStatusCode.OK, queryResponse.StatusCode);
        var queried = await queryResponse.Content.ReadFromJsonAsync<CustomerResponse[]>(JsonOptions);
        Assert.NotNull(queried);
        var projection = Assert.Single(queried!);
        Assert.Equal(created.CustomerCode, projection.CustomerCode);
        Assert.Equal("Modified", projection.FirstName);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FinnovaDbContext>();
        Assert.Equal(1, db.Customers.Count(x => x.CustomerCode == created.CustomerCode));
        Assert.Equal(2, db.CustomerAuditEntries.Count(x => x.CustomerId == created.Id));
    }

    [Fact]
    public async Task Failed_child_validation_rolls_back_customer_and_audit()
    {
        using var client = AdminClient();
        var masters = SeedMasters();
        var request = ValidCreate(masters) with
        {
            BankAccounts = new[]
            {
                new BankAccountRequest(null, "1234567890", "BANK", null, null, null, null, null, true),
                new BankAccountRequest(null, "0987654321", "BANK2", null, null, null, null, null, true),
            },
        };

        var before = Counts(masters.CompanyId);
        var response = await client.PostAsJsonAsync("/api/customer-onboarding", request);

        await AssertCustomerProblemAsync(response, HttpStatusCode.Conflict, "ERR-CUSTOMER-DEFAULT-BANK");
        var after = Counts(masters.CompanyId);
        Assert.Equal(before.Customers, after.Customers);
        Assert.Equal(before.Audits, after.Audits);
        Assert.DoesNotContain("1234567890", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Projection_resolves_current_names_without_persisting_them()
    {
        using var client = AdminClient();
        var masters = SeedMasters();

        var createResponse = await client.PostAsJsonAsync("/api/customer-onboarding", ValidCreate(masters));
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<CustomerResponse>(JsonOptions);
        Assert.NotNull(created);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FinnovaDbContext>();
            var lookup = db.LookupValues.Single(x =>
                x.Module == CustomerLookupTypes.Module &&
                x.LookupType == CustomerLookupTypes.Group &&
                x.Code == masters.GroupCode);
            lookup.Value = "Renamed Group";
            db.SaveChanges();
        }

        var queryResponse = await client.GetAsync(
            $"/api/customer-onboarding?customerCode={Uri.EscapeDataString(created!.CustomerCode)}");
        Assert.Equal(HttpStatusCode.OK, queryResponse.StatusCode);
        var queried = await queryResponse.Content.ReadFromJsonAsync<CustomerResponse[]>(JsonOptions);
        var projection = Assert.Single(queried!);
        Assert.Equal(masters.GroupCode, projection.Group?.Code);
        Assert.Equal("Renamed Group", projection.GroupName);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<FinnovaDbContext>();
        var stored = Assert.Single(verifyDb.Customers.Where(x => x.CustomerCode == created.CustomerCode));
        Assert.Equal(masters.GroupCode, stored.GroupCode);
        Assert.DoesNotContain(
            "GroupName",
            typeof(Customer).GetProperties().Select(x => x.Name),
            StringComparer.Ordinal);
    }

    [Fact]
    public async Task Inactive_or_unknown_master_references_are_rejected_without_writes()
    {
        using var client = AdminClient();
        var masters = SeedMasters();
        var inactiveNationality = $"IX{masters.GroupCode}";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FinnovaDbContext>();
            db.Nationalities.Add(new Nationality
            {
                Code = inactiveNationality,
                Name = "Inactive India Reference",
                IsActive = false,
            });
            db.SaveChanges();
        }

        var cases = new[]
        {
            ("inactive nationality", ValidCreate(masters) with { NationalityCode = inactiveNationality }, "ERR-CUSTOMER-NATIONALITY-REF"),
            ("unknown GL", ValidCreate(masters) with { CustomerPostingGroupCode = "499999999", GLAccountId = null }, "ERR-CUSTOMER-GL-REF"),
            ("unknown lookup", ValidCreate(masters) with { IndustryCode = "ZZZ" }, "ERR-CUSTOMER-LOOKUP-REF"),
        };

        foreach (var (_, request, code) in cases)
        {
            var before = Counts(masters.CompanyId);
            var response = await client.PostAsJsonAsync("/api/customer-onboarding", request);
            await AssertCustomerProblemAsync(response, HttpStatusCode.BadRequest, code);
            var after = Counts(masters.CompanyId);
            Assert.Equal(before.Customers, after.Customers);
            Assert.Equal(before.Audits, after.Audits);
        }
    }

    [Fact]
    public async Task Missing_applicable_document_is_rejected_atomically()
    {
        using var client = AdminClient();
        var masters = SeedMasters();
        SeedMandatoryDocument(masters);
        var before = Counts(masters.CompanyId);

        var response = await client.PostAsJsonAsync(
            "/api/customer-onboarding",
            ValidCreate(masters) with { Documents = Array.Empty<CustomerDocumentReferenceRequest>() });

        await AssertCustomerProblemAsync(response, HttpStatusCode.BadRequest, "ERR-CUSTOMER-DOCUMENT-REF");
        var after = Counts(masters.CompanyId);
        Assert.Equal(before.Customers, after.Customers);
        Assert.Equal(before.Audits, after.Audits);
    }

    [Fact]
    public async Task Unknown_customer_modify_returns_safe_not_found_problem()
    {
        using var client = AdminClient();
        var masters = SeedMasters();

        var response = await client.PutAsJsonAsync(
            "/api/customer-onboarding/ZZZZZZZZZZZZ",
            ValidModify(masters, "ZZZZZZZZZZZZ"));

        await AssertCustomerProblemAsync(response, HttpStatusCode.NotFound, "ERR-CUSTOMER-404");
        Assert.DoesNotContain("ZZZZZZZZZZZZ", await response.Content.ReadAsStringAsync());
    }

    private HttpClient AdminClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", DevJwt.SystemAdmin());
        return client;
    }

    private MasterData SeedMasters()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FinnovaDbContext>();
        var number = Interlocked.Increment(ref _masterSequence) % 9000 + 1000;
        var data = new MasterData(
            Guid.NewGuid(),
            number.ToString("D4"),
            $"I{number % 100:D2}",
            $"C{number:D4}",
            $"IN{number:D4}",
            $"40000{number:D4}",
            Guid.NewGuid());

        EnsureLookup(db, CustomerLookupTypes.Group, data.GroupCode, "Group One");
        EnsureLookup(db, CustomerLookupTypes.Industry, data.IndustryCode, "Industry One");
        EnsureLookup(db, CustomerLookupTypes.Constitution, data.ConstitutionCode, "Individual");
        EnsureLookup(db, CustomerLookupTypes.Title, "MR", "Mr");
        EnsureLookup(db, CustomerLookupTypes.MaritalStatus, "SIN", "Single");

        if (!db.Nationalities.Any(x => x.Code == data.NationalityCode))
        {
            db.Nationalities.Add(new Nationality
            {
                Code = data.NationalityCode,
                Name = "India",
                IsActive = true,
            });
        }

        if (!db.GLAccounts.Any(x => x.Id == data.GLAccountId))
        {
            db.GLAccounts.Add(new GLAccount
            {
                Id = data.GLAccountId,
                CompanyId = data.CompanyId,
                GLCode = data.GLCode,
                Name = "Customer Posting Group",
                Level = GLLevel.L4,
                AccountNature = AccountNature.Asset,
                NormalBalance = NormalBalance.Debit,
                GLType = GLType.Normal,
                IsPostable = true,
                CurrencyId = Guid.NewGuid(),
                EffectiveFrom = DateTime.UtcNow.Date,
                Status = GLStatus.Active,
                AuthorizedVersionNumber = 1,
            });
        }

        db.SaveChanges();
        return data;
    }

    private static void EnsureLookup(FinnovaDbContext db, string lookupType, string code, string value)
    {
        if (db.LookupValues.Any(x =>
                x.Module == CustomerLookupTypes.Module &&
                x.LookupType == lookupType &&
                x.Code == code))
            return;

        db.LookupValues.Add(new LookupValue
        {
            Module = CustomerLookupTypes.Module,
            LookupType = lookupType,
            Code = code,
            Value = value,
            DisplayOrder = 1,
            IsActive = true,
        });
    }

    private void SeedMandatoryDocument(MasterData data)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FinnovaDbContext>();
        var documentId = Guid.NewGuid();
        db.Documents.Add(new Models.Domain.Entities.Document
        {
            Id = documentId,
            DocCode = $"PAN-{data.GroupCode}",
            DocName = "PAN",
            DocCategory = "IDENTITY",
            ApplicableEntity = CustomerLookupTypes.EntityRole,
            IsMandatory = true,
            AllowedMimeTypes = "PDF",
            Status = DocumentStatus.Active,
            AuthorizedVersionNumber = 1,
            ApplicabilityRules = new List<DocumentApplicabilityRule>
            {
                new()
                {
                    Constitution = data.ConstitutionCode,
                    EntityRole = CustomerLookupTypes.EntityRole,
                    IsMandatory = true,
                },
            },
        });
        db.SaveChanges();
    }

    private (int Customers, int Audits) Counts(Guid companyId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FinnovaDbContext>();
        return (
            db.Customers.Count(x => x.CompanyId == companyId),
            db.CustomerAuditEntries.Count(x => db.Customers.Any(c => c.CompanyId == companyId && c.Id == x.CustomerId)));
    }

    private static CustomerCreateRequest ValidCreate(MasterData data) => new(
        CompanyId: data.CompanyId,
        IsCustomer: true,
        IsGuarantor: false,
        IsCoApplicant: false,
        CustomerTypeCode: "INDIVIDUAL",
        GroupCode: data.GroupCode,
        IndustryCode: data.IndustryCode,
        CustomerPostingGroupCode: data.GLCode,
        GLAccountId: data.GLAccountId,
        ConstitutionCode: data.ConstitutionCode,
        NationalityCode: data.NationalityCode,
        TitleCode: "MR",
        MaritalStatusCode: "SIN",
        CustomerBusinessTypeCode: null,
        CustomerBusinessSubTypeCode: null,
        IndustrySectorCode: null,
        SubSectorCode: null,
        FirstName: $"Integration-{data.GroupCode}",
        SecondName: null,
        ThirdName: null,
        FourthName: null,
        FifthName: null,
        SixthName: null,
        Notes: "Integration test customer",
        IsVip: false,
        IsSeniorMember: false,
        FinancialRequirement: null,
        Accommodation: null,
        IsRelatedParty: false,
        RelationCode: null,
        EmployerEconomicActivityCode: null,
        Addresses: new[] { CommunicationAddress() },
        IndividualDetails: null,
        NonIndividualDetails: null,
        BankAccounts: Array.Empty<BankAccountRequest>(),
        Documents: Array.Empty<CustomerDocumentReferenceRequest>(),
        Tracks: Array.Empty<CustomerTrackRequest>(),
        FactoringSubLimits: Array.Empty<FactoringSubLimitRequest>());

    private static CustomerModifyRequest ValidModify(MasterData data, string customerCode) => new(
        CustomerCode: customerCode,
        CompanyId: data.CompanyId,
        CustomerTypeCode: "INDIVIDUAL",
        GroupCode: data.GroupCode,
        IndustryCode: data.IndustryCode,
        CustomerPostingGroupCode: data.GLCode,
        GLAccountId: data.GLAccountId,
        ConstitutionCode: data.ConstitutionCode,
        NationalityCode: data.NationalityCode,
        TitleCode: "MR",
        MaritalStatusCode: "SIN",
        CustomerBusinessTypeCode: null,
        CustomerBusinessSubTypeCode: null,
        IndustrySectorCode: null,
        SubSectorCode: null,
        FirstName: $"Integration-{data.GroupCode}",
        SecondName: null,
        ThirdName: null,
        FourthName: null,
        FifthName: null,
        SixthName: null,
        Notes: "Integration test customer",
        IsVip: false,
        IsSeniorMember: false,
        FinancialRequirement: null,
        Accommodation: null,
        IsRelatedParty: false,
        RelationCode: null,
        EmployerEconomicActivityCode: null,
        Addresses: new[] { CommunicationAddress() },
        IndividualDetails: null,
        NonIndividualDetails: null,
        BankAccounts: Array.Empty<BankAccountRequest>(),
        Documents: Array.Empty<CustomerDocumentReferenceRequest>(),
        Tracks: Array.Empty<CustomerTrackRequest>(),
        FactoringSubLimits: Array.Empty<FactoringSubLimitRequest>(),
        CopyCommunicationAddressToPermanent: false);

    private static CustomerAddressRequest CommunicationAddress() => new(
        AddressType: CustomerAddressType.Communication,
        HouseNumber: "1",
        BlockNumber: null,
        PostalCode: "110001",
        City: "Delhi",
        State: "Delhi",
        Country: "India",
        POBoxNumber: null,
        WayNumber: null,
        Telephone: null,
        ResidenceFaxNumber: null,
        Mobile: "9876543210",
        Email: "customer@example.com",
        Website: "https://example.com",
        ContactPerson: null,
        ContactPersonPhoneNumber: null,
        ContactPersonFlatNumber: null,
        AreaSheik: null,
        LocationLandmark: null);

    private static async Task AssertCustomerProblemAsync(
        HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.NotNull(problem);
        Assert.Equal(code, problem!.Code);
        Assert.Equal(code, problem.Title);
    }

    private sealed record MasterData(
        Guid CompanyId,
        string GroupCode,
        string IndustryCode,
        string ConstitutionCode,
        string NationalityCode,
        string GLCode,
        Guid GLAccountId);

    private sealed record ProblemDetailsDto(int? Status, string? Title, string? Detail, string? Code);
}

/// <summary>
/// Reuses the repository's SystemAdmin WebApplicationFactory and replaces only the external
/// address boundary with its existing deterministic test fake. The Customer repository and
/// application-adapter registrations remain the production registrations under test.
/// </summary>
public sealed class CustomerOnboardingAppFactory : SystemAdminAppFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IIndiaAddressControl>();
            services.AddScoped<IIndiaAddressControl, IndiaAddressControlTestFake>();
        });
    }
}
