using System.Security.Claims;

namespace UniversalLIMS.Application.Security;

/// <summary>
/// Правила доступу до робочих ролей на порталі та в сесії.
/// </summary>
public static class LimsRoleAccess
{
    /// <summary>
    /// Користувач може обрати роль, якщо вона призначена в Identity або він системний адміністратор.
    /// </summary>
    public static bool CanAssumeRole(ClaimsPrincipal user, string roleCode)
    {
        if (string.IsNullOrWhiteSpace(roleCode) || !LimsRoles.All.Contains(roleCode, StringComparer.Ordinal))
        {
            return false;
        }

        return user.IsInRole(roleCode) || user.IsInRole(LimsRoles.SystemAdministrator);
    }

    /// <summary>Ролі, які користувач може обрати на порталі (включно з усіма для адміністратора).</summary>
    public static IReadOnlyList<string> GetAssumableRoleCodes(ClaimsPrincipal user) =>
        LimsRoles.All.Where(role => CanAssumeRole(user, role)).ToArray();
}
