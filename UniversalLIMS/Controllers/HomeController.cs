using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Application.Home;
using UniversalLIMS.Application.Security;
using UniversalLIMS.Models;

namespace UniversalLIMS.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ICurrentUserService _currentUser;
    private readonly IActiveLimsRoleService _activeLimsRole;
    private readonly IPortalThemeService _portalTheme;
    private readonly IDateTimeProvider _dateTime;
    private readonly IWebHostEnvironment _environment;
    private readonly LimsPortalOptions _portalOptions;

    public HomeController(
        ILogger<HomeController> logger,
        ICurrentUserService currentUser,
        IActiveLimsRoleService activeLimsRole,
        IPortalThemeService portalTheme,
        IDateTimeProvider dateTime,
        IWebHostEnvironment environment,
        IOptions<LimsPortalOptions> portalOptions)
    {
        _logger = logger;
        _currentUser = currentUser;
        _activeLimsRole = activeLimsRole;
        _portalTheme = portalTheme;
        _dateTime = dateTime;
        _environment = environment;
        _portalOptions = portalOptions.Value;
    }

    [HttpGet]
    public IActionResult Index()
    {
        if (PortalEntryFlow.ShouldAutoRedirectToWorkspace(User, _portalOptions.AutoRedirectSingleRole))
        {
            var singleRole = PortalEntryFlow.TryGetSingleAssumableRole(User)!;
            _activeLimsRole.SetActiveRole(singleRole);
            return RedirectToAction(nameof(Workspace));
        }

        var model = RolePortalViewModelBuilder.Build(
            User,
            _currentUser,
            _dateTime.UtcNow.ToLocalTime(),
            _activeLimsRole.GetActiveRole(),
            _portalTheme.GetTheme());

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SetPortalTheme(int theme)
    {
        if (!PortalThemes.IsValid(theme))
        {
            return BadRequest();
        }

        _portalTheme.SetTheme(theme);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SelectRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role) || !LimsRoles.All.Contains(role, StringComparer.Ordinal))
        {
            return BadRequest();
        }

        if (!LimsRoleAccess.CanAssumeRole(User, role))
        {
            TempData["RoleSelectError"] = "Вам не призначено цю роль.";
            return RedirectToAction(nameof(Index));
        }

        _activeLimsRole.SetActiveRole(role);
        return RedirectToAction(nameof(Workspace));
    }

    [HttpGet]
    public IActionResult Workspace()
    {
        var activeRole = _activeLimsRole.ResolveActiveRole(User);
        if (string.IsNullOrWhiteSpace(activeRole))
        {
            return RedirectToAction(nameof(Index));
        }

        var model = WorkspaceViewModelBuilder.TryBuild(activeRole, _currentUser, _environment.IsDevelopment());
        if (model is null)
        {
            _activeLimsRole.ClearActiveRole();
            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    [HttpGet]
    public IActionResult ClearRole()
    {
        _activeLimsRole.ClearActiveRole();
        return RedirectToAction(nameof(Index));
    }

    [AllowAnonymous]
    public IActionResult Privacy()
    {
        return View();
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
