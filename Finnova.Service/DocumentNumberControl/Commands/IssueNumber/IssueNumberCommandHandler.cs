using Finnova.Models.Contracts.DocumentNumberControl;
using Finnova.Models.Domain.Exceptions;
using Finnova.Models.Domain.Numbering;
using Finnova.Repository.Interfaces;
using MediatR;

namespace Finnova.Service.DocumentNumberControl.Commands.IssueNumber;

/// <summary>
/// Handles number issuance (R5). Delegates the atomic sequence advance to the repository, then
/// formats the number from the scheme's template. Not-found scheme -> 404 (R5.6). No audit (R6.4).
/// </summary>
public class IssueNumberCommandHandler : IRequestHandler<IssueNumberCommand, IssuedNumberResponse>
{
    private readonly INumberingSchemeRepository _repository;

    public IssueNumberCommandHandler(INumberingSchemeRepository repository) => _repository = repository;

    public async Task<IssuedNumberResponse> Handle(IssueNumberCommand request, CancellationToken cancellationToken)
    {
        var documentType = request.DocumentType.Trim();
        var issuedAt = DateTime.UtcNow;

        // Repository advances the sequence atomically; throws Inactive/Exhausted; null => not found.
        var result = await _repository.IssueNextAsync(documentType, request.Scope, request.ScopeId, issuedAt, cancellationToken)
            ?? throw new NumberingSchemeNotFoundException(documentType);   // R5.6

        var (scheme, sequenceValue) = result;

        var number = NumberFormatter.Format(
            scheme.FormatTemplate, sequenceValue, scheme.SeqPadding, scheme.Prefix, scheme.Suffix, issuedAt); // R5.5

        return new IssuedNumberResponse(scheme.Id, scheme.DocumentType, number, sequenceValue, issuedAt);
    }
}
