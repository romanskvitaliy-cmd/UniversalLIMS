using UniversalLIMS.Application.Home;
using UniversalLIMS.Application.Security;

namespace UniversalLIMS.Infrastructure.Security;

public sealed class PortalThemeService : IPortalThemeService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PortalThemeService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public int GetTheme()
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        if (session is null)
        {
            return PortalThemes.Default;
        }

        var stored = session.GetString(SessionKeys.PortalTheme);
        return int.TryParse(stored, out var theme) ? PortalThemes.Normalize(theme) : PortalThemes.Default;
    }

    public void SetTheme(int theme)
    {
        if (!PortalThemes.IsValid(theme))
        {
            throw new ArgumentException($"Unknown portal theme: {theme}", nameof(theme));
        }

        var session = _httpContextAccessor.HttpContext?.Session
            ?? throw new InvalidOperationException("HTTP session is not available.");

        session.SetString(SessionKeys.PortalTheme, theme.ToString());
    }
}
