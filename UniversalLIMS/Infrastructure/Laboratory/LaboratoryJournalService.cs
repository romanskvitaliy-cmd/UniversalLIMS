using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Application.Common;
using UniversalLIMS.Application.Laboratory;
using UniversalLIMS.Application.Laboratory.Abstractions;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Infrastructure.Persistence;

namespace UniversalLIMS.Infrastructure.Laboratory;

public sealed class LaboratoryJournalService : ILaboratoryJournalService
{
    private readonly ApplicationDbContext _context;
    private readonly ILaboratoryBranchContext _laboratoryBranchContext;

    public LaboratoryJournalService(
        ApplicationDbContext context,
        ILaboratoryBranchContext laboratoryBranchContext)
    {
        _context = context;
        _laboratoryBranchContext = laboratoryBranchContext;
    }

    public async Task<PagedResult<SampleJournalItemDto>> GetSamplesAsync(
        SampleJournalFilter filter,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);
        var branchContext = await _laboratoryBranchContext.GetStateAsync(cancellationToken);

        var samplesQuery = _context.Samples
            .AsNoTracking()
            .Where(sample =>
                !sample.IsAnnulled
                && !sample.Order.IsAnnulled
                && !sample.Order.Customer.IsAnnulled
                && sample.OrderDocuments.Any(document =>
                    !document.IsAnnulled
                    && document.Status != OrderDocumentStatus.Pending
                    && (!branchContext.ActiveBranchId.HasValue
                        || document.TargetBranchId == branchContext.ActiveBranchId.Value)));

        var targetBranchFilter = branchContext.ActiveBranchId;

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

        var rows = await samplesQuery
            .OrderByDescending(sample => sample.RegisteredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(sample => new
            {
                SampleId = sample.Id,
                SampleNumber = sample.Number,
                OrderId = sample.OrderId,
                ReferralNumber = sample.Order.ReferralNumber,
                CustomerFullName = sample.Order.Customer.FullName,
                RegisteredAt = sample.RegisteredAt,
                Status = sample.Status,
                InvestigationTypeName = sample.InvestigationType.NameUk,
                TargetBranchNames = sample.OrderDocuments
                    .Where(document =>
                        !document.IsAnnulled
                        && document.Status != OrderDocumentStatus.Pending
                        && (!targetBranchFilter.HasValue || document.TargetBranchId == targetBranchFilter.Value))
                    .Select(document => document.TargetBranch.Name)
                    .Distinct()
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(row => new SampleJournalItemDto
            {
                SampleId = row.SampleId,
                SampleNumber = row.SampleNumber,
                OrderId = row.OrderId,
                ReferralNumber = row.ReferralNumber,
                CustomerFullName = row.CustomerFullName,
                RegisteredAt = row.RegisteredAt,
                Status = row.Status,
                InvestigationTypeName = row.InvestigationTypeName,
                TargetBranchName = string.Join(", ", row.TargetBranchNames)
            })
            .ToList();

        return new PagedResult<SampleJournalItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}
