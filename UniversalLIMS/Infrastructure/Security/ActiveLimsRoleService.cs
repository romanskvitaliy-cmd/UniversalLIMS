using System.Security.Claims;
using UniversalLIMS.Application.Security;

namespace UniversalLIMS.Infrastructure.Security;

public sealed class ActiveLimsRoleService : IActiveLimsRoleService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ActiveLimsRoleService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? GetActiveRole()
    {
        if (!IsAuthenticated())
        {
            LimsPortalSessionCleanup.ClearAuthenticatedSessionState(_httpContextAccessor.HttpContext?.Session);
            return null;
        }

        var session = _httpContextAccessor.HttpContext?.Session;
        return session?.GetString(SessionKeys.ActiveLimsRole);
    }

    public void SetActiveRole(string roleCode)
    {
        if (!LimsRoles.All.Contains(roleCode, StringComparer.Ordinal))
        {
            throw new ArgumentException($"Unknown LIMS role: {roleCode}", nameof(roleCode));
        }

        var session = _httpContextAccessor.HttpContext?.Session
            ?? throw new InvalidOperationException("HTTP session is not available.");

        session.SetString(SessionKeys.ActiveLimsRole, roleCode);
    }

    public void ClearActiveRole()
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        session?.Remove(SessionKeys.ActiveLimsRole);
    }

    public string? ResolveActiveRole(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            LimsPortalSessionCleanup.ClearAuthenticatedSessionState(_httpContextAccessor.HttpContext?.Session);
            return null;
        }

        var active = GetActiveRole();
        if (!string.IsNullOrWhiteSpace(active))
        {
            if (LimsRoleAccess.CanAssumeRole(user, active))
            {
                return active;
            }

            ClearActiveRole();
        }

        var assumable = LimsRoleAccess.GetAssumableRoleCodes(user);
        if (assumable.Count == 1)
        {
            SetActiveRole(assumable[0]);
            return assumable[0];
        }

        return GetActiveRole();
    }

    private bool IsAuthenticated() =>
        _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;
}
