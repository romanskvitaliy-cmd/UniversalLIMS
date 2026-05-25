using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Application.Common;
using UniversalLIMS.Application.Laboratory;
using UniversalLIMS.Application.Laboratory.Abstractions;
using UniversalLIMS.Infrastructure.Persistence;

namespace UniversalLIMS.Infrastructure.Laboratory;

public sealed class LaboratoryJournalService : ILaboratoryJournalService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public LaboratoryJournalService(ApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<SampleJournalItemDto>> GetSamplesAsync(
        SampleJournalFilter filter,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var samplesQuery = _context.Samples
            .AsNoTracking()
            .Where(sample => !sample.Order.IsAnnulled && !sample.Order.Customer.IsAnnulled);

        if (_currentUser.BranchId is Guid branchId)
        {
            samplesQuery = samplesQuery.Where(sample => sample.Order.BranchId == branchId);
        }

        if (!string.IsNullOrWhiteSpace(filter.SampleNumber))
        {
            var pattern = $"%{filter.SampleNumber.Trim()}%";
            samplesQuery = samplesQuery.Where(sample => EF.Functions.Like(sample.Number, pattern));
        }

        if (filter.Status.HasValue)
        {
            samplesQuery = samplesQuery.Where(sample => sample.Status == filter.Status.Value);
        }

        if (filter.DateFrom.HasValue)
        {
            var fromUtc = DateTime.SpecifyKind(filter.DateFrom.Value.Date, DateTimeKind.Utc);
            samplesQuery = samplesQuery.Where(sample => sample.RegisteredAt >= fromUtc);
        }

        if (filter.DateTo.HasValue)
        {
            var toExclusiveUtc = DateTime.SpecifyKind(filter.DateTo.Value.Date.AddDays(1), DateTimeKind.Utc);
            samplesQuery = samplesQuery.Where(sample => sample.RegisteredAt < toExclusiveUtc);
        }

        var totalCount = await samplesQuery.CountAsync(cancellationToken);

        var items = await samplesQuery
            .OrderByDescending(sample => sample.RegisteredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(sample => new SampleJournalItemDto
            {
                SampleId = sample.Id,
                SampleNumber = sample.Number,
                OrderId = sample.OrderId,
                ReferralNumber = sample.Order.ReferralNumber,
                CustomerFullName = sample.Order.Customer.FullName,
                RegisteredAt = sample.RegisteredAt,
                Status = sample.Status,
                InvestigationTypeName = sample.InvestigationType.NameUk
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<SampleJournalItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}
