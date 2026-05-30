using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Expert;
using UniversalLIMS.Application.Expert.Abstractions;
using UniversalLIMS.Domain.Laboratory;
using UniversalLIMS.Domain.Organization;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Infrastructure.Persistence;

namespace UniversalLIMS.Infrastructure.Expert;

public sealed class ExpertOverviewService : IExpertOverviewService
{
    private readonly ApplicationDbContext _context;

    public ExpertOverviewService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ExpertOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var expertBranches = await _context.Branches
            .AsNoTracking()
            .Where(branch => branch.IsActive
                             && !branch.IsAnnulled
                             && (branch.Kind == BranchKind.Expert || branch.Kind == BranchKind.Mixed))
            .OrderBy(branch => branch.Code)
            .Select(branch => new
            {
                branch.Id,
                branch.Code,
                branch.Name,
                branch.City,
                branch.Kind
            })
            .ToListAsync(cancellationToken);

        var branchOverviews = new List<ExpertBranchOverviewDto>();
        var totalQueue = 0;
        var totalInProgress = 0;

        foreach (var branch in expertBranches)
        {
            var queueQuery = BuildActiveQueueQuery(branch.Id);
            var queueCount = await queueQuery.CountAsync(cancellationToken);
            var inProgressCount = await queueQuery
                .Where(sample =>
                    _context.ExpertConclusionReviews.Any(review =>
                        review.SampleId == sample.Id
                        && review.Status == ExpertConclusionStatus.InProgress))
                .CountAsync(cancellationToken);

            branchOverviews.Add(new ExpertBranchOverviewDto
            {
                BranchId = branch.Id,
                Code = branch.Code,
                Name = branch.Name,
                City = branch.City,
                Kind = branch.Kind,
                QueueSampleCount = queueCount,
                InProgressSampleCount = inProgressCount
            });

            totalQueue += queueCount;
            totalInProgress += inProgressCount;
        }

        return new ExpertOverviewDto
        {
            Branches = branchOverviews,
            TotalQueueSampleCount = totalQueue,
            TotalInProgressSampleCount = totalInProgress
        };
    }

    private IQueryable<Sample> BuildActiveQueueQuery(Guid expertBranchId) =>
        ApplyExpertBranchFilter(BuildExpertEligibleSamplesQuery(), expertBranchId)
            .Where(sample =>
                !_context.ExpertConclusionReviews.Any(review =>
                    review.SampleId == sample.Id
                    && (review.Status == ExpertConclusionStatus.Approved
                        || review.Status == ExpertConclusionStatus.ReturnedForRework)));

    private IQueryable<Sample> BuildExpertEligibleSamplesQuery() =>
        _context.Samples
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

    private static IQueryable<Sample> ApplyExpertBranchFilter(IQueryable<Sample> samplesQuery, Guid expertBranchId) =>
        samplesQuery.Where(sample =>
            sample.OrderDocuments.Any(document =>
                !document.IsAnnulled
                && document.Status != OrderDocumentStatus.Pending
                && (document.TargetBranchId == expertBranchId
                    || document.TargetBranch.ExpertBranchId == expertBranchId)));
}
