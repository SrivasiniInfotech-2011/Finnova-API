using Riok.Mapperly.Abstractions;
using Finnova.Models.Contracts.Users;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;

namespace Finnova.Service.Mappers;

[Mapper(EnumMappingStrategy = EnumMappingStrategy.ByName)]
public static partial class UserMapper
{
    // PasswordHash is intentionally never projected onto the response contract — a credential
    // hash must not leave the domain. Declaring it as an ignored source member tells Mapperly the
    // omission is deliberate and clears RMG020.
    [MapperIgnoreSource(nameof(User.Organization))]
    [MapperIgnoreSource(nameof(User.PasswordHash))]
    public static partial UserResponse ToResponse(this User user);

    public static List<UserResponse> ToResponseList(this IEnumerable<User> users)
        => users.Select(u => u.ToResponse()).ToList();

    private static string UserRoleToString(UserRole role) => role.ToString();
    private static string UserStatusToString(UserStatus status) => status.ToString();
}
