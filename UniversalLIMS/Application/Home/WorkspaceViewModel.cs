namespace UniversalLIMS.Application.Home;

public sealed class WorkspaceViewModel
{
    public required string RoleCode { get; init; }
    public required string RoleDisplayName { get; init; }
    public required string AccentColor { get; init; }
    public required string IconClass { get; init; }
    public required string UserDisplayName { get; init; }
    public required IReadOnlyList<WorkspaceNavItem> NavItems { get; init; }
    public required IReadOnlyList<WorkspaceQuickLinkVm> QuickLinks { get; init; }

    /// <summary>Development: розширений доступ до модулів для демонстрації замовнику.</summary>
    public bool IsDemoMode { get; init; }
}
