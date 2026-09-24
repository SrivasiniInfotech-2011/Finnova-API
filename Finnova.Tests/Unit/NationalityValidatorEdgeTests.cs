using FluentValidation.TestHelper;
using Xunit;
using Finnova.Service.Nationality.Commands.CreateNationality;
using Finnova.Service.Nationality.Commands.UpdateNationalityName;
using Finnova.Service.Nationality.Queries.GetNationalitiesPaged;

namespace Finnova.Tests.Unit;

/// <summary>
/// Example/edge unit tests (task 10.2) for the nationality validators and the paged-query
/// record defaults. Uses FluentValidation's TestHelper, mirroring the existing Lookup edge tests.
///
/// Coverage:
///   - Code length 10 accepted / 11 rejected; Name 100 accepted / 101 rejected (R1.4, R2.5, R3.3).
///   - Blank Code/Name rejected (R1.3, R2.5, R3.2).
///   - Page defaults to 1 and PageSize to 20 (R5.5, R5.6).
///   - Page &lt; 1 rejected; PageSize &lt; 1 or &gt; 100 rejected (R5.7, R5.8).
/// </summary>
public class NationalityValidatorEdgeTests
{
    private static readonly CreateNationalityCommandValidator CreateValidator = new();
    private static readonly UpdateNationalityNameCommandValidator UpdateValidator = new();
    private static readonly GetNationalitiesPagedQueryValidator PagedValidator = new();

    private static CreateNationalityCommand ValidCreate() =>
        new(Code: "IN", Name: "Indian", IsActive: true, ActingAdmin: "admin");

    private static UpdateNationalityNameCommand ValidUpdate() =>
        new(Id: Guid.NewGuid(), Name: "Indian", ActingAdmin: "admin");

    // ---- Create: Code length boundary (R1.4, R2.5) ----

    [Fact]
    public void Create_Accepts_CodeAtMaxLength_10()
    {
        var cmd = ValidCreate() with { Code = new string('C', 10) };

        var result = CreateValidator.TestValidate(cmd);

        result.ShouldNotHaveValidationErrorFor(x => x.Code);
    }

    [Fact]
    public void Create_Rejects_CodeOverMaxLength_11()
    {
        var cmd = ValidCreate() with { Code = new string('C', 11) };

        var result = CreateValidator.TestValidate(cmd);

        result.ShouldHaveValidationErrorFor(x => x.Code);
    }

    // ---- Create: Name length boundary (R1.4) ----

    [Fact]
    public void Create_Accepts_NameAtMaxLength_100()
    {
        var cmd = ValidCreate() with { Name = new string('N', 100) };

        var result = CreateValidator.TestValidate(cmd);

        result.ShouldNotHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Create_Rejects_NameOverMaxLength_101()
    {
        var cmd = ValidCreate() with { Name = new string('N', 101) };

        var result = CreateValidator.TestValidate(cmd);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    // ---- Create: blank Code / Name rejected (R1.3, R2.5) ----

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Rejects_BlankCode(string code)
    {
        var cmd = ValidCreate() with { Code = code };

        var result = CreateValidator.TestValidate(cmd);

        result.ShouldHaveValidationErrorFor(x => x.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Rejects_BlankName(string name)
    {
        var cmd = ValidCreate() with { Name = name };

        var result = CreateValidator.TestValidate(cmd);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Create_Accepts_ValidCommand()
    {
        var result = CreateValidator.TestValidate(ValidCreate());

        result.ShouldNotHaveAnyValidationErrors();
    }

    // ---- Update: Name length boundary (R3.3) ----

    [Fact]
    public void Update_Accepts_NameAtMaxLength_100()
    {
        var cmd = ValidUpdate() with { Name = new string('N', 100) };

        var result = UpdateValidator.TestValidate(cmd);

        result.ShouldNotHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Update_Rejects_NameOverMaxLength_101()
    {
        var cmd = ValidUpdate() with { Name = new string('N', 101) };

        var result = UpdateValidator.TestValidate(cmd);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    // ---- Update: blank Name rejected (R3.2) ----

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Update_Rejects_BlankName(string name)
    {
        var cmd = ValidUpdate() with { Name = name };

        var result = UpdateValidator.TestValidate(cmd);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Update_Rejects_EmptyId()
    {
        var cmd = ValidUpdate() with { Id = Guid.Empty };

        var result = UpdateValidator.TestValidate(cmd);

        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    // ---- Query defaults (R5.5, R5.6) ----

    [Fact]
    public void PagedQuery_Defaults_Page_To_1_And_PageSize_To_20()
    {
        var query = new GetNationalitiesPagedQuery(SearchTerm: null);

        Assert.Equal(1, query.Page);
        Assert.Equal(20, query.PageSize);
    }

    [Fact]
    public void PagedQuery_Default_Values_Pass_Validation()
    {
        var query = new GetNationalitiesPagedQuery(SearchTerm: null);

        var result = PagedValidator.TestValidate(query);

        result.ShouldNotHaveAnyValidationErrors();
    }

    // ---- Query paging bounds (R5.7, R5.8) ----

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void PagedQuery_Rejects_PageBelow1(int page)
    {
        var query = new GetNationalitiesPagedQuery(SearchTerm: null, Page: page, PageSize: 20);

        var result = PagedValidator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(x => x.Page);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(1000)]
    public void PagedQuery_Rejects_PageSizeOutOfRange(int pageSize)
    {
        var query = new GetNationalitiesPagedQuery(SearchTerm: null, Page: 1, PageSize: pageSize);

        var result = PagedValidator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void PagedQuery_Accepts_PageSizeBoundaries(int pageSize)
    {
        var query = new GetNationalitiesPagedQuery(SearchTerm: null, Page: 1, PageSize: pageSize);

        var result = PagedValidator.TestValidate(query);

        result.ShouldNotHaveValidationErrorFor(x => x.PageSize);
    }
}
