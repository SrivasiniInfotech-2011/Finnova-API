using FsCheck;
using FsCheck.Xunit;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Service.Document.Commands.AuthorizeDocument;
using Finnova.Service.Document.Commands.CreateDocument;
using Finnova.Service.Document.Commands.UpdateDocument;
using Finnova.Service.Document.Queries.GetActiveDocumentsForConsumption;
using Finnova.Tests.Infrastructure;
using Xunit;

namespace Finnova.Tests.Unit.Document;

public class DocumentMakerCheckerProperties
{
    // Generators: non-empty alphanumeric-hyphen codes, 1..20 chars.
    private static Arbitrary<string> DocCodeArb() =>
        Arb.From(Gen.Choose(1, 20).SelectMany(len =>
            Gen.Elements("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-".ToCharArray())
               .ListOf(len).Select(cs => new string(cs.ToArray()))));

    private static (CreateDocumentCommandHandler handler,
                    InMemoryDocumentRepository repo,
                    InMemoryDocumentAuditRepository audit) NewCreateHandler()
    {
        var repo = new InMemoryDocumentRepository();
        var audit = new InMemoryDocumentAuditRepository();
        var lookups = new FakeLookupValidationPort().WithAllowAll();
        return (new CreateDocumentCommandHandler(repo, audit, lookups), repo, audit);
    }

    // Feature: document-master-management, Property 1: Maker cannot authorize or reject own change
    [Property(MaxTest = 100)]
    public Property MakerCannotAuthorizeOwnChange()
    {
        return Prop.ForAll(DocCodeArb(), code =>
        {
            var repo = new InMemoryDocumentRepository();
            var audit = new InMemoryDocumentAuditRepository();
            var id = Guid.NewGuid();
            repo.Seed(new Finnova.Models.Domain.Entities.Document
            {
                Id = id, DocCode = code, DocName = "n", DocCategory = "KYC",
                ApplicableEntity = "CUSTOMER", Status = DocumentStatus.PendingApproval,
                MakerId = "user-1"
            });
            var handler = new AuthorizeDocumentCommandHandler(repo, audit);

            var threw = false;
            try { handler.Handle(new AuthorizeDocumentCommand(id, "user-1"), default).GetAwaiter().GetResult(); }
            catch (DocumentSelfAuthorizationException) { threw = true; }

            var after = repo.Snapshot().Single(x => x.Id == id);
            return (threw && after.Status == DocumentStatus.PendingApproval && audit.Count == 0)
                .ToProperty();
        });
    }

    // Feature: document-master-management, Property 2: DocCode uniqueness is trimmed and case-insensitive
    [Property(MaxTest = 100)]
    public Property DocCodeUniquenessIsTrimmedAndCaseInsensitive()
    {
        return Prop.ForAll(DocCodeArb(), code =>
        {
            var (handler, repo, _) = NewCreateHandler();
            handler.Handle(Cmd(code), default).GetAwaiter().GetResult();

            var dup = $"  {code.ToLowerInvariant()}  ";   // whitespace + different case
            var threw = false;
            try { handler.Handle(Cmd(dup), default).GetAwaiter().GetResult(); }
            catch (DocumentDuplicateCodeException) { threw = true; }

            return (threw && repo.Count == 1).ToProperty();
        });

        static CreateDocumentCommand Cmd(string code) => new(
            code, "Name", null, null, "KYC", "CUSTOMER",
            null, null, null, null, null, "maker-1");
    }

    // Feature: document-master-management, Property 8: Pending edit never alters live values until authorize
    [Property(MaxTest = 100)]
    public Property PendingEditDoesNotAlterLiveUntilAuthorize()
    {
        return Prop.ForAll(Arb.From(Gen.Elements("Old Name", "First", "Base")), liveName =>
        {
            var repo = new InMemoryDocumentRepository();
            var audit = new InMemoryDocumentAuditRepository();
            var lookups = new FakeLookupValidationPort().WithAllowAll();
            var id = Guid.NewGuid();
            repo.Seed(new Finnova.Models.Domain.Entities.Document
            {
                Id = id, DocCode = "DOC-X", DocName = liveName, DocCategory = "KYC",
                ApplicableEntity = "CUSTOMER", Status = DocumentStatus.Active,
                MakerId = "maker-1", AuthorizedVersionNumber = 1,
                ApplicabilityRules = { new DocumentApplicabilityRule
                    { DocumentId = id, Constitution = "Individual", EntityRole = "CUSTOMER", IsMandatory = true } }
            });

            var update = new UpdateDocumentCommandHandler(repo, audit, lookups);
            update.Handle(new UpdateDocumentCommand(
                id, "New Name", null, null, "KYC", "CUSTOMER", true, false, "maker-1"), default)
                .GetAwaiter().GetResult();

            // After edit (pending), consumption still sees the live name.
            var consume = new GetActiveDocumentsForConsumptionQueryHandler(repo, lookups);
            var beforeAuth = consume.Handle(
                new GetActiveDocumentsForConsumptionQuery("Individual", "CUSTOMER"), default)
                .GetAwaiter().GetResult();

            // A different user authorizes.
            var authorize = new AuthorizeDocumentCommandHandler(repo, audit);
            authorize.Handle(new AuthorizeDocumentCommand(id, "checker-1"), default).GetAwaiter().GetResult();
            var afterAuth = consume.Handle(
                new GetActiveDocumentsForConsumptionQuery("Individual", "CUSTOMER"), default)
                .GetAwaiter().GetResult();

            return (beforeAuth.Single().DocName == liveName
                    && afterAuth.Single().DocName == "New Name").ToProperty();
        });
    }
}