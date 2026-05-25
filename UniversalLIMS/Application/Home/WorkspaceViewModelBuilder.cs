using UniversalLIMS.Application.Abstractions;

namespace UniversalLIMS.Application.Home;

public static class WorkspaceViewModelBuilder
{
    public static WorkspaceViewModel? TryBuild(string? activeRoleCode, ICurrentUserService currentUser, bool isDevelopment = false)
    {
        var definition = RolePortalCatalog.FindByRoleCode(activeRoleCode);
        if (definition is null)
        {
            return null;
        }

        return new WorkspaceViewModel
        {
            RoleCode = definition.RoleCode,
            RoleDisplayName = definition.DisplayName,
            AccentColor = definition.AccentColor,
            IconClass = definition.IconClass,
            UserDisplayName = currentUser.UserFullName ?? "користувач",
            NavItems = WorkspaceNavigationCatalog.GetNavItems(definition.RoleCode, isDevelopment),
            QuickLinks = WorkspaceNavigationCatalog.GetQuickLinks(definition.RoleCode)
        };
    }
}
