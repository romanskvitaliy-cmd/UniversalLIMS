using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Laboratory;
using UniversalLIMS.Application.Laboratory.Abstractions;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Infrastructure.Persistence;

namespace UniversalLIMS.Infrastructure.Laboratory;

public sealed class LaboratoryOverviewService : ILaboratoryOverviewService
{
    private readonly ApplicationDbContext _context;
    private readonly ILaboratoryBranchContext _laboratoryBranchContext;

    public LaboratoryOverviewService(
        ApplicationDbContext context,
        ILaboratoryBranchContext laboratoryBranchContext)
    {
        _context = context;
        _laboratoryBranchContext = laboratoryBranchContext;
    }

    public async Task<LaboratoryOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var branchContext = await _laboratoryBranchContext.GetStateAsync(cancellationToken);

        var branches = await _context.Branches
            .AsNoTracking()
            .Where(branch => branch.IsActive && !branch.IsAnnulled)
            .OrderBy(branch => branch.Code)
            .Select(branch => new
            {
                branch.Id,
                branch.Code,
                branch.Name,
                branch.City
            })
            .ToListAsync(cancellationToken);

        var pendingSamples = await (
                from sample in _context.Samples.AsNoTracking()
                where !sample.IsAnnulled
                      && !sample.Order.IsAnnulled
                      && !sample.Order.Customer.IsAnnulled
                from document in sample.OrderDocuments
                where !document.IsAnnulled
                      && document.Status == OrderDocumentStatus.Pending
                select new
                {
                    sample.Id,
                    document.TargetBranchId
                })
            .ToListAsync(cancellationToken);

        var pendingSamplesByBranch = pendingSamples
            .GroupBy(row => row.TargetBranchId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(row => row.Id)
                    .Distinct()
                    .Count());

        var routedSamples = await (
            from sample in _context.Samples.AsNoTracking()
            where !sample.IsAnnulled
                  && !sample.Order.IsAnnulled
                  && !sample.Order.Customer.IsAnnulled
            from document in sample.OrderDocuments
            where !document.IsAnnulled
                  && document.Status != OrderDocumentStatus.Pending
            select new
            {
                sample.Id,
                sample.Status,
                document.TargetBranchId
            })
            .ToListAsync(cancellationToken);

        var samplesByBranch = routedSamples
            .GroupBy(row => row.TargetBranchId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .GroupBy(row => row.Id)
                    .Select(sampleGroup => sampleGroup.First())
                    .ToList());

        var branchOverviews = branches
            .Select(branch =>
            {
                samplesByBranch.TryGetValue(branch.Id, out var branchSamples);
                branchSamples ??= [];
                pendingSamplesByBranch.TryGetValue(branch.Id, out var awaitingSendCount);

                return new LaboratoryBranchOverviewDto
                {
                    BranchId = branch.Id,
                    Code = branch.Code,
                    Name = branch.Name,
                    City = branch.City,
                    ActiveSampleCount = branchSamples.Count,
                    InProgressSampleCount = branchSamples.Count(sample => sample.Status == SampleStatus.InProgress),
                    ResultsEnteredSampleCount = branchSamples.Count(sample => sample.Status == SampleStatus.ResultsEntered),
                    AwaitingSendSampleCount = awaitingSendCount
                };
            })
            .ToList();

        var distinctSamples = routedSamples
            .GroupBy(row => row.Id)
            .Select(group => group.First())
            .ToList();

        var totalAwaitingSend = pendingSamples
            .Select(row => row.Id)
            .Distinct()
            .Count();

        return new LaboratoryOverviewDto
        {
            Branches = branchOverviews,
            TotalActiveSampleCount = distinctSamples.Count,
            TotalInProgressSampleCount = distinctSamples.Count(sample => sample.Status == SampleStatus.InProgress),
            TotalResultsEnteredSampleCount = distinctSamples.Count(sample => sample.Status == SampleStatus.ResultsEntered),
            TotalAwaitingSendSampleCount = totalAwaitingSend,
            ActiveBranchId = branchContext.ActiveBranchId,
            ActiveBranchName = branchContext.ActiveBranchId is Guid activeBranchId
                ? branches.FirstOrDefault(branch => branch.Id == activeBranchId)?.Name
                : null
        };
    }
}
