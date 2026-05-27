namespace UniversalLIMS.Application.Security;

public interface IPortalThemeService
{
    int GetTheme(string? role = null);

    void SetTheme(int theme, string? role = null);
}
