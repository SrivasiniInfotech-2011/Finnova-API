using Finnova.Models.Contracts.DocumentNumberControl;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Exceptions;
using Finnova.Models.Domain.Numbering;
using Finnova.Repository.Interfaces;
using Finnova.Service.DocumentNumberControl.Internal;
using MediatR;

namespace Finnova.Service.DocumentNumberControl.Commands.IssueNumber;

/// <summary>
/// Handles number issuance (R5). Delegates the atomic sequence advance to the repository, then
/// formats the number from the scheme's template. Not-found scheme -> 404 (R5.6). No audit (R6.4).
/// </summary>
public class IssueNumberCommandHandler : IRequestHandler<IssueNumberCommand, IssuedNumberResponse>
{
    private readonly INumberingSchemeRepository _repository;
    private readonly INumberingReferenceRepository? _references;

    public IssueNumberCommandHandler(INumberingSchemeRepository repository, INumberingReferenceRepository? references = null)
    {
        _repository = repository;
        _references = references;
    }

    public async Task<IssuedNumberResponse> Handle(IssueNumberCommand request, CancellationToken cancellationToken)
    {
        var documentType = request.DocumentType.Trim();
        var issuedAt = DateTime.UtcNow;
        string? lobCode = null, branchCode = null, level = null;
        (NumberingScheme Scheme, long SequenceValue)? result;

        if (request.UseFallback)
        {
            if (_references is null) throw new InvalidOperationException("The reference repository is required for fallback issuance.");
            if (request.LineOfBusinessId is not { } lobId || request.BranchId is not { } branchId)
                throw new NumberingSchemeValidationException("Line of Business and Branch are required.");

            // R16.12: unknown LOB or Branch is rejected before any sequence moves.
            lobCode = await _references.GetLobTokenAsync(lobId, cancellationToken)
                ?? throw new NumberingSchemeValidationException("The Line of Business does not exist or is inactive.");
            branchCode = await _references.GetBranchCodeAsync(branchId, cancellationToken)
                ?? throw new NumberingSchemeValidationException("The Branch does not exist or is inactive.");

            var candidates = await _repository.GetActiveByDocumentTypeAsync(documentType, cancellationToken);
            var resolution = SchemeResolver.Resolve(candidates, documentType, lobId, branchId)
                ?? throw new NumberingSchemeNotFoundException(documentType);   // no active scheme: no sequence advanced
            level = resolution.Level.ToString();
            result = await _repository.IssueNextByIdAsync(resolution.Scheme.Id, issuedAt, cancellationToken)
                ?? throw new NumberingSchemeNotFoundException(documentType);
        }
        else
        {
            // Repository advances the sequence atomically; throws Inactive/Exhausted; null => not found.
            result = await _repository.IssueNextAsync(documentType, request.Scope, request.ScopeId, issuedAt, cancellationToken)
                ?? throw new NumberingSchemeNotFoundException(documentType);   // R5.6
        }

        var (scheme, sequenceValue) = result.Value;
        var number = NumberFormatter.Format(
            scheme.FormatTemplate, sequenceValue, scheme.SeqPadding, scheme.Prefix, scheme.Suffix, issuedAt, lobCode, branchCode); // R5.5

        return new IssuedNumberResponse(scheme.Id, scheme.DocumentType, number, sequenceValue, issuedAt, scheme.Code, level);
    }
}