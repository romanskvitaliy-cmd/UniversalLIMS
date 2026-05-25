using System.Security.Claims;

namespace UniversalLIMS.Application.Security;

public interface IActiveLimsRoleService
{
    string? GetActiveRole();

    void SetActiveRole(string roleCode);

    void ClearActiveRole();

    /// <summary>
    /// Повертає активну роль з сесії або автоматично обирає єдину роль користувача.
    /// </summary>
    string? ResolveActiveRole(ClaimsPrincipal user);
}
