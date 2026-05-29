using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Application.Organization;
using UniversalLIMS.Application.Organization.Abstractions;
using UniversalLIMS.Domain.Organization;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Infrastructure.Persistence;

namespace UniversalLIMS.Infrastructure.Organization;

public sealed class BranchService : IBranchService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public BranchService(
        ApplicationDbContext context,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTime)
    {
        _context = context;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<IReadOnlyList<BranchListItemDto>> GetListAsync(CancellationToken cancellationToken = default)
    {
        var branches = await _context.Branches
            .AsNoTracking()
            .OrderBy(branch => branch.Code)
            .Select(branch => new
            {
                branch.Id,
                branch.Code,
                branch.Name,
                branch.City,
                branch.Address,
                branch.IsActive
            })
            .ToListAsync(cancellationToken);

        var workflowCounts = await _context.OrderDocuments
            .AsNoTracking()
            .Where(document => document.Status != OrderDocumentStatus.Pending)
            .GroupBy(document => document.TargetBranchId)
            .Select(group => new { BranchId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.BranchId, item => item.Count, cancellationToken);

        var pendingCounts = await _context.OrderDocuments
            .AsNoTracking()
            .Where(document => document.Status == OrderDocumentStatus.Pending)
            .GroupBy(document => document.TargetBranchId)
            .Select(group => new { BranchId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.BranchId, item => item.Count, cancellationToken);

        var userCounts = await _context.Users
            .AsNoTracking()
            .Where(user => user.BranchId != null && user.IsActive)
            .GroupBy(user => user.BranchId!.Value)
            .Select(group => new { BranchId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.BranchId, item => item.Count, cancellationToken);

        return branches
            .Select(branch => new BranchListItemDto
            {
                Id = branch.Id,
                Code = branch.Code,
                Name = branch.Name,
                City = branch.City,
                Address = branch.Address,
                IsActive = branch.IsActive,
                WorkflowDocumentCount = workflowCounts.GetValueOrDefault(branch.Id),
                PendingDocumentCount = pendingCounts.GetValueOrDefault(branch.Id),
                AssignedUserCount = userCounts.GetValueOrDefault(branch.Id)
            })
            .ToList();
    }

    public async Task<BranchEditDto?> GetForEditAsync(Guid branchId, CancellationToken cancellationToken = default)
    {
        return await _context.Branches
            .AsNoTracking()
            .Where(branch => branch.Id == branchId)
            .Select(branch => new BranchEditDto
            {
                Id = branch.Id,
                Code = branch.Code,
                Name = branch.Name,
                City = branch.City,
                Address = branch.Address,
                IsActive = branch.IsActive
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Guid> CreateAsync(CreateBranchRequest request, CancellationToken cancellationToken = default)
    {
        ValidateCreateRequest(request);

        var normalizedCode = request.Code.Trim().ToUpperInvariant();

        var codeExists = await _context.Branches
            .IgnoreQueryFilters()
            .AnyAsync(branch => branch.Code == normalizedCode, cancellationToken);

        if (codeExists)
        {
            throw new InvalidOperationException($"Філію з кодом «{normalizedCode}» вже створено.");
        }

        var branch = new Branch
        {
            Code = normalizedCode,
            Name = request.Name.Trim(),
            City = request.City.Trim(),
            Address = request.Address?.Trim(),
            IsActive = true
        };

        _context.Branches.Add(branch);
        await _context.SaveChangesAsync(cancellationToken);

        return branch.Id;
    }

    public async Task UpdateAsync(Guid branchId, UpdateBranchRequest request, CancellationToken cancellationToken = default)
    {
        ValidateUpdateRequest(request);

        var branch = await _context.Branches
            .FirstOrDefaultAsync(item => item.Id == branchId, cancellationToken);

        if (branch is null)
        {
            throw new InvalidOperationException("Філію не знайдено.");
        }

        branch.Name = request.Name.Trim();
        branch.City = request.City.Trim();
        branch.Address = request.Address?.Trim();
        branch.IsActive = request.IsActive;

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task AnnulAsync(Guid branchId, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOperationException("Причина анулювання є обов'язковою.");
        }

        var branch = await _context.Branches
            .FirstOrDefaultAsync(item => item.Id == branchId, cancellationToken);

        if (branch is null)
        {
            throw new InvalidOperationException("Філію не знайдено.");
        }

        var hasWorkflowDocuments = await _context.OrderDocuments
            .AsNoTracking()
            .AnyAsync(
                document => document.TargetBranchId == branchId
                            && document.Status != OrderDocumentStatus.Pending,
                cancellationToken);

        if (hasWorkflowDocuments)
        {
            throw new InvalidOperationException(
                "Неможливо анулювати філію: є документи в лабораторному workflow. Завершіть або анулюйте їх спочатку.");
        }

        branch.AnnulmentReason = reason.Trim();
        branch.IsAnnulled = true;
        branch.AnnulledAtUtc = _dateTime.UtcNow;
        branch.AnnulledByUserId = _currentUser.UserId;
        branch.IsActive = false;

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateCreateRequest(CreateBranchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            throw new InvalidOperationException("Код філії є обов'язковим.");
        }

        if (request.Code.Trim().Length > 16)
        {
            throw new InvalidOperationException("Код філії не може перевищувати 16 символів.");
        }

        ValidateUpdateRequest(new UpdateBranchRequest
        {
            Name = request.Name,
            City = request.City,
            Address = request.Address,
            IsActive = true
        });
    }

    private static void ValidateUpdateRequest(UpdateBranchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Назву філії є обов'язковою.");
        }

        if (string.IsNullOrWhiteSpace(request.City))
        {
            throw new InvalidOperationException("Місто філії є обов'язковим.");
        }
    }
}
