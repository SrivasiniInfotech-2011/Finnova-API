using Finnova.Models.Contracts.GL;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.GL.Commands.AuthorizeGLAccount;
using Finnova.Service.GL.Commands.CreateGLAccount;
using Finnova.Service.GL.Ports;
using Moq;
using Xunit;

namespace Finnova.Tests.GL;

public class GLHandlerFactsTests
{
    private static CreateGLAccountRequest NewReq(Guid company, string code = "100000000") =>
        new(company, code, "Cash", null, GLType.Normal, null, null, Guid.NewGuid(),
            true, false, false, null, DateTime.UtcNow, null, new List<Guid>());

    // PROP-RETIRED-PRECEDENCE (R2.4): retired code wins over plain duplicate.
    [Fact]
    public async Task Create_retired_code_takes_precedence()
    {
        var company = Guid.NewGuid();
        var repo = new Mock<IGLAccountRepository>();
        var audit = new Mock<IGLAuditRepository>();
        var cal = new Mock<IAccountingCalendarPort>();
        cal.Setup(c => c.IsClosedPeriodAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), default)).ReturnsAsync(false);
        repo.Setup(r => r.GetInactiveByCodeAsync(company, "100000000", default))
            .ReturnsAsync(new GLAccount { GLCode = "100000000", Status = GLStatus.Inactive });

        var handler = new CreateGLAccountCommandHandler(repo.Object, audit.Object, cal.Object);
        await Assert.ThrowsAsync<GLRetiredCodeException>(() =>
            handler.Handle(new CreateGLAccountCommand(NewReq(company), "maker-1"), default));
        audit.Verify(a => a.AddAsync(It.IsAny<GLAuditEntry>(), default), Times.Never);   // R10.2
    }

    // PROP-AUDIT-EXACTLY-ONCE (R10.1): success writes exactly one audit entry.
    [Fact]
    public async Task Create_success_writes_one_audit_entry()
    {
        var company = Guid.NewGuid();
        var repo = new Mock<IGLAccountRepository>();
        var audit = new Mock<IGLAuditRepository>();
        var cal = new Mock<IAccountingCalendarPort>();
        cal.Setup(c => c.IsClosedPeriodAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), default)).ReturnsAsync(false);
        repo.Setup(r => r.GetInactiveByCodeAsync(company, It.IsAny<string>(), default)).ReturnsAsync((GLAccount?)null);
        repo.Setup(r => r.ExistsByCodeAsync(company, It.IsAny<string>(), null, default)).ReturnsAsync(false);
        repo.Setup(r => r.ExistsByNameAsync(company, It.IsAny<string>(), null, default)).ReturnsAsync(false);

        var handler = new CreateGLAccountCommandHandler(repo.Object, audit.Object, cal.Object);
        var result = await handler.Handle(new CreateGLAccountCommand(NewReq(company), "maker-1"), default);

        Assert.Equal(GLStatus.Draft, result.Status);
        audit.Verify(a => a.AddAsync(It.IsAny<GLAuditEntry>(), default), Times.Once);   // R10.1
    }

    // PROP-MAKER-NE-CHECKER (R3.3): self-authorization is rejected, state unchanged.
    [Fact]
    public async Task Authorize_self_is_rejected()
    {
        var repo = new Mock<IGLAccountRepository>();
        var audit = new Mock<IGLAuditRepository>();
        var pending = new GLAccount { Status = GLStatus.PendingAuthorization, MakerId = "maker-1" };
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync(pending);

        var handler = new AuthorizeGLAccountCommandHandler(repo.Object, audit.Object);
        await Assert.ThrowsAsync<GLConflictException>(() =>
            handler.Handle(new AuthorizeGLAccountCommand(pending.Id, "maker-1"), default));
        Assert.Equal(GLStatus.PendingAuthorization, pending.Status);
        audit.Verify(a => a.AddAsync(It.IsAny<GLAuditEntry>(), default), Times.Never);
    }

    // R3.4: a different checker authorizes a pending account to Active.
    [Fact]
    public async Task Authorize_by_other_user_sets_active()
    {
        var repo = new Mock<IGLAccountRepository>();
        var audit = new Mock<IGLAuditRepository>();
        var pending = new GLAccount { Status = GLStatus.PendingAuthorization, MakerId = "maker-1" };
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync(pending);

        var handler = new AuthorizeGLAccountCommandHandler(repo.Object, audit.Object);
        var result = await handler.Handle(new AuthorizeGLAccountCommand(pending.Id, "checker-2"), default);

        Assert.Equal(GLStatus.Active, result.Status);
        audit.Verify(a => a.AddAsync(It.IsAny<GLAuditEntry>(), default), Times.Once);
    }
}
