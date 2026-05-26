using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversalLIMS.Application.Laboratory.Abstractions;
using UniversalLIMS.Application.Security;
using UniversalLIMS.Infrastructure.Filters;
using UniversalLIMS.ViewModels.Laboratory;

namespace UniversalLIMS.Controllers;

[Authorize(Policy = LimsPolicies.ManageSystem)]
[RequireActiveLimsRole]
public sealed class LaboratoriesController : Controller
{
    private readonly ILaboratoryOverviewService _overview;
    private readonly ILaboratoryBranchContext _laboratoryBranchContext;
    private readonly IActiveLimsRoleService _activeLimsRole;

    public LaboratoriesController(
        ILaboratoryOverviewService overview,
        ILaboratoryBranchContext laboratoryBranchContext,
        IActiveLimsRoleService activeLimsRole)
    {
        _overview = overview;
        _laboratoryBranchContext = laboratoryBranchContext;
        _activeLimsRole = activeLimsRole;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!IsActiveAdministrator())
        {
            TempData["RoleSelectError"] = "Огляд лабораторій доступний у робочій ролі «Адміністратор».";
            return RedirectToAction("Workspace", "Home");
        }

        var overview = await _overview.GetOverviewAsync(cancellationToken);

        return View(new LaboratoryOverviewViewModel
        {
            Overview = overview
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnterJournal(Guid? branchId, CancellationToken cancellationToken)
    {
        if (!IsActiveAdministrator())
        {
            return Forbid();
        }

        await _laboratoryBranchContext.SetSelectedBranchAsync(branchId, cancellationToken);

        return RedirectToAction("Index", "Laboratory");
    }

    private bool IsActiveAdministrator() =>
        string.Equals(
            _activeLimsRole.ResolveActiveRole(User),
            LimsRoles.SystemAdministrator,
            StringComparison.Ordinal);
}
