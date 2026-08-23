using Riok.Mapperly.Abstractions;
using Finnova.Models.Contracts.Accounts;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;

namespace Finnova.Service.Mappers;

[Mapper(EnumMappingStrategy = EnumMappingStrategy.ByName)]
public static partial class TransactionMapper
{
    [MapperIgnoreSource(nameof(Transaction.Account))]
    public static partial TransactionResponse ToResponse(this Transaction transaction);

    public static List<TransactionResponse> ToResponseList(this IEnumerable<Transaction> transactions)
        => transactions.Select(t => t.ToResponse()).ToList();

    private static string TransactionTypeToString(TransactionType type) => type.ToString();
    private static string TransactionStatusToString(TransactionStatus status) => status.ToString();
}
