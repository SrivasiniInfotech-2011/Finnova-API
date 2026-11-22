using Finnova.Models.Contracts.ReceiptAppropriation;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Service.ReceiptAppropriation;
using Finnova.Service.ReceiptAppropriation.Ports;
using Microsoft.AspNetCore.Mvc.Testing;
using Moq;
using System.Net;
using System.Security.Claims;
using Xunit;

namespace Finnova.Tests.ReceiptAppropriation;

public sealed class ReceiptAppropriationHandlerTests
{
    [Fact]
    public async Task ConfirmRejectsTotalAboveReceiptWithoutWriting()
    {
        var receipt = new ReceiptContext(Guid.NewGuid(), Guid.NewGuid(), "CUST-1", Guid.NewGuid(), Guid.NewGuid(), 35000m, "INR", "PendingAuthorization", "1"); var receiptPort = new Mock<IReceiptContextPort>(); receiptPort.Setup(x => x.GetAsync(receipt.Id, It.IsAny<CancellationToken>())).ReturnsAsync(receipt); receiptPort.Setup(x => x.CanAppropriateAsync(receipt, It.IsAny<CancellationToken>())).ReturnsAsync(true); var due = new PendingDueProjection(Guid.NewGuid(), Guid.NewGuid(), "ACC-1", receipt.CustomerId, CashFlowDueType.Installment, new DateOnly(2026, 1, 1), 40000m, "Collectible", "1"); var duePort = new Mock<IPendingDuePort>(); duePort.Setup(x => x.GetEligibleAsync(receipt, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { due }); var scope = new Mock<IAppropriationScopeResolver>(); scope.Setup(x => x.ResolveAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>())).ReturnsAsync(new AuthorizationScope(new[] { receipt.CompanyId }.ToHashSet(), new[] { receipt.LineOfBusinessId }.ToHashSet(), new HashSet<Guid>(), true)); scope.Setup(x => x.CanAccess(It.IsAny<AuthorizationScope>(), receipt)).Returns(true); var request = new ConfirmAppropriationRequest(Guid.NewGuid(), AppropriationMode.Manual, new[] { new ConfirmAllocationRequest(due.Id, 35000.01m) }, false); var handler = new ConfirmAppropriationCommandHandler(receiptPort.Object, duePort.Object, Mock.Of<IAppropriationRulePort>(), Mock.Of<IAdvanceAccountPort>(), scope.Object, Mock.Of<Finnova.Repository.Interfaces.IReceiptAppropriationRepository>(), Mock.Of<Finnova.Repository.Interfaces.IApplicationUnitOfWork>()); await Assert.ThrowsAsync<ReceiptAppropriationReconciliationException>(() => handler.Handle(new ConfirmAppropriationCommand(receipt.Id, request, new ClaimsPrincipal(new ClaimsIdentity()), "tester", "corr"), CancellationToken.None)); duePort.Verify(x => x.ReserveAsync(It.IsAny<IReadOnlyList<DueReservation>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UnauthenticatedAppropriationRequestReturns401()
    {
        await using var factory = new WebApplicationFactory<Finnova.AccountsService.Controllers.ReceiptAppropriationController>();
        using var client = factory.CreateClient();
        var response = await client.GetAsync($"/api/receipt-appropriation/{Guid.NewGuid()}/dues");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}