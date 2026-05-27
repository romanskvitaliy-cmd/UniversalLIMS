using UniversalLIMS.Application.Home;
using UniversalLIMS.Application.Security;
using System.Text.Json;

namespace UniversalLIMS.Infrastructure.Security;

public sealed class PortalThemeService : IPortalThemeService
{
    private static readonly JsonSerializerOptions ThemeJsonOptions = new(JsonSerializerDefaults.Web);
    private const string ThemeCookieName = "ulims.portal.theme";
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PortalThemeService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public int GetTheme(string? role = null)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return PortalThemes.Default;
        }

        var session = httpContext.Session;
        var roleKey = NormalizeRoleKey(role);
        if (session is not null && !string.IsNullOrEmpty(roleKey))
        {
            var perRoleThemes = ReadPerRoleThemeMap(session.GetString(SessionKeys.PortalThemeByRole));
            if (perRoleThemes.TryGetValue(roleKey, out var roleTheme))
            {
                return PortalThemes.Normalize(roleTheme);
            }
        }

        if (session is not null)
        {
            var stored = session.GetString(SessionKeys.PortalTheme);
            if (int.TryParse(stored, out var sessionTheme))
            {
                return PortalThemes.Normalize(sessionTheme);
            }
        }

        var cookieTheme = httpContext.Request.Cookies[ThemeCookieName];
        return int.TryParse(cookieTheme, out var theme) ? PortalThemes.Normalize(theme) : PortalThemes.Default;
    }

    public void SetTheme(int theme, string? role = null)
    {
        if (!PortalThemes.IsValid(theme))
        {
            throw new ArgumentException($"Unknown portal theme: {theme}", nameof(theme));
        }

        var httpContext = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("HTTP context is not available.");

        var normalizedTheme = PortalThemes.Normalize(theme);
        var roleKey = NormalizeRoleKey(role);
        var session = httpContext.Session;
        if (session is not null && !string.IsNullOrEmpty(roleKey))
        {
            var perRoleThemes = ReadPerRoleThemeMap(session.GetString(SessionKeys.PortalThemeByRole));
            perRoleThemes[roleKey] = normalizedTheme;
            session.SetString(SessionKeys.PortalThemeByRole, JsonSerializer.Serialize(perRoleThemes, ThemeJsonOptions));
        }

        if (session is not null)
        {
            session.SetString(SessionKeys.PortalTheme, normalizedTheme.ToString());
        }

        httpContext.Response.Cookies.Append(
            ThemeCookieName,
            normalizedTheme.ToString(),
            new CookieOptions
            {
                HttpOnly = false,
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddDays(90)
            });
    }

    private static string? NormalizeRoleKey(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return null;
        }

        return role.Trim().ToLowerInvariant();
    }

    private static Dictionary<string, int> ReadPerRoleThemeMap(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return new Dictionary<string, int>(StringComparer.Ordinal);
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, int>>(rawJson, ThemeJsonOptions);
            if (parsed is null)
            {
                return new Dictionary<string, int>(StringComparer.Ordinal);
            }

            return parsed
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Key) && PortalThemes.IsValid(entry.Value))
                .ToDictionary(
                    entry => entry.Key.Trim().ToLowerInvariant(),
                    entry => PortalThemes.Normalize(entry.Value),
                    StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return new Dictionary<string, int>(StringComparer.Ordinal);
        }
    }
}
