namespace UniversalLIMS.Application.Home;

public sealed class WorkspaceQuickLinkVm
{
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string IconClass { get; init; }
    public string? Url { get; init; }
    public bool IsAvailable { get; init; }
}
