namespace UniversalLIMS.Application.Home;

/// <summary>Дані для шапки робочого layout (бейдж ролі, навігація).</summary>
public sealed class WorkspaceChromeViewModel
{
    public required string RoleDisplayName { get; init; }
    public required string AccentColor { get; init; }
    public required string IconClass { get; init; }
    public required IReadOnlyList<WorkspaceNavItem> NavItems { get; init; }

    public static WorkspaceChromeViewModel? TryFromRole(string? roleCode, bool isDevelopment = false)
    {
        var definition = RolePortalCatalog.FindByRoleCode(roleCode);
        if (definition is null)
        {
            return null;
        }

        return new WorkspaceChromeViewModel
        {
            RoleDisplayName = definition.DisplayName,
            AccentColor = definition.AccentColor,
            IconClass = definition.IconClass,
            NavItems = WorkspaceNavigationCatalog.GetNavItems(definition.RoleCode, isDevelopment)
        };
    }
}
