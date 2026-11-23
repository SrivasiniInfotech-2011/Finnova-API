using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Finnova.Models.Contracts.AppropriationLogic;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Service.AppropriationLogic.Ports;

namespace Finnova.Service.AppropriationLogic.Rules;

public sealed record NormalizedDetail(AppropriationDueType DueType, string CashFlowDueTypeCode, decimal Percentage, int SequenceNumber);
public sealed record NormalizedRule(Guid CompanyId, Guid? LineOfBusinessId, AppropriationDirection Direction, bool FullDue, IReadOnlyList<NormalizedDetail> Details, decimal PercentageTotal, string Hash);

public static class AppropriationLogicNormalizer
{
    public static async Task<NormalizedRule> NormalizeAsync(Guid companyId, Guid? lobId, AppropriationDirection direction, bool fullDue, IReadOnlyList<AppropriationLogicDetailRequest> input, IAppropriationReferencePort references, CancellationToken ct)
    {
        if (companyId == Guid.Empty) throw new AppropriationLogicValidationException("CompanyId is required.");
        if (!Enum.IsDefined(direction)) throw new AppropriationLogicValidationException("Type must be Vertical or Horizontal.");
        if (input.Count == 0) throw new AppropriationLogicValidationException("At least one appropriation detail is required.");
        if (lobId.HasValue)
        {
            var lob = await references.GetLineOfBusinessAsync(lobId.Value, ct) ?? throw new AppropriationLogicValidationException("Line of Business was not found.");
            if (!lob.IsActive || lob.CompanyId != companyId) throw new AppropriationLogicValidationException("Line of Business is inactive or belongs to another Company.");
        }
        var details = new List<NormalizedDetail>();
        var seen = new HashSet<AppropriationDueType>();
        for (var index = 0; index < input.Count; index++)
        {
            var item = input[index];
            if (!seen.Add(item.DueType)) throw new AppropriationLogicConflictException(AppropriationLogicErrorCodes.DuplicateDueHead, "Duplicate appropriation due head is not allowed.");
            if (item.Percentage < 0m || item.Percentage > 100m || decimal.Round(item.Percentage, 2) != item.Percentage) throw new AppropriationLogicValidationException($"Percentage at sequence {index + 1} must be between 0.00 and 100.00 with at most two decimal places.");
            var mapping = await references.GetDueHeadMappingAsync(item.DueType, ct) ?? throw new AppropriationLogicDependencyException($"No due-ledger mapping exists for {item.DueType}.");
            if (!mapping.IsActive) throw new AppropriationLogicDependencyException($"The due-ledger mapping for {item.DueType} is inactive.");
            details.Add(new NormalizedDetail(item.DueType, mapping.CashFlowDueTypeCode, item.Percentage, index + 1));
        }
        var total = decimal.Round(details.Sum(x => x.Percentage), 2);
        if (total > 100m) throw new AppropriationLogicConflictException(AppropriationLogicErrorCodes.TotalOver100, "The total percentage cannot exceed 100.00.");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { companyId, lobId, direction, fullDue, details }))));
        return new NormalizedRule(companyId, lobId, direction, fullDue, details, total, hash);
    }
}