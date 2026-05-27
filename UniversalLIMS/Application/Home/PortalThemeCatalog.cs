namespace UniversalLIMS.Application.Home;

public sealed record PortalThemeDefinition(
    int Id,
    string DisplayName,
    string CssClass,
    string SwatchGradient);

public static class PortalThemeCatalog
{
    public static readonly IReadOnlyList<PortalThemeDefinition> All =
    [
        new(1, "Сірий", PortalThemes.ToCssClass(1),
            "linear-gradient(135deg, #9ca3af 0%, #4b5563 50%, #111827 100%)"),
        new(2, "Медичний", PortalThemes.ToCssClass(2),
            "linear-gradient(135deg, #14532d, #0f172a)"),
        new(3, "Салатовий", PortalThemes.ToCssClass(3),
            "linear-gradient(135deg, #bef264 0%, #65a30d 45%, #0f172a 100%)"),
        new(4, "Білий", PortalThemes.ToCssClass(4),
            "linear-gradient(135deg, #ffffff 0%, #f1f5f9 50%, #e2e8f0 100%)")
    ];

    public static PortalThemeDefinition Get(int theme) =>
        All.First(t => t.Id == PortalThemes.Normalize(theme));
}
