using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Application.Home;
using UniversalLIMS.Application.Security;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Infrastructure.Persistence;
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
    private readonly ApplicationDbContext _context;
    private readonly LimsPortalOptions _portalOptions;

    public HomeController(
        ILogger<HomeController> logger,
        ICurrentUserService currentUser,
        IActiveLimsRoleService activeLimsRole,
        IPortalThemeService portalTheme,
        IDateTimeProvider dateTime,
        IWebHostEnvironment environment,
        ApplicationDbContext context,
        IOptions<LimsPortalOptions> portalOptions)
    {
        _logger = logger;
        _currentUser = currentUser;
        _activeLimsRole = activeLimsRole;
        _portalTheme = portalTheme;
        _dateTime = dateTime;
        _environment = environment;
        _context = context;
        _portalOptions = portalOptions.Value;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated == true && !PortalEntryFlow.CanAccessRolePortal(User))
        {
            return RedirectToAction(nameof(Workspace));
        }

        var activeRole = _activeLimsRole.ResolveActiveRole(User);
        var model = RolePortalViewModelBuilder.Build(
            User,
            _currentUser,
            _dateTime.UtcNow.ToLocalTime(),
            activeRole,
            _portalTheme.GetTheme(activeRole));

        return View(model);
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult SetPortalTheme(int theme, string? returnUrl = null) =>
        ApplyPortalTheme(theme, returnUrl);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SetPortalThemePost(int theme, string? returnUrl = null) =>
        ApplyPortalTheme(theme, returnUrl);

    private IActionResult ApplyPortalTheme(int theme, string? returnUrl)
    {
        if (!PortalThemes.IsValid(theme))
        {
            return BadRequest();
        }

        _portalTheme.SetTheme(theme, _activeLimsRole.ResolveActiveRole(User));

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Workspace));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SelectRole(string role)
    {
        if (!PortalEntryFlow.CanAccessRolePortal(User))
        {
            return Forbid();
        }

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
    public async Task<IActionResult> Workspace(CancellationToken cancellationToken)
    {
        var activeRole = _activeLimsRole.ResolveActiveRole(User);
        if (string.IsNullOrWhiteSpace(activeRole))
        {
            if (PortalEntryFlow.CanAccessRolePortal(User))
            {
                return RedirectToAction(nameof(Index));
            }

            return RedirectToWorkspaceEntry();
        }

        var model = WorkspaceViewModelBuilder.TryBuild(activeRole, _currentUser, _environment.IsDevelopment());
        if (model is null)
        {
            _activeLimsRole.ClearActiveRole();
            return RedirectToAction(nameof(Index));
        }

        if (activeRole == LimsRoles.Registrar)
        {
            model.Metrics = await BuildRegistrarMetricsAsync(cancellationToken);
        }
        else if (activeRole == LimsRoles.SystemAdministrator)
        {
            model.Metrics = await BuildAdministratorMetricsAsync(cancellationToken);
        }

        return View(model);
    }

    private async Task<IReadOnlyList<WorkspaceMetricVm>> BuildAdministratorMetricsAsync(
        CancellationToken cancellationToken)
    {
        var branches = await _context.Branches
            .AsNoTracking()
            .CountAsync(branch => branch.IsActive && !branch.IsAnnulled, cancellationToken);

        var activeSamples = await _context.Samples
            .AsNoTracking()
            .Where(sample =>
                !sample.IsAnnulled
                && !sample.Order.IsAnnulled
                && sample.OrderDocuments.Any(document =>
                    !document.IsAnnulled
                    && document.Status != OrderDocumentStatus.Pending))
            .CountAsync(cancellationToken);

        var inProgressSamples = await _context.Samples
            .AsNoTracking()
            .Where(sample =>
                !sample.IsAnnulled
                && sample.Status == SampleStatus.InProgress)
            .CountAsync(cancellationToken);

        return
        [
            new WorkspaceMetricVm
            {
                Label = "Лабораторії",
                Value = branches.ToString("N0"),
                Description = "активних філій",
                IconClass = "bi-building"
            },
            new WorkspaceMetricVm
            {
                Label = "У лабораторії",
                Value = activeSamples.ToString("N0"),
                Description = "проб у workflow",
                IconClass = "bi-droplet-half"
            },
            new WorkspaceMetricVm
            {
                Label = "В роботі",
                Value = inProgressSamples.ToString("N0"),
                Description = "проб зараз",
                IconClass = "bi-activity"
            }
        ];
    }

    private async Task<IReadOnlyList<WorkspaceMetricVm>> BuildRegistrarMetricsAsync(CancellationToken cancellationToken)
    {
        var localToday = _dateTime.UtcNow.ToLocalTime().Date;
        var todayStartUtc = DateTime.SpecifyKind(localToday, DateTimeKind.Local).ToUniversalTime();
        var tomorrowStartUtc = todayStartUtc.AddDays(1);

        var orders = _context.Orders
            .AsNoTracking()
            .Where(order => !order.IsAnnulled);

        if (_currentUser.BranchId is Guid branchId)
        {
            orders = orders.Where(order => order.BranchId == branchId);
        }

        var acceptedToday = await orders.CountAsync(
            order => (order.RegisteredAtUtc ?? order.CreatedAtUtc) >= todayStartUtc
                && (order.RegisteredAtUtc ?? order.CreatedAtUtc) < tomorrowStartUtc,
            cancellationToken);

        var documents = _context.OrderDocuments
            .AsNoTracking()
            .Where(document => !document.IsAnnulled && document.Status == OrderDocumentStatus.Pending);

        if (_currentUser.BranchId is Guid documentBranchId)
        {
            documents = documents.Where(document => document.Order.BranchId == documentBranchId);
        }

        var pendingDocuments = await documents.CountAsync(cancellationToken);

        var readyForPickupQuery = _context.Samples
            .AsNoTracking()
            .Where(sample =>
                !sample.IsAnnulled
                && !sample.Order.IsAnnulled
                && sample.DeliveryStatus == Domain.Registration.SampleDeliveryStatus.ReadyForPickup
                && _context.ExpertConclusionReviews.Any(review =>
                    review.SampleId == sample.Id
                    && review.Status == Domain.Laboratory.ExpertConclusionStatus.Approved));

        if (_currentUser.BranchId is Guid pickupBranchId)
        {
            readyForPickupQuery = readyForPickupQuery.Where(sample => sample.Order.BranchId == pickupBranchId);
        }

        var readyForPickup = await readyForPickupQuery.CountAsync(cancellationToken);

        var ordersInWorkflow = await orders.CountAsync(
            order => order.OrderDocuments.Any(document =>
                !document.IsAnnulled && document.Status != OrderDocumentStatus.ResultsEntered),
            cancellationToken);

        return
        [
            new WorkspaceMetricVm
            {
                Label = "Сьогодні прийнято",
                Value = acceptedToday.ToString("N0"),
                Description = "зареєстрованих проб",
                IconClass = "bi-calendar2-check"
            },
            new WorkspaceMetricVm
            {
                Label = "Очікують передачі",
                Value = pendingDocuments.ToString("N0"),
                Description = "PDF-направлень",
                IconClass = "bi-hourglass-split"
            },
            new WorkspaceMetricVm
            {
                Label = "Готово до видачі",
                Value = readyForPickup.ToString("N0"),
                Description = "затверджених проб",
                IconClass = "bi-box-arrow-up-right"
            },
            new WorkspaceMetricVm
            {
                Label = "В роботі",
                Value = ordersInWorkflow.ToString("N0"),
                Description = "активних справ",
                IconClass = "bi-activity"
            }
        ];
    }

    [HttpGet]
    public IActionResult ClearRole()
    {
        _activeLimsRole.ClearActiveRole();

        if (!PortalEntryFlow.CanAccessRolePortal(User))
        {
            return RedirectToWorkspaceEntry();
        }

        return RedirectToAction(nameof(Index));
    }

    private IActionResult RedirectToWorkspaceEntry()
    {
        var role = PortalEntryFlow.ResolveWorkspaceEntryRole(User);
        if (string.IsNullOrWhiteSpace(role))
        {
            return Forbid();
        }

        _activeLimsRole.SetActiveRole(role);
        return RedirectToAction(nameof(Workspace));
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
