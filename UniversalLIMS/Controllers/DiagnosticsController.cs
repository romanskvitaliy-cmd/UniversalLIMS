using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Security;
using UniversalLIMS.Domain.Identity;
using UniversalLIMS.Domain.Templates;
using UniversalLIMS.Infrastructure.Persistence;
using UniversalLIMS.ViewModels.Diagnostics;

namespace UniversalLIMS.Controllers;

public sealed class DiagnosticsController : Controller
{
    private const string DiagnosticDataFieldPrefix = "Diagnostics.Foundation.";

    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;

    public DiagnosticsController(
        ApplicationDbContext context,
        IWebHostEnvironment environment,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager)
    {
        _context = context;
        _environment = environment;
        _userManager = userManager;
        _signInManager = signInManager;
    }

    public async Task<IActionResult> Foundation(CancellationToken cancellationToken)
    {
        if (!IsDevelopment())
        {
            return NotFound();
        }

        var appliedMigrations = await _context.Database.GetAppliedMigrationsAsync(cancellationToken);
        var pendingMigrations = await _context.Database.GetPendingMigrationsAsync(cancellationToken);

        var model = new FoundationStatusViewModel
        {
            RoleCount = await _context.Roles.CountAsync(cancellationToken),
            BranchCount = await _context.Branches.IgnoreQueryFilters().CountAsync(cancellationToken),
            DataFieldCount = await _context.DataFields.IgnoreQueryFilters().CountAsync(cancellationToken),
            AuditLogCount = await _context.AuditLogs.CountAsync(cancellationToken),
            ActiveDiagnosticDataFieldCount = await _context.DataFields
                .CountAsync(dataField => dataField.Key.StartsWith(DiagnosticDataFieldPrefix), cancellationToken),
            AnnulledDiagnosticDataFieldCount = await _context.DataFields
                .IgnoreQueryFilters()
                .CountAsync(dataField =>
                    dataField.Key.StartsWith(DiagnosticDataFieldPrefix) && dataField.IsAnnulled,
                    cancellationToken),
            LatestDiagnosticDataFieldKey = await _context.DataFields
                .IgnoreQueryFilters()
                .Where(dataField => dataField.Key.StartsWith(DiagnosticDataFieldPrefix))
                .OrderByDescending(dataField => dataField.CreatedAtUtc)
                .Select(dataField => dataField.Key)
                .FirstOrDefaultAsync(cancellationToken),
            AppliedMigrations = appliedMigrations.ToList(),
            PendingMigrations = pendingMigrations.ToList()
        };

        return View(model);
    }

    public async Task<IActionResult> TemplateConstructor(CancellationToken cancellationToken)
    {
        if (!IsDevelopment())
        {
            return NotFound();
        }

        var appliedMigrations = await _context.Database.GetAppliedMigrationsAsync(cancellationToken);
        var pendingMigrations = await _context.Database.GetPendingMigrationsAsync(cancellationToken);

        var model = new TemplateConstructorStatusViewModel
        {
            TemplateCount = await _context.Templates.IgnoreQueryFilters().CountAsync(cancellationToken),
            TemplateVersionCount = await _context.TemplateVersions.IgnoreQueryFilters().CountAsync(cancellationToken),
            PublishedTemplateVersionCount = await _context.TemplateVersions
                .IgnoreQueryFilters()
                .CountAsync(version => version.Status == TemplateVersionStatus.Published, cancellationToken),
            AnnulledTemplateVersionCount = await _context.TemplateVersions
                .IgnoreQueryFilters()
                .CountAsync(version => version.IsAnnulled, cancellationToken),
            TemplateFieldCount = await _context.TemplateFields.IgnoreQueryFilters().CountAsync(cancellationToken),
            UnmappedRequiredFieldCount = await _context.TemplateFields
                .CountAsync(field => field.IsRequired && field.DataFieldId == null, cancellationToken),
            TemplateFieldPermissionCount = await _context.TemplateFieldPermissions.IgnoreQueryFilters().CountAsync(cancellationToken),
            TemplatesWithoutPublishedVersionCount = await _context.Templates
                .CountAsync(template => template.CurrentPublishedVersionId == null, cancellationToken),
            LatestTemplateCode = await _context.Templates
                .IgnoreQueryFilters()
                .OrderByDescending(template => template.CreatedAtUtc)
                .Select(template => template.Code)
                .FirstOrDefaultAsync(cancellationToken),
            AppliedMigrations = appliedMigrations.ToList(),
            PendingMigrations = pendingMigrations.ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateDiagnosticDataField(CancellationToken cancellationToken)
    {
        if (!IsDevelopment())
        {
            return NotFound();
        }

        var suffix = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        var dataField = new DataField
        {
            Key = $"{DiagnosticDataFieldPrefix}{suffix}",
            DisplayNameUk = $"Тестове поле фундаменту {suffix}",
            DescriptionUk = "Службове dev-only поле для перевірки Audit Trail.",
            FieldType = DataFieldType.Text,
            Scope = DataFieldScope.System,
            IsRequired = false,
            IsSystem = false,
            IsActive = true
        };

        _context.DataFields.Add(dataField);
        await _context.SaveChangesAsync(cancellationToken);

        TempData["DiagnosticsMessage"] = $"Створено тестове поле: {dataField.Key}";
        return RedirectToAction(nameof(Foundation));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AnnulLatestDiagnosticDataField(CancellationToken cancellationToken)
    {
        if (!IsDevelopment())
        {
            return NotFound();
        }

        var dataField = await _context.DataFields
            .Where(field => field.Key.StartsWith(DiagnosticDataFieldPrefix))
            .OrderByDescending(field => field.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (dataField is null)
        {
            TempData["DiagnosticsMessage"] = "Немає активного тестового поля для анулювання.";
            return RedirectToAction(nameof(Foundation));
        }

        dataField.AnnulmentReason = "Dev-only перевірка SoftAnnulmentSaveChangesInterceptor.";
        _context.DataFields.Remove(dataField);
        await _context.SaveChangesAsync(cancellationToken);

        TempData["DiagnosticsMessage"] = $"Анульовано тестове поле: {dataField.Key}";
        return RedirectToAction(nameof(Foundation));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PromoteCurrentUserToSystemAdministrator(CancellationToken cancellationToken)
    {
        if (!IsDevelopment())
        {
            return NotFound();
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            TempData["DiagnosticsMessage"] = "Спочатку увійдіть у систему.";
            return RedirectToAction(nameof(Foundation));
        }

        if (!await _userManager.IsInRoleAsync(user, LimsRoles.SystemAdministrator))
        {
            var result = await _userManager.AddToRoleAsync(user, LimsRoles.SystemAdministrator);
            if (!result.Succeeded)
            {
                var errors = string.Join("; ", result.Errors.Select(error => error.Description));
                TempData["DiagnosticsMessage"] = $"Не вдалося надати роль SystemAdministrator: {errors}";
                return RedirectToAction(nameof(Foundation));
            }
        }

        await _signInManager.RefreshSignInAsync(user);
        TempData["DiagnosticsMessage"] = $"Користувачу {user.Email} надано роль SystemAdministrator. Sign-in оновлено.";

        return RedirectToAction(nameof(Foundation));
    }

    private bool IsDevelopment()
    {
        return _environment.IsDevelopment();
    }
}
