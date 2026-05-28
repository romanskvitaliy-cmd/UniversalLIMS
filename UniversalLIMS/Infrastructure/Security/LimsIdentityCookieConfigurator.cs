using UniversalLIMS.Application.Security;

namespace UniversalLIMS.Infrastructure.Security;

/// <summary>Після логіну — редірект на портал ролей (/), а не на сторінки Identity.</summary>
public static class LimsIdentityCookieConfigurator
{
    public static void ConfigureLimsIdentityCookies(IServiceCollection services)
    {
        services.ConfigureApplicationCookie(options =>
        {
            options.Events.OnSigningOut = context =>
            {
                LimsPortalSessionCleanup.ClearAuthenticatedSessionState(context.HttpContext.Session);
                return Task.CompletedTask;
            };

            options.Events.OnRedirectToReturnUrl = context =>
            {
                if (PortalEntryFlow.ShouldRedirectToPortalHome(context.RedirectUri))
                {
                    context.Response.Redirect(
                        PortalEntryFlow.GetDefaultLandingPath(context.HttpContext.User));
                }
                else
                {
                    context.Response.Redirect(context.RedirectUri);
                }

                return Task.CompletedTask;
            };
        });
    }
}
