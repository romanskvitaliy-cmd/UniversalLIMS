using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Identity;
using UniversalLIMS.Application.Identity.Abstractions;
using UniversalLIMS.Application.Security;
using UniversalLIMS.Domain.Identity;
using UniversalLIMS.Infrastructure.Persistence;

namespace UniversalLIMS.Infrastructure.Identity;

public sealed class UserManagementService : IUserManagementService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public UserManagementService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<IReadOnlyList<UserListItemDto>> GetListAsync(
        UserListQuery query,
        CancellationToken cancellationToken = default)
    {
        var usersQuery = _context.Users.AsNoTracking().AsQueryable();

        if (!query.IncludeInactive)
        {
            usersQuery = usersQuery.Where(user => user.IsActive);
        }

        if (query.BranchId.HasValue)
        {
            usersQuery = usersQuery.Where(user => user.BranchId == query.BranchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            usersQuery = usersQuery.Where(user =>
                user.Email!.Contains(search)
                || user.FullName.Contains(search)
                || (user.Position != null && user.Position.Contains(search)));
        }

        var users = await usersQuery
            .OrderBy(user => user.FullName)
            .ThenBy(user => user.Email)
            .Select(user => new
            {
                user.Id,
                user.Email,
                user.FullName,
                user.Position,
                user.BranchId,
                user.IsActive
            })
            .ToListAsync(cancellationToken);

        var branchLookup = await _context.Branches
            .AsNoTracking()
            .Select(branch => new { branch.Id, branch.Code, branch.Name })
            .ToDictionaryAsync(branch => branch.Id, cancellationToken);

        var roleLookup = await BuildUserRolesLookupAsync(cancellationToken);
        var roleFilter = string.IsNullOrWhiteSpace(query.Role) ? null : query.Role.Trim();

        return users
            .Select(user =>
            {
                var roles = roleLookup.GetValueOrDefault(user.Id) ?? [];
                return new UserListItemDto
                {
                    Id = user.Id,
                    Email = user.Email ?? string.Empty,
                    FullName = user.FullName,
                    Position = user.Position,
                    BranchId = user.BranchId,
                    BranchCode = user.BranchId.HasValue && branchLookup.TryGetValue(user.BranchId.Value, out var branch)
                        ? branch.Code
                        : null,
                    BranchName = user.BranchId.HasValue && branchLookup.TryGetValue(user.BranchId.Value, out var branchInfo)
                        ? branchInfo.Name
                        : null,
                    IsActive = user.IsActive,
                    Roles = roles,
                    IsBranchPortalAccount = BranchPortalAccountConventions.IsBranchPortalEmail(user.Email)
                };
            })
            .Where(user => roleFilter is null || user.Roles.Contains(roleFilter, StringComparer.Ordinal))
            .ToList();
    }

    public async Task<UserEditDto?> GetForEditAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Where(item => item.Id == userId)
            .Select(item => new
            {
                item.Id,
                item.Email,
                item.FullName,
                item.Position,
                item.BranchId,
                item.IsActive
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return null;
        }

        var roles = await _userManager.GetRolesAsync(
            await _userManager.FindByIdAsync(userId) ?? throw new InvalidOperationException("Користувача не знайдено."));

        return new UserEditDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FullName = user.FullName,
            Position = user.Position,
            BranchId = user.BranchId,
            IsActive = user.IsActive,
            Roles = roles.OrderBy(role => role, StringComparer.Ordinal).ToArray(),
            IsBranchPortalAccount = BranchPortalAccountConventions.IsBranchPortalEmail(user.Email)
        };
    }

    public async Task<IReadOnlyDictionary<Guid, BranchPortalAccountDto?>> GetBranchPortalAccountsAsync(
        CancellationToken cancellationToken = default)
    {
        var branches = await _context.Branches
            .AsNoTracking()
            .Select(branch => new { branch.Id, branch.Code })
            .ToListAsync(cancellationToken);

        var portalEmails = branches
            .Select(branch => BranchPortalAccountConventions.BuildEmail(branch.Code))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var portalUsers = await _context.Users
            .AsNoTracking()
            .Where(user => portalEmails.Contains(user.Email!))
            .Select(user => new
            {
                user.Id,
                user.Email,
                user.FullName,
                user.BranchId,
                user.IsActive
            })
            .ToListAsync(cancellationToken);

        var roleLookup = await BuildUserRolesLookupAsync(cancellationToken);

        var accountsByBranch = portalUsers
            .Where(user => user.BranchId.HasValue)
            .GroupBy(user => user.BranchId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(user => user.Email, StringComparer.OrdinalIgnoreCase).First());

        var result = new Dictionary<Guid, BranchPortalAccountDto?>();
        foreach (var branch in branches)
        {
            if (!accountsByBranch.TryGetValue(branch.Id, out var user))
            {
                result[branch.Id] = null;
                continue;
            }

            result[branch.Id] = new BranchPortalAccountDto
            {
                UserId = user.Id,
                Email = user.Email ?? string.Empty,
                FullName = user.FullName,
                IsActive = user.IsActive,
                Roles = roleLookup.GetValueOrDefault(user.Id) ?? []
            };
        }

        return result;
    }

    public async Task<string> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRoles(request.Roles);
        ValidateBranchRequirement(request.Roles, request.BranchId);
        await EnsureBranchExistsAsync(request.BranchId, cancellationToken);

        var email = request.Email.Trim();
        var existing = await _userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            throw new InvalidOperationException($"Користувача з email «{email}» вже створено.");
        }

        var user = new ApplicationUser
        {
            Email = email,
            UserName = email,
            FullName = request.FullName.Trim(),
            Position = request.Position?.Trim(),
            BranchId = request.BranchId,
            EmailConfirmed = true,
            IsActive = request.IsActive
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(FormatIdentityErrors(createResult));
        }

        await AssignRolesAsync(user, request.Roles);
        return user.Id;
    }

    public async Task UpdateAsync(string userId, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRoles(request.Roles);
        ValidateBranchRequirement(request.Roles, request.BranchId);
        await EnsureBranchExistsAsync(request.BranchId, cancellationToken);

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            throw new InvalidOperationException("Користувача не знайдено.");
        }

        var currentRoles = (IReadOnlyList<string>)[.. await _userManager.GetRolesAsync(user)];
        await EnsureCanChangeAdministratorAsync(userId, currentRoles, request.Roles, request.IsActive, cancellationToken);

        var email = request.Email.Trim();
        if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            var duplicate = await _userManager.FindByEmailAsync(email);
            if (duplicate is not null && duplicate.Id != userId)
            {
                throw new InvalidOperationException($"Email «{email}» уже використовується.");
            }

            user.Email = email;
            user.UserName = email;
        }

        user.FullName = request.FullName.Trim();
        user.Position = request.Position?.Trim();
        user.BranchId = request.BranchId;
        user.IsActive = request.IsActive;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            throw new InvalidOperationException(FormatIdentityErrors(updateResult));
        }

        if (!string.IsNullOrWhiteSpace(request.NewPassword))
        {
            var passwordResult = await ResetPasswordAsync(user, request.NewPassword);
            if (!passwordResult.Succeeded)
            {
                throw new InvalidOperationException(FormatIdentityErrors(passwordResult));
            }
        }

        await SyncRolesAsync(user, currentRoles, request.Roles);
    }

    public async Task<UserPasswordRevealDto> GetRevealablePasswordAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return new UserPasswordRevealDto
            {
                StatusMessage = "Користувача не знайдено."
            };
        }

        foreach (var candidate in UserKnownPasswordCatalog.GetCandidates(user.Email))
        {
            if (await _userManager.CheckPasswordAsync(user, candidate.Password))
            {
                return new UserPasswordRevealDto
                {
                    CanReveal = true,
                    Password = candidate.Password,
                    StatusMessage = $"Поточний пароль ({candidate.SourceLabel})."
                };
            }
        }

        return new UserPasswordRevealDto
        {
            StatusMessage = "Пароль змінено — перегляд недоступний. Вкажіть новий у полі нижче."
        };
    }

    private async Task EnsureCanChangeAdministratorAsync(
        string userId,
        IReadOnlyList<string> currentRoles,
        IReadOnlyList<string> nextRoles,
        bool nextIsActive,
        CancellationToken cancellationToken)
    {
        var wasAdministrator = currentRoles.Contains(LimsRoles.SystemAdministrator, StringComparer.Ordinal);
        var willBeAdministrator = nextRoles.Contains(LimsRoles.SystemAdministrator, StringComparer.Ordinal);

        if (wasAdministrator && willBeAdministrator && nextIsActive)
        {
            return;
        }

        var activeAdministrators = await CountActiveAdministratorsAsync(cancellationToken);
        if (!wasAdministrator || activeAdministrators > 1)
        {
            return;
        }

        if (!willBeAdministrator || !nextIsActive)
        {
            throw new InvalidOperationException("Неможливо зняти роль або деактивувати останнього адміністратора системи.");
        }
    }

    private async Task<int> CountActiveAdministratorsAsync(CancellationToken cancellationToken)
    {
        var adminRoleId = await _context.Roles
            .AsNoTracking()
            .Where(role => role.Name == LimsRoles.SystemAdministrator)
            .Select(role => role.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (adminRoleId is null)
        {
            return 0;
        }

        return await _context.UserRoles
            .AsNoTracking()
            .Join(
                _context.Users.AsNoTracking().Where(user => user.IsActive),
                userRole => userRole.UserId,
                user => user.Id,
                (userRole, _) => userRole)
            .CountAsync(userRole => userRole.RoleId == adminRoleId, cancellationToken);
    }

    private async Task<Dictionary<string, IReadOnlyList<string>>> BuildUserRolesLookupAsync(
        CancellationToken cancellationToken)
    {
        var rows = await (
            from userRole in _context.UserRoles.AsNoTracking()
            join role in _context.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            select new { userRole.UserId, RoleName = role.Name })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.UserId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group
                    .Select(row => row.RoleName!)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToArray());
    }

    private async Task EnsureBranchExistsAsync(Guid? branchId, CancellationToken cancellationToken)
    {
        if (!branchId.HasValue)
        {
            return;
        }

        var exists = await _context.Branches
            .AsNoTracking()
            .AnyAsync(branch => branch.Id == branchId.Value, cancellationToken);

        if (!exists)
        {
            throw new InvalidOperationException("Обрану філію не знайдено.");
        }
    }

    private static void ValidateRoles(IReadOnlyList<string> roles)
    {
        if (roles.Count == 0)
        {
            throw new InvalidOperationException("Оберіть хоча б одну роль.");
        }

        foreach (var role in roles)
        {
            if (!LimsRoles.All.Contains(role, StringComparer.Ordinal))
            {
                throw new InvalidOperationException($"Невідома роль «{role}».");
            }
        }
    }

    private static void ValidateBranchRequirement(IReadOnlyList<string> roles, Guid? branchId)
    {
        var requiresBranch = roles.Any(role =>
            !string.Equals(role, LimsRoles.SystemAdministrator, StringComparison.Ordinal));

        if (requiresBranch && !branchId.HasValue)
        {
            throw new InvalidOperationException("Для робочих ролей потрібно обрати філію.");
        }
    }

    private async Task AssignRolesAsync(ApplicationUser user, IReadOnlyList<string> roles)
    {
        foreach (var role in roles.Distinct(StringComparer.Ordinal))
        {
            if (!await _roleManager.RoleExistsAsync(role))
            {
                throw new InvalidOperationException($"Роль «{role}» не існує в системі.");
            }

            var result = await _userManager.AddToRoleAsync(user, role);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(FormatIdentityErrors(result));
            }
        }
    }

    private async Task SyncRolesAsync(
        ApplicationUser user,
        IReadOnlyList<string> currentRoles,
        IReadOnlyList<string> nextRoles)
    {
        var nextSet = nextRoles.Distinct(StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        var currentSet = currentRoles.ToHashSet(StringComparer.Ordinal);

        var toRemove = currentSet.Where(role => !nextSet.Contains(role)).ToArray();
        if (toRemove.Length > 0)
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(user, toRemove);
            if (!removeResult.Succeeded)
            {
                throw new InvalidOperationException(FormatIdentityErrors(removeResult));
            }
        }

        var toAdd = nextSet.Where(role => !currentSet.Contains(role)).ToArray();
        foreach (var role in toAdd)
        {
            if (!await _roleManager.RoleExistsAsync(role))
            {
                throw new InvalidOperationException($"Роль «{role}» не існує в системі.");
            }

            var addResult = await _userManager.AddToRoleAsync(user, role);
            if (!addResult.Succeeded)
            {
                throw new InvalidOperationException(FormatIdentityErrors(addResult));
            }
        }
    }

    private async Task<IdentityResult> ResetPasswordAsync(ApplicationUser user, string password)
    {
        if (await _userManager.HasPasswordAsync(user))
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            return await _userManager.ResetPasswordAsync(user, token, password);
        }

        return await _userManager.AddPasswordAsync(user, password);
    }

    private static string FormatIdentityErrors(IdentityResult result) =>
        string.Join(" ", result.Errors.Select(error => error.Description));
}
