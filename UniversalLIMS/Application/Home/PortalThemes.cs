namespace UniversalLIMS.Application.Home;

public static class PortalThemes
{
    public const int Default = 1;

    public static readonly int[] All = [1, 2, 3, 4];

    public static bool IsValid(int theme) => theme is >= 1 and <= 4;

    public static int Normalize(int? theme) =>
        theme is int value && IsValid(value) ? value : Default;

    public static string ToCssClass(int theme) => $"portal-theme-{Normalize(theme)}";
}
