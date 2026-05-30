using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Application.Common;
using UniversalLIMS.Application.Registration;
using UniversalLIMS.Application.Registration.Abstractions;
using UniversalLIMS.Domain.Laboratory;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Infrastructure.Persistence;

namespace UniversalLIMS.Infrastructure.Registration;

public sealed class SampleDeliveryService : ISampleDeliveryService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;

    public SampleDeliveryService(
        ApplicationDbContext context,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTimeProvider)
    {
        _context = context;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<PagedResult<SampleDeliveryQueueItemDto>> GetQueueAsync(
        SampleDeliveryQueueFilter filter,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var samplesQuery = _context.Samples
            .AsNoTracking()
            .Where(sample =>
                !sample.IsAnnulled
                && !sample.Order.IsAnnulled
                && !sample.Order.Customer.IsAnnulled
                && _context.ExpertConclusionReviews.Any(review =>
                    review.SampleId == sample.Id
                    && review.Status == ExpertConclusionStatus.Approved));

        if (_currentUser.BranchId is Guid branchId)
        {
            samplesQuery = samplesQuery.Where(sample => sample.Order.BranchId == branchId);
        }

        if (filter.ShowIssued)
        {
            samplesQuery = samplesQuery.Where(sample => sample.DeliveryStatus == SampleDeliveryStatus.Issued);
        }
        else
        {
            samplesQuery = samplesQuery.Where(sample => sample.DeliveryStatus == SampleDeliveryStatus.ReadyForPickup);
        }

        if (!string.IsNullOrWhiteSpace(filter.SampleNumber))
        {
            var pattern = $"%{filter.SampleNumber.Trim()}%";
            samplesQuery = samplesQuery.Where(sample => EF.Functions.Like(sample.Number, pattern));
        }

        if (!string.IsNullOrWhiteSpace(filter.CustomerFullName))
        {
            var pattern = $"%{filter.CustomerFullName.Trim()}%";
            samplesQuery = samplesQuery.Where(sample =>
                EF.Functions.Like(sample.Order.Customer.FullName, pattern));
        }

        if (filter.DateFrom.HasValue)
        {
            var fromUtc = DateTime.SpecifyKind(filter.DateFrom.Value.Date, DateTimeKind.Utc);
            samplesQuery = filter.ShowIssued
                ? samplesQuery.Where(sample => sample.IssuedAtUtc >= fromUtc)
                : samplesQuery.Where(sample =>
                    (sample.ReadyForPickupAtUtc ?? sample.RegisteredAt) >= fromUtc);
        }

        if (filter.DateTo.HasValue)
        {
            var toExclusiveUtc = DateTime.SpecifyKind(filter.DateTo.Value.Date.AddDays(1), DateTimeKind.Utc);
            samplesQuery = filter.ShowIssued
                ? samplesQuery.Where(sample => sample.IssuedAtUtc < toExclusiveUtc)
                : samplesQuery.Where(sample =>
                    (sample.ReadyForPickupAtUtc ?? sample.RegisteredAt) < toExclusiveUtc);
        }

        var totalCount = await samplesQuery.CountAsync(cancellationToken);

        var orderBy = filter.ShowIssued
            ? samplesQuery.OrderByDescending(sample => sample.IssuedAtUtc)
            : samplesQuery.OrderByDescending(sample => sample.ReadyForPickupAtUtc);

        var rows = await orderBy
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(sample => new
            {
                SampleId = sample.Id,
                sample.Number,
                sample.OrderId,
                sample.Order.ReferralNumber,
                CustomerFullName = sample.Order.Customer.FullName,
                InvestigationTypeName = sample.InvestigationType.NameUk,
                sample.DeliveryStatus,
                sample.ReadyForPickupAtUtc,
                sample.IssuedAtUtc,
                ApprovedAtUtc = _context.ExpertConclusionReviews
                    .Where(review => review.SampleId == sample.Id)
                    .Select(review => review.ApprovedAtUtc)
                    .FirstOrDefault(),
                DocumentCount = sample.OrderDocuments.Count(document =>
                    !document.IsAnnulled
                    && document.Status != OrderDocumentStatus.Pending),
                SingleDocumentIdForFinalPdf = sample.OrderDocuments
                    .Where(document =>
                        !document.IsAnnulled
                        && document.Status != OrderDocumentStatus.Pending)
                    .OrderBy(document => document.Id)
                    .Select(document => (Guid?)document.Id)
                    .FirstOrDefault(),
                SingleTemplateVersionIdForFinalPdf = sample.OrderDocuments
                    .Where(document =>
                        !document.IsAnnulled
                        && document.Status != OrderDocumentStatus.Pending)
                    .OrderBy(document => document.Id)
                    .Select(document => (Guid?)document.TemplateVersionId)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(row => new SampleDeliveryQueueItemDto
            {
                SampleId = row.SampleId,
                SampleNumber = row.Number,
                OrderId = row.OrderId,
                ReferralNumber = row.ReferralNumber,
                CustomerFullName = row.CustomerFullName,
                InvestigationTypeName = row.InvestigationTypeName,
                DeliveryStatus = row.DeliveryStatus,
                ReadyForPickupAtUtc = row.ReadyForPickupAtUtc,
                IssuedAtUtc = row.IssuedAtUtc,
                ApprovedAtUtc = row.ApprovedAtUtc,
                DocumentCount = row.DocumentCount,
                SingleDocumentIdForFinalPdf = row.DocumentCount == 1 ? row.SingleDocumentIdForFinalPdf : null,
                SingleTemplateVersionIdForFinalPdf = row.DocumentCount == 1 ? row.SingleTemplateVersionIdForFinalPdf : null
            })
            .ToList();

        return new PagedResult<SampleDeliveryQueueItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<bool> MarkIssuedAsync(Guid sampleId, CancellationToken cancellationToken = default)
    {
        var sample = await _context.Samples
            .Include(entity => entity.Order)
            .FirstOrDefaultAsync(entity =>
                entity.Id == sampleId
                && !entity.IsAnnulled
                && !entity.Order.IsAnnulled,
                cancellationToken);

        if (sample is null || sample.DeliveryStatus != SampleDeliveryStatus.ReadyForPickup)
        {
            return false;
        }

        var isApproved = await _context.ExpertConclusionReviews
            .AnyAsync(review =>
                review.SampleId == sampleId
                && review.Status == ExpertConclusionStatus.Approved,
                cancellationToken);

        if (!isApproved)
        {
            return false;
        }

        if (_currentUser.BranchId is Guid branchId && sample.Order.BranchId != branchId)
        {
            return false;
        }

        var now = _dateTimeProvider.UtcNow;
        sample.DeliveryStatus = SampleDeliveryStatus.Issued;
        sample.IssuedAtUtc = now;
        sample.IssuedByUserId = _currentUser.UserId;
        sample.UpdatedAtUtc = now;
        sample.UpdatedByUserId = _currentUser.UserId;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
