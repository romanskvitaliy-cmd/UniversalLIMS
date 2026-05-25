namespace UniversalLIMS.Application.Security;

public interface IPortalThemeService
{
    int GetTheme();

    void SetTheme(int theme);
}
