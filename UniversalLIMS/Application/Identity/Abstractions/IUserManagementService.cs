using UniversalLIMS.Application.Identity;

namespace UniversalLIMS.Application.Identity.Abstractions;

public interface IUserManagementService
{
    Task<IReadOnlyList<UserListItemDto>> GetListAsync(UserListQuery query, CancellationToken cancellationToken = default);

    Task<UserEditDto?> GetForEditAsync(string userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, BranchPortalAccountDto?>> GetBranchPortalAccountsAsync(
        CancellationToken cancellationToken = default);

    Task<string> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default);

    Task UpdateAsync(string userId, UpdateUserRequest request, CancellationToken cancellationToken = default);

    Task<UserPasswordRevealDto> GetRevealablePasswordAsync(string userId, CancellationToken cancellationToken = default);
}
