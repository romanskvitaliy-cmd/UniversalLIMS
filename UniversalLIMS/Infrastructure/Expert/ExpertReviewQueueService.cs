using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Common;
using UniversalLIMS.Application.Expert;
using UniversalLIMS.Application.Expert.Abstractions;
using UniversalLIMS.Domain.Laboratory;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Infrastructure.Persistence;

namespace UniversalLIMS.Infrastructure.Expert;

public sealed class ExpertReviewQueueService : IExpertReviewQueueService
{
    private readonly ApplicationDbContext _context;

    public ExpertReviewQueueService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<ExpertReviewQueueItemDto>> GetQueueAsync(
        ExpertReviewQueueFilter filter,
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
                && sample.OrderDocuments.Any(document =>
                    !document.IsAnnulled
                    && document.Status != OrderDocumentStatus.Pending)
                && !sample.OrderDocuments.Any(document =>
                    !document.IsAnnulled
                    && document.Status != OrderDocumentStatus.Pending
                    && document.Status != OrderDocumentStatus.ResultsEntered));

        if (!string.IsNullOrWhiteSpace(filter.SampleNumber))
        {
            var pattern = $"%{filter.SampleNumber.Trim()}%";
            samplesQuery = samplesQuery.Where(sample => EF.Functions.Like(sample.Number, pattern));
        }

        if (!string.IsNullOrWhiteSpace(filter.NotesContainsUk))
        {
            var notesPattern = $"%{filter.NotesContainsUk.Trim()}%";
            samplesQuery = samplesQuery.Where(sample =>
                _context.ExpertConclusionReviews.Any(review =>
                    review.SampleId == sample.Id
                    && review.NotesUk != null
                    && EF.Functions.Like(review.NotesUk, notesPattern)));
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

        if (filter.ReviewStatus == ExpertConclusionStatus.Approved)
        {
            samplesQuery = samplesQuery.Where(sample =>
                _context.ExpertConclusionReviews.Any(review =>
                    review.SampleId == sample.Id
                    && review.Status == ExpertConclusionStatus.Approved));
        }
        else
        {
            samplesQuery = samplesQuery.Where(sample =>
                !_context.ExpertConclusionReviews.Any(review =>
                    review.SampleId == sample.Id
                    && review.Status == ExpertConclusionStatus.Approved));

            if (filter.ReviewStatus.HasValue)
            {
                var status = filter.ReviewStatus.Value;
                samplesQuery = samplesQuery.Where(sample =>
                    _context.ExpertConclusionReviews.Any(review =>
                        review.SampleId == sample.Id
                        && review.Status == status));
            }
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
                InvestigationTypeName = sample.InvestigationType.NameUk,
                DocumentCount = sample.OrderDocuments.Count(document =>
                    !document.IsAnnulled
                    && document.Status != OrderDocumentStatus.Pending),
                TargetBranchNames = sample.OrderDocuments
                    .Where(document =>
                        !document.IsAnnulled
                        && document.Status != OrderDocumentStatus.Pending)
                    .Select(document => document.TargetBranch.Name)
                    .Distinct()
                    .ToList(),
                ReviewStatus = _context.ExpertConclusionReviews
                    .Where(review => review.SampleId == sample.Id)
                    .Select(review => (ExpertConclusionStatus?)review.Status)
                    .FirstOrDefault(),
                ApprovedAtUtc = _context.ExpertConclusionReviews
                    .Where(review => review.SampleId == sample.Id)
                    .Select(review => review.ApprovedAtUtc)
                    .FirstOrDefault(),
                ApprovedByUserId = _context.ExpertConclusionReviews
                    .Where(review => review.SampleId == sample.Id)
                    .Select(review => review.ApprovedByUserId)
                    .FirstOrDefault(),
                NotesUk = _context.ExpertConclusionReviews
                    .Where(review => review.SampleId == sample.Id)
                    .Select(review => review.NotesUk)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(row => new ExpertReviewQueueItemDto
            {
                SampleId = row.SampleId,
                SampleNumber = row.SampleNumber,
                OrderId = row.OrderId,
                ReferralNumber = row.ReferralNumber,
                CustomerFullName = row.CustomerFullName,
                RegisteredAt = row.RegisteredAt,
                InvestigationTypeName = row.InvestigationTypeName,
                DocumentCount = row.DocumentCount,
                TargetBranchName = string.Join(", ", row.TargetBranchNames),
                ReviewStatus = row.ReviewStatus,
                ApprovedAtUtc = row.ApprovedAtUtc,
                ApprovedByUserId = row.ApprovedByUserId,
                NotesUk = row.NotesUk
            })
            .ToList();

        return new PagedResult<ExpertReviewQueueItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}
