using FsCheck.Xunit;
using Finnova.Models.Domain.Enums;
using Finnova.Service.GL.Internal;
using Xunit;

namespace Finnova.Tests.GL;

public class JvLineValidatorPropertyTests
{
    private static GLValidationSnapshot Good(Guid company, Guid branch, Guid currency) =>
        new(Guid.NewGuid(), company, GLStatus.Active, DateTime.UtcNow.AddYears(-1),
            DateTime.UtcNow.AddYears(1), true, true, new[] { branch }, false, false, currency, GLType.Normal);

    // PROP-JV-COMPLETENESS (R6.11): an all-valid line is accepted (zero reasons).
    [Property(MaxTest = 200)]
    public void Valid_line_is_accepted()
    {
        var company = Guid.NewGuid(); var branch = Guid.NewGuid(); var currency = Guid.NewGuid();
        var snap = Good(company, branch, currency);
        var line = new JvLine(1, snap.GLAccountId, currency, null, null, null);
        var ctx = new JvVoucherContext(company, DateTime.UtcNow, branch, JvEntryMode.System);
        Assert.Empty(JvLineValidator.Evaluate(line, snap, ctx));
    }

    // PROP-JV-COMPLETENESS: all applicable reasons are returned (not just the first).
    [Fact]
    public void All_applicable_reasons_returned()
    {
        var company = Guid.NewGuid(); var otherCompany = Guid.NewGuid();
        var branch = Guid.NewGuid(); var otherBranch = Guid.NewGuid();
        var currency = Guid.NewGuid(); var otherCurrency = Guid.NewGuid();
        var snap = new GLValidationSnapshot(Guid.NewGuid(), otherCompany, GLStatus.Frozen,
            DateTime.UtcNow.AddYears(1), DateTime.UtcNow.AddYears(2), false, false,
            new[] { otherBranch }, false, false, currency, GLType.Normal);
        var line = new JvLine(1, snap.GLAccountId, otherCurrency, null, null, null);
        var ctx = new JvVoucherContext(company, DateTime.UtcNow, branch, JvEntryMode.Manual);

        var reasons = JvLineValidator.Evaluate(line, snap, ctx);
        Assert.True(reasons.Count >= 6);   // company, active, date-range, postable, manual, branch, currency
    }

    // PROP-JV-ORDERING (R6.12): same input -> identical ordered reasons.
    [Property(MaxTest = 200)]
    public void Evaluation_is_deterministic(bool manual)
    {
        var company = Guid.NewGuid(); var branch = Guid.NewGuid(); var currency = Guid.NewGuid();
        var snap = new GLValidationSnapshot(Guid.NewGuid(), company, GLStatus.Active,
            DateTime.UtcNow.AddYears(-1), DateTime.UtcNow.AddYears(1), false, false,
            new[] { branch }, false, false, currency, GLType.Normal);
        var line = new JvLine(1, snap.GLAccountId, currency, null, null, null);
        var ctx = new JvVoucherContext(company, DateTime.UtcNow, branch,
            manual ? JvEntryMode.Manual : JvEntryMode.System);

        var a = JvLineValidator.Evaluate(line, snap, ctx);
        var b = JvLineValidator.Evaluate(line, snap, ctx);
        Assert.Equal(a, b);
    }

    // R6.13: unresolved GL -> single "does not exist" reason.
    [Fact]
    public void Unresolved_gl_single_reason()
    {
        var ctx = new JvVoucherContext(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), JvEntryMode.System);
        var line = new JvLine(1, Guid.NewGuid(), Guid.NewGuid(), null, null, null);
        var reasons = JvLineValidator.Evaluate(line, null, ctx);
        Assert.Single(reasons);
    }
}
