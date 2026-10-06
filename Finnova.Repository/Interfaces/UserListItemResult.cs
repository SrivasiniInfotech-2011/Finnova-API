using Finnova.Models.Domain.Enums;

namespace Finnova.Repository.Interfaces;

/// <summary>
/// Repository-level union projection for the paged list (R13). The repository unions user_accounts,
/// user_groups and functional_groups into this flat shape; the service maps it to UserListItemResponse.
/// </summary>
public record UserListItemResult(Guid Id, string Code, string Name, UserConfiguration Kind, bool IsActive);
