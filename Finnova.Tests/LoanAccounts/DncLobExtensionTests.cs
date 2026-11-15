using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Numbering;
using Finnova.Service.DocumentNumberControl.Commands.IssueNumber;
using Finnova.Tests.Infrastructure;
using FsCheck.Xunit;
using Xunit;

namespace Finnova.Tests.LoanAccounts;

public sealed class DncLobExtensionTests
{
    [Property(MaxTest = 100)] // Feature: application-account-creation, Property 16: DNC token and scope consistency
    public bool TokensRequireMatchingScope(bool useLob, bool useBranch, bool hasLob, bool isBranch)
    {
        var template = "A" + (useLob ? "{LOB}" : "") + (useBranch ? "{BRANCH}" : "") + "{SEQ:5}";
        var expectedErrors = (useLob && !hasLob ? 1 : 0) + (useBranch && !isBranch ? 1 : 0);
        return NumberFormatter.Validate(template).Count == 0
            && NumberFormatter.ValidateScopeTokens(template, hasLob, isBranch).Count == expectedErrors;
    }

    private static NumberingScheme Make(string code, NumberScope scope, Guid? scopeId, Guid? lob) => new()
    {
        Code = code,
        Name = code,
        DocumentType = "LoanAccount",
        FormatTemplate = "{SEQ:6}",
        SeqPadding = 6,
        Scope = scope,
        ScopeId = scopeId,
        LineOfBusinessId = lob,
        IsActive = true
    };

    [Property(MaxTest = 100)] // Feature: application-account-creation, Property 17: Independent sequences per scope
    public bool CountersAreIndependentPerSchemeKey(byte[] picks)
    {
        var lob1 = Guid.NewGuid(); var lob2 = Guid.NewGuid(); var b1 = Guid.NewGuid(); var b2 = Guid.NewGuid();
        var repo = new InMemoryNumberingSchemeRepository(new[]
        {
            Make("S-LB", NumberScope.Branch, b1, lob1), Make("S-L", NumberScope.Global, null, lob1),
            Make("S-B", NumberScope.Branch, b1, null), Make("S-G", NumberScope.Global, null, null)
        });
        var handler = new IssueNumberCommandHandler(repo, new FakeNumberingReferences());
        var last = new Dictionary<Guid, long>();
        foreach (var p in picks)
        {
            var lob = (p & 1) == 0 ? lob1 : lob2;
            var branch = (p & 2) == 0 ? b1 : b2;
            var issued = handler.Handle(new IssueNumberCommand("LoanAccount", NumberScope.Global, null, "t", lob, branch, true), default)
                .GetAwaiter().GetResult();
            var expected = last.TryGetValue(issued.SchemeId, out var previous) ? previous + 1 : 1;
            if (issued.SequenceValue != expected) return false;
            last[issued.SchemeId] = issued.SequenceValue;
        }
        return true;
    }

    [Fact]
    public void LobAndBranchTokensAreRendered()
    {
        var utc = new DateTime(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal("RETAIL-MUM01-0007",
            NumberFormatter.Format("{LOB}-{BRANCH}-{SEQ:4}", 7, 1, null, null, utc, "RETAIL", "MUM01"));
    }

    [Fact]
    public void LobTokenIsUpperCaseAlphanumericAndCapped()
    {
        Assert.Equal("RETAILLEND", LobTokenFormatter.ToToken("Retail Lending (India)"));
    }

    [Fact]
    public void MaxRenderedLengthIncludesPrefixSuffixAndTokenCeilings()
    {
        var length = NumberFormatter.MaxRenderedLength("{PREFIX}{LOB}{BRANCH}{SEQ:5}{SUFFIX}", 5, new string('P', 20), new string('S', 20));
        Assert.Equal(20 + 10 + 20 + 5 + 20, length);
    }

    [Fact]
    public async Task UnknownLineOfBusinessIsRejectedBeforeAnySequenceMoves()
    {
        var scheme = Make("S-G", NumberScope.Global, null, null);
        var repo = new InMemoryNumberingSchemeRepository(new[] { scheme });
        var handler = new IssueNumberCommandHandler(repo, new NoLobReferences());
        await Assert.ThrowsAsync<Finnova.Models.Domain.Exceptions.NumberingSchemeValidationException>(() =>
            handler.Handle(new IssueNumberCommand("LoanAccount", NumberScope.Global, null, "t", Guid.NewGuid(), Guid.NewGuid(), true), default));
        Assert.Equal(0, repo.Snapshot().Single().CurrentValue);
    }

    private sealed class NoLobReferences : Finnova.Repository.Interfaces.INumberingReferenceRepository
    {
        public Task<string?> GetLobTokenAsync(Guid id, CancellationToken ct) => Task.FromResult<string?>(null);
        public Task<string?> GetBranchCodeAsync(Guid id, CancellationToken ct) => Task.FromResult<string?>("BR");
    }
}
