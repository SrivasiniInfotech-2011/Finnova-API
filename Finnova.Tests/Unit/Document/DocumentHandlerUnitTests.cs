using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Service.Document.Commands.DeleteDocument;
using Finnova.Service.Document.Commands.UpdateValidationConfig;
using Finnova.Tests.Infrastructure;
using FluentValidation;
using Xunit;

namespace Finnova.Tests.Unit.Document;

public class DocumentHandlerUnitTests
{
    private static Finnova.Models.Domain.Entities.Document Seeded(
        InMemoryDocumentRepository repo, DocumentStatus status, int version = 0, bool aadhaar = false)
    {
        var id = Guid.NewGuid();
        var d = new Finnova.Models.Domain.Entities.Document
        {
            Id = id, DocCode = "DOC-X", DocName = "n", DocCategory = "KYC",
            ApplicableEntity = "CUSTOMER", Status = status, MakerId = "maker-1",
            AuthorizedVersionNumber = version, IsAadhaarDocument = aadhaar
        };
        repo.Seed(d);
        return d;
    }

    [Fact] // R9.4
    public async Task ValidationConfig_AadhaarWithoutMasking_IsRejected()
    {
        var repo = new InMemoryDocumentRepository();
        var audit = new InMemoryDocumentAuditRepository();
        var lookups = new FakeLookupValidationPort().WithAllowAll();
        var d = Seeded(repo, DocumentStatus.Active, version: 1, aadhaar: true);
        var handler = new UpdateValidationConfigCommandHandler(repo, audit, lookups);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            handler.Handle(new UpdateValidationConfigCommand(
                d.Id, "NONE", null, RequiresMasking: false, null, 5, new[] { "PDF" }, "maker-1"), default));
        Assert.Contains("Aadhaar must always be masked", ex.Message);
        Assert.Equal(0, audit.Count);   // R8.3 no audit on rejected action
    }

    [Fact] // R7.3
    public async Task Delete_ActiveDocument_IsBlocked()
    {
        var repo = new InMemoryDocumentRepository();
        var audit = new InMemoryDocumentAuditRepository();
        var usage = new FakeDocumentUsageChecker { Referenced = false };
        var d = Seeded(repo, DocumentStatus.Active, version: 1);
        var handler = new DeleteDocumentCommandHandler(repo, audit, usage);

        await Assert.ThrowsAsync<DocumentConflictException>(() =>
            handler.Handle(new DeleteDocumentCommand(d.Id, "maker-1"), default));
        Assert.Equal(1, repo.Count);
    }

    [Fact] // R7.1 / ERR-DOC-002
    public async Task Delete_ReferencedDocument_ThrowsInUse()
    {
        var repo = new InMemoryDocumentRepository();
        var audit = new InMemoryDocumentAuditRepository();
        var usage = new FakeDocumentUsageChecker { Referenced = true };
        var d = Seeded(repo, DocumentStatus.PendingApproval, version: 0);
        var handler = new DeleteDocumentCommandHandler(repo, audit, usage);

        await Assert.ThrowsAsync<DocumentInUseException>(() =>
            handler.Handle(new DeleteDocumentCommand(d.Id, "maker-1"), default));
        Assert.Equal(1, repo.Count);
    }

    [Fact] // R7.5 fail-safe on usage-checker error
    public async Task Delete_UsageCheckerThrows_IsBlockedFailSafe()
    {
        var repo = new InMemoryDocumentRepository();
        var audit = new InMemoryDocumentAuditRepository();
        var usage = new FakeDocumentUsageChecker { Throw = true };
        var d = Seeded(repo, DocumentStatus.PendingApproval, version: 0);
        var handler = new DeleteDocumentCommandHandler(repo, audit, usage);

        await Assert.ThrowsAsync<DocumentConflictException>(() =>
            handler.Handle(new DeleteDocumentCommand(d.Id, "maker-1"), default));
        Assert.Equal(1, repo.Count);
    }

    // Remaining example/edge tests to add in this file (design Testing Strategy):
    //  - Duplicate code on create (R2.2); DocCode not part of UpdateDocumentRequest (R2.3).
    //  - Single-pending conflict on a second edit (R3.5); not-found on edit/review/delete (R3.3/R5.2/R7.4).
    //  - Self-auth on reject (R5.5); reject remark bounds 1..500 (R5.7).
    //  - Inactivate only from Active, reactivate only from Inactive (R6.1/R6.4).
    //  - Delete of a never-authorized PendingApproval succeeds and writes a Delete audit (R7.2/R8.1).
    //  - Mask required when masking enabled (R9.3); uncompilable regex rejected (R9.5);
    //    MIME set non-empty subset of {PDF,JPG,PNG} (R9.9).
    //  - Consumption unknown Constitution/Entity Role => ValidationException (R11.6);
    //    defined-but-empty scope => empty list (R11.5).
    //  - Paging bounds: page < 1 and pageSize < 1 / > 100 rejected (R10.6/R10.7).
}