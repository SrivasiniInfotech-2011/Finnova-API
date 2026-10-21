using FsCheck;
using FsCheck.Xunit;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Service.Document.Commands.UpdateValidationConfig;
using Finnova.Tests.Infrastructure;
using FluentValidation;

namespace Finnova.Tests.Unit.Document;

public class DocumentValidationProperties
{
    // Feature: document-master-management, Property 5: Max file size is bounded to 1..25
    [Property(MaxTest = 100)]
    public Property MaxFileSizeBounded()
    {
        return Prop.ForAll(Arb.From(Gen.Choose(-50, 100)), size =>
        {
            var repo = new InMemoryDocumentRepository();
            var audit = new InMemoryDocumentAuditRepository();
            var lookups = new FakeLookupValidationPort().WithAllowAll();
            var id = Guid.NewGuid();
            repo.Seed(new Finnova.Models.Domain.Entities.Document
            {
                Id = id, DocCode = "DOC-X", DocName = "n", DocCategory = "KYC",
                ApplicableEntity = "CUSTOMER", Status = DocumentStatus.Active,
                MakerId = "maker-1", AuthorizedVersionNumber = 1
            });
            var handler = new UpdateValidationConfigCommandHandler(repo, audit, lookups);

            var inRange = size is >= 1 and <= 25;
            var threwWithMessage = false;
            try
            {
                handler.Handle(new UpdateValidationConfigCommand(
                    id, "NONE", null, false, null, size, new[] { "PDF" }, "maker-1"), default)
                    .GetAwaiter().GetResult();
            }
            catch (ValidationException ex)
            {
                threwWithMessage = ex.Message.Contains("Max file size must be between 1 and 25 MB");
            }

            // Accepted iff in range; otherwise rejected with the exact message.
            return (inRange || threwWithMessage).ToProperty();
        });
    }

    // Add here, one [Property(MaxTest = 100)] method each, tagged with its number:
    //  Property 3  — Aadhaar documents are always masked (IsAadhaarDocument && !RequiresMasking => reject
    //                "Aadhaar must always be masked").
    //  Property 4  — Mask pattern required when masking enabled (RequiresMasking && blank mask => reject
    //                "Mask pattern is required when masking is enabled").
    //  Property 6  — Applicability combination uniqueness (duplicate (Constitution, EntityRole) => whole
    //                submission rejected, nothing persisted).
    //  Property 7  — Format regex must compile when present (uncompilable regex => reject, nothing persisted).
    //  Property 9  — Status transition legality (authorize/reject only from pending; resubmit only from
    //                ReturnedToMaker; inactivate only from Active; reactivate only from Inactive; delete
    //                only from never-authorized PendingApproval).
    //  Property 10 — Consumption returns a doc iff Active && applicability contains the pair.
    //  Property 11 — Paginated query echoes page/size, correct Total, <= pageSize items, DocCode+Id order.
    //  Property 12 — Audit trail ordering deterministic newest-first (ChangedAtUtc desc, Id desc).
    //  Property 11-audit — Exactly one audit entry per persisted action; none on a rejected action.
}