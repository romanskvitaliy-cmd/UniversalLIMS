namespace UniversalLIMS.Application.Home;

public sealed class RolePortalCardVm
{
    public required string RoleCode { get; init; }
    public required string DisplayName { get; init; }
    public required string AccentColor { get; init; }
    public required string AccentRgb { get; init; }
    public required string IconClass { get; init; }
    public required string Description { get; init; }
    public bool IsGranted { get; init; }
    /// <summary>Ця роль зараз у сесії (ActiveLimsRole).</summary>
    public bool IsActiveRole { get; init; }
    public int AnimationDelayMs { get; init; }
}
