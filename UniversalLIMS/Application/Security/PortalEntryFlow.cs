using System.Security.Claims;

namespace UniversalLIMS.Application.Security;

/// <summary>Логіка входу на портал / workspace (для контролера та тестів).</summary>
public static class PortalEntryFlow
{
    /// <summary>Єдина доступна роль для авто-переходу в workspace, інакше <c>null</c>.</summary>
    public static string? TryGetSingleAssumableRole(ClaimsPrincipal user)
    {
        var roles = LimsRoleAccess.GetAssumableRoleCodes(user);
        return roles.Count == 1 ? roles[0] : null;
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
