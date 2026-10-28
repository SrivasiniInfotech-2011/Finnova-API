using Moq;
using Xunit;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Customers.References;
using Finnova.Models.Contracts.Customers;
public sealed class CustomerMasterReferenceValidatorTests
{
    [Fact] public async Task Inactive_lookup_is_rejected() { var repo = new Mock<ILookupRepository>(); repo.Setup(x => x.GetActiveByModuleAndTypeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<LookupValue>()); var resolver = new LookupReferenceResolver(repo.Object); await Assert.ThrowsAsync<CustomerLookupReferenceNotFoundException>(() => resolver.ResolveActiveAsync("M", "T", "X", "GroupCode")); }
    [Fact] public async Task Nationality_resolver_uses_active_by_code() { var repo = new Mock<INationalityRepository>(); repo.Setup(x => x.GetActiveByCodeAsync("IN", It.IsAny<CancellationToken>())).ReturnsAsync(new Nationality { Code = "IN", Name = "India", IsActive = true }); var result = await new NationalityReferenceResolver(repo.Object).ResolveActiveAsync("IN"); Assert.Equal("India", result.Name); }
    [Fact] public async Task Document_resolver_uses_active_applicable_definitions() { var repo = new Mock<IDocumentRepository>(); repo.Setup(x => x.GetActiveForConsumptionAsync("C", "R", It.IsAny<CancellationToken>())).ReturnsAsync(new List<Document> { new() { Id = Guid.NewGuid(), DocCode = "PAN", IsMandatory = true } }); var result = await new DocumentDefinitionResolver(repo.Object).GetApplicableAsync("C", "R"); Assert.Single(result); }
    [Fact] public async Task Address_fake_returns_approved_values_without_external_call() { var fake = new IndiaAddressControlTestFake(); var result = await fake.ValidateAndApproveAsync(new CustomerAddressRequest(CustomerAddressType.Communication, "1", null, "110001", "Delhi", "Delhi", "India", null, null, null, null, null, "a@example.com", "https://example.com", null, null, null, null, null)); Assert.True(result.IsValid); }
}
