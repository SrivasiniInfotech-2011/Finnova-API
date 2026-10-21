using System.Text.RegularExpressions;
using Finnova.Models.Contracts.Documents;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Document.Internal;
using Finnova.Service.Document.Ports;
using Finnova.Service.Mappers;
using FluentValidation;
using MediatR;
using DocAudit = Finnova.Models.Domain.Entities.DocumentAuditEntry;

namespace Finnova.Service.Document.Commands.UpdateValidationConfig;

public record UpdateValidationConfigCommand(
    Guid Id, string ValidationEngine, string? FormatRegex, bool RequiresMasking,
    string? MaskPattern, int MaxFileSizeMb, IReadOnlyList<string> AllowedMimeTypes,
    string ActingUser) : IRequest<DocumentResponse>;

public class UpdateValidationConfigCommandHandler
    : IRequestHandler<UpdateValidationConfigCommand, DocumentResponse>
{
    private static readonly HashSet<string> AllowedMime =
        new(StringComparer.OrdinalIgnoreCase) { "PDF", "JPG", "PNG" };

    private readonly IDocumentRepository _repository;
    private readonly IDocumentAuditRepository _audit;
    private readonly ILookupValidationPort _lookups;

    public UpdateValidationConfigCommandHandler(IDocumentRepository repository,
        IDocumentAuditRepository audit, ILookupValidationPort lookups)
    { _repository = repository; _audit = audit; _lookups = lookups; }

    public async Task<DocumentResponse> Handle(UpdateValidationConfigCommand request, CancellationToken ct)
    {
        var entity = await _repository.GetByIdWithRulesAsync(request.Id, ct)
            ?? throw new DocumentNotFoundException();

        if (entity.Status is DocumentStatus.PendingModification
            || (entity.Status is DocumentStatus.PendingApproval && entity.PendingValuesJson is not null))
            throw new DocumentConflictException("A change is already pending authorization."); // R3.5

        // R9.2 — ValidationEngine must be an ACTIVE lookup value (also blocks free text).
        if (!await _lookups.IsActiveLookupAsync("VALIDATION_ENGINE", request.ValidationEngine, ct))
            throw new ValidationException("Validation Engine is not an active lookup value.");

        // R9.8 — max file size bounds (exact message).
        if (request.MaxFileSizeMb is < 1 or > 25)
            throw new ValidationException("Max file size must be between 1 and 25 MB");

        // R9.4 — Aadhaar must always be masked.
        if (entity.IsAadhaarDocument && !request.RequiresMasking)
            throw new ValidationException("Aadhaar must always be masked");

        // R9.3 — mask pattern required when masking is enabled.
        if (request.RequiresMasking && string.IsNullOrWhiteSpace(request.MaskPattern))
            throw new ValidationException("Mask pattern is required when masking is enabled");

        // R9.6 / R9.7 — length caps.
        if (request.MaskPattern is { Length: > 200 })
            throw new ValidationException("Mask Pattern must not exceed 200 characters.");
        if (request.FormatRegex is { Length: > 500 })
            throw new ValidationException("Format Regex must not exceed 500 characters.");

        // R9.5 — regex must compile when present (stored, not executed — R9.11).
        if (!string.IsNullOrWhiteSpace(request.FormatRegex))
        {
            try { _ = new Regex(request.FormatRegex); }
            catch (ArgumentException)
            { throw new ValidationException("Format Regex is not a valid regular expression."); }
        }

        // R9.9 — allowed MIME set non-empty and within {PDF,JPG,PNG}.
        if (request.AllowedMimeTypes is null || request.AllowedMimeTypes.Count == 0
            || request.AllowedMimeTypes.Any(m => !AllowedMime.Contains(m)))
            throw new ValidationException("Allowed Mime Types must be a non-empty subset of PDF, JPG, PNG.");

        // R9.1 / R9.10 — stage as a pending change; live values unchanged until authorize.
        entity.PendingValuesJson = DocumentPendingPayload.ForValidationConfig(
            request.ValidationEngine, request.FormatRegex, request.RequiresMasking,
            request.MaskPattern, request.MaxFileSizeMb, request.AllowedMimeTypes);
        entity.Status = entity.Status == DocumentStatus.Active
            ? DocumentStatus.PendingModification : DocumentStatus.PendingApproval;
        entity.MakerId = request.ActingUser;
        entity.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(entity, ct);

        await _audit.AddAsync(new DocAudit
        {
            DocumentId = entity.Id,
            Action = DocumentAuditAction.ValidationConfigChange,
            OldValues = DocumentAuditSnapshot.Editable(entity),
            NewValues = entity.PendingValuesJson,
            Summary = $"Submitted validation/masking config for '{entity.DocCode}'.",
            ChangedBy = request.ActingUser,
            ChangedAtUtc = DateTime.UtcNow,
        }, ct);

        return entity.ToResponse();
    }
}