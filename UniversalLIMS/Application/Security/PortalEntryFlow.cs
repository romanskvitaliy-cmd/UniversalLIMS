using System.Security.Claims;

namespace UniversalLIMS.Application.Security;

/// <summary>Логіка входу на портал / workspace (для контролера та тестів).</summary>
public static class PortalEntryFlow
{
    /// <summary>Екран порталу (логотип) — для будь-якого користувача з хоча б однією доступною роллю.</summary>
    public static bool CanAccessRolePortal(ClaimsPrincipal user) =>
        CountPickableRoles(user) > 0;

    /// <summary>Скільки ролей можна обрати на порталі (адмін — усі чотири).</summary>
    public static int CountPickableRoles(ClaimsPrincipal user)
    {
        if (user.IsInRole(LimsRoles.SystemAdministrator))
        {
            return LimsRoles.All.Length;
        }

        return LimsRoles.All.Count(user.IsInRole);
    }

    public static bool CanSwitchRole(ClaimsPrincipal user) =>
        CountPickableRoles(user) > 1;

    /// <summary>Стартова сторінка після логіну: портал для адміна, кабінет для інших ролей.</summary>
    public static string GetDefaultLandingPath(ClaimsPrincipal user) =>
        user.IsInRole(LimsRoles.SystemAdministrator) ? "/" : "/Home/Workspace";

    /// <summary>Єдина доступна роль для авто-переходу в workspace, інакше <c>null</c>.</summary>
    public static string? TryGetSingleAssumableRole(ClaimsPrincipal user)
    {
        var roles = LimsRoleAccess.GetAssumableRoleCodes(user);
        return roles.Count == 1 ? roles[0] : null;
    }

    /// <summary>Роль для негайного входу в кабінет (одна роль або перша з доступних).</summary>
    public static string? ResolveWorkspaceEntryRole(ClaimsPrincipal user)
    {
        var single = TryGetSingleAssumableRole(user);
        if (!string.IsNullOrEmpty(single))
        {
            return single;
        }

        var assumable = LimsRoleAccess.GetAssumableRoleCodes(user);
        return assumable.Count > 0 ? assumable[0] : null;
    }

    public static bool ShouldAutoRedirectToWorkspace(ClaimsPrincipal user, bool autoRedirectEnabled) =>
        autoRedirectEnabled && TryGetSingleAssumableRole(user) is not null;

    /// <summary>Після логіну Identity — завжди на портал ролей, а не на сторінки Identity.</summary>
    public static bool ShouldRedirectToPortalHome(string? redirectUri)
    {
        if (string.IsNullOrWhiteSpace(redirectUri))
        {
            return true;
        }

        if (!Uri.TryCreate(redirectUri, UriKind.RelativeOrAbsolute, out var uri))
        {
            return true;
        }

        if (uri.IsAbsoluteUri)
        {
            return true;
        }

        var path = uri.ToString();
        return path.Contains("/Identity/", StringComparison.OrdinalIgnoreCase);
    }
}
