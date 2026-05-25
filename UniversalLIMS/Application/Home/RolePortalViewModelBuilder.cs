using System.Globalization;
using System.Security.Claims;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Application.Security;

namespace UniversalLIMS.Application.Home;

public static class RolePortalViewModelBuilder
{
    private static readonly CultureInfo UkrainianCulture = CultureInfo.GetCultureInfo("uk-UA");

    public static RolePortalViewModel Build(
        ClaimsPrincipal user,
        ICurrentUserService currentUser,
        DateTime now,
        string? activeRoleCode,
        int portalTheme)
    {
        var hasActiveRole = !string.IsNullOrEmpty(activeRoleCode);
        var cards = new List<RolePortalCardVm>();
        var delayIndex = 0;

        foreach (var definition in RolePortalCatalog.All)
        {
            var isGranted = LimsRoleAccess.CanAssumeRole(user, definition.RoleCode);
            cards.Add(new RolePortalCardVm
            {
                RoleCode = definition.RoleCode,
                DisplayName = definition.DisplayName,
                AccentColor = definition.AccentColor,
                AccentRgb = definition.AccentRgb,
                IconClass = definition.IconClass,
                Description = definition.Description,
                IsGranted = isGranted,
                IsActiveRole = hasActiveRole
                    && string.Equals(activeRoleCode, definition.RoleCode, StringComparison.Ordinal),
                AnimationDelayMs = delayIndex * 100
            });
            delayIndex++;
        }

        var theme = PortalThemes.Normalize(portalTheme);
        var themeOptions = PortalThemeCatalog.All
            .Select(t => new PortalThemeOptionVm
            {
                Id = t.Id,
                DisplayName = t.DisplayName,
                CssClass = t.CssClass,
                SwatchGradient = t.SwatchGradient,
                IsSelected = t.Id == theme
            })
            .ToList();

        return new RolePortalViewModel
        {
            GreetingText = GetGreeting(now),
            UserDisplayName = currentUser.UserFullName ?? user.Identity?.Name ?? "користувач",
            FormattedDate = now.ToString("d MMMM yyyy", UkrainianCulture),
            Cards = cards,
            HasActiveRole = hasActiveRole,
            BackgroundVariant = theme,
            ThemeCssClass = PortalThemes.ToCssClass(theme),
            ThemeOptions = themeOptions
        };
    }

    private static string GetGreeting(DateTime now)
    {
        var hour = now.Hour;
        if (hour >= 18 || hour < 5)
        {
            return "Добрий вечір";
        }

        return "Добрий день";
    }
}
