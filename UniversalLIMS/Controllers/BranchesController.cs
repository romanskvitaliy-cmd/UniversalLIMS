using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversalLIMS.Application.Organization;
using UniversalLIMS.Application.Organization.Abstractions;
using UniversalLIMS.Application.Security;
using UniversalLIMS.Infrastructure.Filters;
using UniversalLIMS.ViewModels.Organization;

namespace UniversalLIMS.Controllers;

[Authorize(Policy = LimsPolicies.ManageSystem)]
[RequireActiveLimsRole]
public sealed class BranchesController : Controller
{
    private readonly IBranchService _branchService;
    private readonly IActiveLimsRoleService _activeLimsRole;

    public BranchesController(IBranchService branchService, IActiveLimsRoleService activeLimsRole)
    {
        _branchService = branchService;
        _activeLimsRole = activeLimsRole;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!IsActiveAdministrator())
        {
            TempData["RoleSelectError"] = "Довідник філій доступний у робочій ролі «Адміністратор».";
            return RedirectToAction("Workspace", "Home");
        }

        var branches = await _branchService.GetListAsync(cancellationToken);
        return View(new BranchIndexViewModel { Branches = branches });
    }

    [HttpGet]
    public IActionResult Create()
    {
        if (!IsActiveAdministrator())
        {
            return Forbid();
        }

        return View(new BranchCreateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BranchCreateViewModel model, CancellationToken cancellationToken)
    {
        if (!IsActiveAdministrator())
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var branchId = await _branchService.CreateAsync(
                new CreateBranchRequest
                {
                    Code = model.Code,
                    Name = model.Name,
                    City = model.City,
                    Address = model.Address
                },
                cancellationToken);

            TempData["SuccessMessage"] = "Філію створено.";
            return RedirectToAction(nameof(Edit), new { id = branchId });
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        if (!IsActiveAdministrator())
        {
            return Forbid();
        }

        var branch = await _branchService.GetForEditAsync(id, cancellationToken);
        if (branch is null)
        {
            return NotFound();
        }

        return View(MapToEditViewModel(branch));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, BranchEditViewModel model, CancellationToken cancellationToken)
    {
        if (!IsActiveAdministrator())
        {
            return Forbid();
        }

        if (id != model.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await _branchService.UpdateAsync(
                id,
                new UpdateBranchRequest
                {
                    Name = model.Name,
                    City = model.City,
                    Address = model.Address,
                    IsActive = model.IsActive
                },
                cancellationToken);

            TempData["SuccessMessage"] = "Зміни збережено.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Annul(Guid id, CancellationToken cancellationToken)
    {
        if (!IsActiveAdministrator())
        {
            return Forbid();
        }

        var branch = await _branchService.GetForEditAsync(id, cancellationToken);
        if (branch is null)
        {
            return NotFound();
        }

        return View(new AnnulBranchViewModel
        {
            Id = branch.Id,
            Code = branch.Code,
            Name = branch.Name
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Annul(Guid id, AnnulBranchViewModel model, CancellationToken cancellationToken)
    {
        if (!IsActiveAdministrator())
        {
            return Forbid();
        }

        if (id != model.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await _branchService.AnnulAsync(id, model.AnnulmentReason, cancellationToken);
            TempData["SuccessMessage"] = "Філію анульовано.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(model);
        }
    }

    private bool IsActiveAdministrator() =>
        string.Equals(
            _activeLimsRole.ResolveActiveRole(User),
            LimsRoles.SystemAdministrator,
            StringComparison.Ordinal);

    private static BranchEditViewModel MapToEditViewModel(BranchEditDto branch) =>
        new()
        {
            Id = branch.Id,
            Code = branch.Code,
            Name = branch.Name,
            City = branch.City,
            Address = branch.Address,
            IsActive = branch.IsActive
        };
}
