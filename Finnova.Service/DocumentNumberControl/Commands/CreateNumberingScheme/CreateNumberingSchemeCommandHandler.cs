using Finnova.Models.Contracts.DocumentNumberControl;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Models.Domain.Numbering;
using Finnova.Repository.Interfaces;
using Finnova.Service.DocumentNumberControl.Internal;
using Finnova.Service.Mappers;
using MediatR;

namespace Finnova.Service.DocumentNumberControl.Commands.CreateNumberingScheme;

/// <summary>
/// Handles scheme creation: validates the template and scope consistency, enforces code and
/// document-type/scope uniqueness, applies defaults, persists the scheme, and records exactly one
/// Create audit entry (R1, R2, R6.1).
/// </summary>
public class CreateNumberingSchemeCommandHandler
    : IRequestHandler<CreateNumberingSchemeCommand, NumberingSchemeResponse>
{
    private readonly INumberingSchemeRepository _repository;
    private readonly INumberingSchemeAuditRepository _auditRepository;

    public CreateNumberingSchemeCommandHandler(
        INumberingSchemeRepository repository,
        INumberingSchemeAuditRepository auditRepository)
    {
        _repository = repository;
        _auditRepository = auditRepository;
    }

    public async Task<NumberingSchemeResponse> Handle(
        CreateNumberingSchemeCommand request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim();
        var documentType = request.DocumentType.Trim();
        var scope = request.Scope ?? NumberScope.Global;

        // Scope / scope-id consistency (R1.10/1.11).
        if (scope == NumberScope.Global && request.ScopeId is not null)
            throw new NumberingSchemeValidationException("A scope id must not be supplied for global scope.");
        if (scope != NumberScope.Global && request.ScopeId is null)
            throw new NumberingSchemeValidationException("A scope id is required for company or branch scope.");

        // Template validity (R1.12/1.13).
        var templateErrors = NumberFormatter.Validate(request.FormatTemplate);
        if (templateErrors.Count > 0)
            throw new NumberingSchemeValidationException(string.Join(" ", templateErrors));

        // Uniqueness (R2.2, R2.4).
        if (await _repository.ExistsByCodeAsync(code, null, cancellationToken))
            throw new NumberingSchemeDuplicateCodeException();
        if (await _repository.ExistsByScopeAsync(documentType, scope, request.ScopeId, null, cancellationToken))
            throw new NumberingSchemeScopeConflictException();

        var entity = new NumberingScheme
        {
            Code = code,
            Name = request.Name.Trim(),
            DocumentType = documentType,
            FormatTemplate = request.FormatTemplate.Trim(),
            Prefix = request.Prefix,
            Suffix = request.Suffix,
            SeqStart = request.SeqStart ?? 1,          // R1.4
            SeqIncrement = request.SeqIncrement ?? 1,  // R1.4
            SeqPadding = request.SeqPadding ?? 1,      // R1.4
            ResetRule = request.ResetRule,
            Scope = scope,                             // R1.3
            ScopeId = request.ScopeId,
            IsActive = request.IsActive ?? true,       // R1.2
            CurrentValue = 0,
            PeriodKey = string.Empty,
        };

        await _repository.AddAsync(entity, cancellationToken);

        await _auditRepository.AddAsync(new NumberingSchemeAuditEntry
        {
            SchemeId = entity.Id,
            Action = NumberingSchemeAuditAction.Create,
            OldValues = null,
            NewValues = SchemeAuditSnapshot.Editable(entity),
            Summary = $"Created scheme '{entity.Code}' for document type '{entity.DocumentType}'.",
            ChangedBy = request.ActingAdmin,
            ChangedAtUtc = DateTime.UtcNow,
        }, cancellationToken);

        return entity.ToResponse();
    }
}
