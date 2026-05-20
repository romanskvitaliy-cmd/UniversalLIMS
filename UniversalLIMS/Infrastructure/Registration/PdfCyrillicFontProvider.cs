namespace UniversalLIMS.Infrastructure.Registration;

/// <summary>
/// Resolves a TrueType font with Cyrillic support for PDF overlay rendering.
/// </summary>
internal static class PdfCyrillicFontProvider
{
    private static readonly string[] RelativeFontPaths =
    [
        Path.Combine("wwwroot", "fonts", "arial.ttf"),
        Path.Combine("wwwroot", "fonts", "Arial.ttf")
    ];

    private static readonly string[] SystemFontPaths =
    [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "Arial.ttf"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeui.ttf"),
        "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
        "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf"
    ];

    public static string ResolveFontPath()
    {
        foreach (var relativePath in RelativeFontPaths)
        {
            var fromBase = Path.Combine(AppContext.BaseDirectory, relativePath);
            if (File.Exists(fromBase))
            {
                return fromBase;
            }

            var fromCurrent = Path.Combine(Directory.GetCurrentDirectory(), relativePath);
            if (File.Exists(fromCurrent))
            {
                return fromCurrent;
            }
        }

        foreach (var systemPath in SystemFontPaths)
        {
            if (File.Exists(systemPath))
            {
                return systemPath;
            }
        }

        throw new InvalidOperationException(
            "Не знайдено TrueType-шрифт із підтримкою кирилиці. " +
            "Додайте wwwroot/fonts/arial.ttf або встановіть системний Arial/DejaVu.");
    }
}
