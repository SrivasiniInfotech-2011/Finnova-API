using Riok.Mapperly.Abstractions;
using Finnova.Models.Contracts.Accounts;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;

namespace Finnova.Service.Mappers;

[Mapper(EnumMappingStrategy = EnumMappingStrategy.ByName)]
public static partial class AccountMapper
{
    public static partial AccountResponse ToResponse(this Account account);

    public static List<AccountResponse> ToResponseList(this IEnumerable<Account> accounts)
        => accounts.Select(a => a.ToResponse()).ToList();

    private static string AccountTypeToString(AccountType type) => type.ToString();
    private static string AccountStatusToString(AccountStatus status) => status.ToString();
}
