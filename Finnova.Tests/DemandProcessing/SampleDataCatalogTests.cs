using Finnova.Data.Seeder.DemandSample;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace Finnova.Tests.DemandProcessing;

public sealed class SampleDataCatalogTests
{
    [Fact] public void CatalogValidationAcceptsTheCompleteFixture() => SampleDataCatalog.Validate();
    [Fact] public void CatalogHasSixPeriodsEightBranchesAndSevenLoans() { Assert.Equal(6, SampleDataCatalog.Periods.Count); Assert.Equal(8, SampleDataCatalog.Branches.Count); Assert.Equal(7, SampleDataCatalog.Loans.Count); }
    [Fact] public void EveryBranchHasExplicitCityAndZone() => Assert.All(SampleDataCatalog.Branches, branch => { var city = Assert.Single(SampleDataCatalog.Locations.Where(x => x.Code == branch.CityCode)); var zone = Assert.Single(SampleDataCatalog.Locations.Where(x => x.Code == branch.ZoneCode)); Assert.Equal(3, city.Level); Assert.Equal(4, zone.Level); Assert.Equal(city.Code, zone.ParentCode); });
    [Fact] public void EmptyBranchAndCustomerAreIntentional() { Assert.Contains(SampleDataCatalog.Branches, x => x.Code == "MDU-B02"); Assert.Contains(SampleDataCatalog.Customers, x => x.Code == "CUSTDP000008"); Assert.DoesNotContain(SampleDataCatalog.Loans, x => x.BranchCode == "MDU-B02"); Assert.DoesNotContain(SampleDataCatalog.Loans, x => x.CustomerKey == "C008"); }
    [Fact] public void InvalidReceiptCasesArePresent() { Assert.Contains(SampleDataCatalog.Receipts, x => x.Dataset == "OTHER-DATASET"); Assert.Contains(SampleDataCatalog.Receipts, x => x.Description.Split('|').Length != 11); Assert.Contains(SampleDataCatalog.Receipts, x => x.PersistedAmount.HasValue); Assert.Contains(SampleDataCatalog.Receipts, x => x.Amount < 0m); }
    [Property(MaxTest = 100)] public Property StableIdsAreRepeatable(NonEmptyString value) => (SampleDataCatalog.StableId("test", value.Get) == SampleDataCatalog.StableId("test", value.Get)).ToProperty();
    [Property(MaxTest = 100)] public Property CutoffIsAfterLocalStart(PositiveInt day) { var date = new DateOnly(2026, 3, Math.Min(day.Get, 28)); return (SampleDataCatalog.CutoffUtc(date) > date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)).ToProperty(); }
}