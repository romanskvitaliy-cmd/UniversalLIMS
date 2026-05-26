using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Application.Laboratory.Abstractions;
using UniversalLIMS.Application.Registration;
using UniversalLIMS.Application.Security;
using UniversalLIMS.Infrastructure.Persistence;

namespace UniversalLIMS.Infrastructure.Laboratory;

public sealed class LaboratoryBranchContext : ILaboratoryBranchContext
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public LaboratoryBranchContext(
        ApplicationDbContext context,
        ICurrentUserService currentUser,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _currentUser = currentUser;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<LaboratoryBranchContextState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        if (!IsSystemAdministrator())
        {
            return new LaboratoryBranchContextState
            {
                ActiveBranchId = _currentUser.BranchId
            };
        }

        var branches = await LoadBranchesAsync(cancellationToken);
        var selectedBranchId = GetSelectedBranchId();
        if (selectedBranchId.HasValue && branches.All(branch => branch.Id != selectedBranchId.Value))
        {
            selectedBranchId = null;
            ClearSelectedBranchId();
        }

        return new LaboratoryBranchContextState
        {
            CanSelectBranch = true,
            ActiveBranchId = selectedBranchId,
            Branches = branches
        };
    }

    public async Task SetSelectedBranchAsync(Guid? branchId, CancellationToken cancellationToken = default)
    {
        if (!IsSystemAdministrator())
        {
            throw new InvalidOperationException("Вибір лабораторії доступний лише адміністратору.");
        }

        if (!branchId.HasValue || branchId == Guid.Empty)
        {
            ClearSelectedBranchId();
            return;
        }

        var exists = await _context.Branches
            .AsNoTracking()
            .AnyAsync(branch => branch.Id == branchId.Value && branch.IsActive && !branch.IsAnnulled, cancellationToken);
        if (!exists)
        {
            throw new InvalidOperationException("Лабораторію не знайдено або вона неактивна.");
        }

        _httpContextAccessor.HttpContext?.Session.SetString(
            SessionKeys.ActiveLaboratoryBranchId,
            branchId.Value.ToString("D"));
    }

    private async Task<IReadOnlyList<BranchOptionDto>> LoadBranchesAsync(CancellationToken cancellationToken)
    {
        return await _context.Branches
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
    }

    private bool IsSystemAdministrator()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        return user?.IsInRole(LimsRoles.SystemAdministrator) == true;
    }

    private Guid? GetSelectedBranchId()
    {
        var value = _httpContextAccessor.HttpContext?.Session.GetString(SessionKeys.ActiveLaboratoryBranchId);
        return Guid.TryParse(value, out var branchId) ? branchId : null;
    }

    private void ClearSelectedBranchId()
    {
        _httpContextAccessor.HttpContext?.Session.Remove(SessionKeys.ActiveLaboratoryBranchId);
    }
}
