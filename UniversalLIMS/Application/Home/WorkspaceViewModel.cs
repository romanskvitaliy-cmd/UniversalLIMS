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
    public IReadOnlyList<WorkspaceMetricVm> Metrics { get; set; } = [];
}

public sealed class WorkspaceMetricVm
{
    public required string Label { get; init; }

    public required string Value { get; init; }

    public required string Description { get; init; }

    public required string IconClass { get; init; }
}
