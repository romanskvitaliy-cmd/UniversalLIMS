namespace UniversalLIMS.Application.Home;

public sealed class RolePortalViewModel
{
    public required string GreetingText { get; init; }
    public required string UserDisplayName { get; init; }
    public required string FormattedDate { get; init; }
    public required IReadOnlyList<RolePortalCardVm> Cards { get; init; }
    /// <summary>У сесії збережено ActiveLimsRole — показуємо лише «Продовжити» на активній картці.</summary>
    public bool HasActiveRole { get; init; }
    /// <summary>Користувач може обрати іншу роль (більше однієї доступної).</summary>
    public bool CanSwitchRole { get; init; }
    public required int BackgroundVariant { get; init; }
    public required string ThemeCssClass { get; init; }
    public required IReadOnlyList<PortalThemeOptionVm> ThemeOptions { get; init; }
}
