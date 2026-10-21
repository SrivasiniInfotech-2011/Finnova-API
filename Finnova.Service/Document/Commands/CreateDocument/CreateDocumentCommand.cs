using Finnova.Models.Contracts.Documents;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Document.Internal;
using Finnova.Service.Document.Ports;
using Finnova.Service.Mappers;
using FluentValidation;
using MediatR;

namespace Finnova.Service.Document.Commands.CreateDocument;

public record CreateDocumentCommand(
    string DocCode, string DocName, string? DocShortName, string? Description,
    string DocCategory, string ApplicableEntity,
    bool? IsMandatory, bool? HasExpiry, bool? RequiresMasking, bool? IsAadhaarDocument,
    int? MaxFileSizeMb, string ActingUser) : IRequest<DocumentResponse>;

public class CreateDocumentCommandHandler : IRequestHandler<CreateDocumentCommand, DocumentResponse>
{
    private readonly IDocumentRepository _repository;
    private readonly IDocumentAuditRepository _audit;
    private readonly ILookupValidationPort _lookups;

    public CreateDocumentCommandHandler(IDocumentRepository repository,
        IDocumentAuditRepository audit, ILookupValidationPort lookups)
    { _repository = repository; _audit = audit; _lookups = lookups; }

    public async Task<DocumentResponse> Handle(CreateDocumentCommand request, CancellationToken ct)
    {
        var code = request.DocCode.Trim();

        // R1.8 lookup-backed fields must be ACTIVE lookup values.
        if (!await _lookups.IsActiveLookupAsync("DOC_CATEGORY", request.DocCategory, ct))
            throw new ValidationException("Doc Category is not an active lookup value.");
        if (!await _lookups.IsActiveLookupAsync("APPLICABLE_ENTITY", request.ApplicableEntity, ct))
            throw new ValidationException("Applicable Entity is not an active lookup value.");

        // R2.2 trimmed, case-insensitive uniqueness.
        if (await _repository.ExistsByCodeAsync(code, null, ct))
            throw new DocumentDuplicateCodeException();

        var entity = new Finnova.Models.Domain.Entities.Document
        {
            DocCode = code,
            DocName = request.DocName.Trim(),
            DocShortName = request.DocShortName?.Trim(),
            Description = request.Description?.Trim(),
            DocCategory = request.DocCategory,
            ApplicableEntity = request.ApplicableEntity,
            IsMandatory = request.IsMandatory ?? false,          // R1.9
            HasExpiry = request.HasExpiry ?? false,              // R1.9
            RequiresMasking = request.RequiresMasking ?? false,  // R1.9
            IsAadhaarDocument = request.IsAadhaarDocument ?? false,
            MaxFileSizeMb = request.MaxFileSizeMb ?? 5,          // R1.6 default
            Status = DocumentStatus.PendingApproval,             // R1.1 / R1.2
            MakerId = request.ActingUser,
        };

        await _repository.AddAsync(entity, ct);
        await _audit.AddAsync(new DocumentAuditEntry
        {
            DocumentId = entity.Id,
            Action = DocumentAuditAction.Create,
            OldValues = null,
            NewValues = DocumentAuditSnapshot.Editable(entity),
            Summary = $"Created document '{entity.DocCode}'.",
            ChangedBy = request.ActingUser,
            ChangedAtUtc = DateTime.UtcNow,
        }, ct);

        return entity.ToResponse();
    }
}

public class CreateDocumentCommandValidator : AbstractValidator<CreateDocumentCommand>
{
    public CreateDocumentCommandValidator()
    {
        RuleFor(x => x.DocCode).NotEmpty().WithMessage("Doc Code is required.")           // R1.3
            .Must(c => c is not null && c.Trim().Length <= 20)
                .WithMessage("Doc Code must not exceed 20 characters.")                   // R1.4
            .Matches("^[A-Za-z0-9-]+$")
                .WithMessage("Doc Code may contain only letters, digits, and hyphens.");  // R1.4
        RuleFor(x => x.DocName).NotEmpty().WithMessage("Doc Name is required.")           // R1.3
            .Must(n => n is not null && n.Trim().Length <= 150)
                .WithMessage("Doc Name must not exceed 150 characters.");                 // R1.5
        RuleFor(x => x.DocCategory).NotEmpty().WithMessage("Doc Category is required.");  // R1.3
        RuleFor(x => x.ApplicableEntity).NotEmpty().WithMessage("Applicable Entity is required."); // R1.3
        RuleFor(x => x.MaxFileSizeMb).InclusiveBetween(1, 25)
            .WithMessage("Max file size must be between 1 and 25 MB")                     // R1.7
            .When(x => x.MaxFileSizeMb.HasValue);
    }
}