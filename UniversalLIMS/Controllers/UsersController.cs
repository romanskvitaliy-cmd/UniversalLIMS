using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Home;
using UniversalLIMS.Application.Identity;
using UniversalLIMS.Application.Identity.Abstractions;
using UniversalLIMS.Application.Registration;
using UniversalLIMS.Application.Security;
using UniversalLIMS.Infrastructure.Filters;
using UniversalLIMS.Infrastructure.Persistence;
using UniversalLIMS.ViewModels.Identity;

namespace UniversalLIMS.Controllers;

[Authorize(Policy = LimsPolicies.ManageSystem)]
[RequireActiveLimsRole]
public sealed class UsersController : Controller
{
    private readonly IUserManagementService _userManagement;
    private readonly ApplicationDbContext _context;
    private readonly IActiveLimsRoleService _activeLimsRole;

    public UsersController(
        IUserManagementService userManagement,
        ApplicationDbContext context,
        IActiveLimsRoleService activeLimsRole)
    {
        _userManagement = userManagement;
        _context = context;
        _activeLimsRole = activeLimsRole;
    }

    [HttpGet]
    public async Task<IActionResult> Index(UserListFiltersViewModel filters, CancellationToken cancellationToken)
    {
        if (!IsActiveAdministrator())
        {
            TempData["RoleSelectError"] = "Керування користувачами доступне у робочій ролі «Адміністратор».";
            return RedirectToAction("Workspace", "Home");
        }

        var users = await _userManagement.GetListAsync(
            new UserListQuery
            {
                Search = filters.Search,
                BranchId = filters.BranchId,
                Role = filters.Role,
                IncludeInactive = filters.IncludeInactive
            },
            cancellationToken);

        return View(new UserIndexViewModel
        {
            Users = users,
            Branches = await LoadBranchOptionsAsync(cancellationToken),
            Filters = filters
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create(Guid? branchId, CancellationToken cancellationToken)
    {
        if (!IsActiveAdministrator())
        {
            return Forbid();
        }

        var branch = branchId.HasValue
            ? await _context.Branches.AsNoTracking().FirstOrDefaultAsync(item => item.Id == branchId.Value, cancellationToken)
            : null;

        var model = new UserFormViewModel
        {
            Branches = await LoadBranchOptionsAsync(cancellationToken),
            BranchId = branchId,
            Email = branch is not null ? BranchPortalAccountConventions.BuildEmail(branch.Code) : string.Empty,
            FullName = branch is not null ? BranchPortalAccountConventions.BuildFullName(branch.City) : string.Empty,
            Password = branch is not null ? BranchPortalAccountConventions.BuildDefaultPassword(branch.Code) : string.Empty,
            RoleSelections = BuildRoleSelections([])
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserFormViewModel model, CancellationToken cancellationToken)
    {
        if (!IsActiveAdministrator())
        {
            return Forbid();
        }

        model.Branches = await LoadBranchOptionsAsync(cancellationToken);
        model.RoleSelections = EnsureRoleSelections(model.RoleSelections);
        ValidateRoleSelection(model);
        ValidatePasswordRequired(model.Password, isEdit: false);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var userId = await _userManagement.CreateAsync(
                new CreateUserRequest
                {
                    Email = model.Email,
                    Password = model.Password!,
                    FullName = model.FullName,
                    Position = model.Position,
                    BranchId = model.BranchId,
                    IsActive = model.IsActive,
                    Roles = GetSelectedRoles(model)
                },
                cancellationToken);

            TempData["SuccessMessage"] = "Користувача створено.";
            return RedirectToAction(nameof(Edit), new { id = userId });
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id, CancellationToken cancellationToken)
    {
        if (!IsActiveAdministrator())
        {
            return Forbid();
        }

        var user = await _userManagement.GetForEditAsync(id, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        return View(await BuildFormViewModelAsync(user, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, UserFormViewModel model, CancellationToken cancellationToken)
    {
        if (!IsActiveAdministrator())
        {
            return Forbid();
        }

        if (!string.Equals(id, model.Id, StringComparison.Ordinal))
        {
            return BadRequest();
        }

        model.Branches = await LoadBranchOptionsAsync(cancellationToken);
        model.RoleSelections = EnsureRoleSelections(model.RoleSelections);
        ValidateRoleSelection(model);

        if (!ModelState.IsValid)
        {
            await ApplyPasswordRevealAsync(model, id, cancellationToken);
            return View(model);
        }

        try
        {
            await _userManagement.UpdateAsync(
                id,
                new UpdateUserRequest
                {
                    Email = model.Email,
                    FullName = model.FullName,
                    Position = model.Position,
                    BranchId = model.BranchId,
                    IsActive = model.IsActive,
                    Roles = GetSelectedRoles(model),
                    NewPassword = string.IsNullOrWhiteSpace(model.NewPassword) ? null : model.NewPassword
                },
                cancellationToken);

            TempData["SuccessMessage"] = "Зміни збережено.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            await ApplyPasswordRevealAsync(model, id, cancellationToken);
            return View(model);
        }
    }

    private async Task<UserFormViewModel> BuildFormViewModelAsync(
        UserEditDto user,
        CancellationToken cancellationToken)
    {
        var model = MapToFormViewModel(user, await LoadBranchOptionsAsync(cancellationToken));
        await ApplyPasswordRevealAsync(model, user.Id, cancellationToken);
        return model;
    }

    private async Task ApplyPasswordRevealAsync(
        UserFormViewModel model,
        string userId,
        CancellationToken cancellationToken)
    {
        var reveal = await _userManagement.GetRevealablePasswordAsync(userId, cancellationToken);
        model.CanRevealCurrentPassword = reveal.CanReveal;
        model.CurrentPassword = reveal.Password;
        model.CurrentPasswordStatusMessage = reveal.StatusMessage;
    }

    private bool IsActiveAdministrator() =>
        string.Equals(
            _activeLimsRole.ResolveActiveRole(User),
            LimsRoles.SystemAdministrator,
            StringComparison.Ordinal);

    private async Task<IReadOnlyList<BranchOptionDto>> LoadBranchOptionsAsync(CancellationToken cancellationToken) =>
        await _context.Branches
            .AsNoTracking()
            .Where(branch => branch.IsActive && !branch.IsAnnulled)
            .OrderBy(branch => branch.Code)
            .Select(branch => new BranchOptionDto
            {
                Id = branch.Id,
                Code = branch.Code,
                Name = branch.Name
            })
            .ToListAsync(cancellationToken);

    private static UserFormViewModel MapToFormViewModel(UserEditDto user, IReadOnlyList<BranchOptionDto> branches) =>
        new()
        {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            Position = user.Position,
            BranchId = user.BranchId,
            IsActive = user.IsActive,
            IsBranchPortalAccount = user.IsBranchPortalAccount,
            Branches = branches,
            RoleSelections = BuildRoleSelections(user.Roles)
        };

    private static List<RoleSelectionViewModel> BuildRoleSelections(IReadOnlyList<string> selectedRoles)
    {
        var selected = selectedRoles.ToHashSet(StringComparer.Ordinal);
        return LimsRoleCapabilitiesCatalog.All
            .Select(definition => new RoleSelectionViewModel
            {
                RoleCode = definition.RoleCode,
                DisplayName = definition.DisplayName,
                AccentColor = definition.AccentColor,
                IconClass = definition.IconClass,
                Summary = definition.Summary,
                IsSelected = selected.Contains(definition.RoleCode)
            })
            .ToList();
    }

    private static List<RoleSelectionViewModel> EnsureRoleSelections(IReadOnlyList<RoleSelectionViewModel> selections)
    {
        var selected = selections
            .Where(item => item.IsSelected)
            .Select(item => item.RoleCode)
            .ToHashSet(StringComparer.Ordinal);

        return BuildRoleSelections(selected.ToArray());
    }

    private static IReadOnlyList<string> GetSelectedRoles(UserFormViewModel model) =>
        model.RoleSelections
            .Where(item => item.IsSelected)
            .Select(item => item.RoleCode)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private void ValidateRoleSelection(UserFormViewModel model)
    {
        if (GetSelectedRoles(model).Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Оберіть хоча б одну роль.");
        }
    }

    private void ValidatePasswordRequired(string? password, bool isEdit)
    {
        if (!isEdit && string.IsNullOrWhiteSpace(password))
        {
            ModelState.AddModelError(nameof(UserFormViewModel.Password), "Пароль є обов'язковим.");
        }
    }
}
