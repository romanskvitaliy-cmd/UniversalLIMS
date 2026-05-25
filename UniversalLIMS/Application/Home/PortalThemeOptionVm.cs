namespace UniversalLIMS.Application.Home;

public sealed class PortalThemeOptionVm
{
    public required int Id { get; init; }
    public required string DisplayName { get; init; }
    public required string CssClass { get; init; }
    public required string SwatchGradient { get; init; }
    public bool IsSelected { get; init; }
}
