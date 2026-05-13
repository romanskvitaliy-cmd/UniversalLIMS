using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Application.Registration.Abstractions;
using UniversalLIMS.Infrastructure.Persistence;

namespace UniversalLIMS.Infrastructure.Registration;

public sealed class NumberingService : INumberingService
{
    private readonly ApplicationDbContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;

    public NumberingService(ApplicationDbContext context, IDateTimeProvider dateTimeProvider)
    {
        _context = context;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<string> AssignSampleNumberAsync(Guid branchId, CancellationToken cancellationToken = default)
    {
        var branchCode = await GetBranchCodeAsync(branchId, cancellationToken);
        var year = _dateTimeProvider.UtcNow.Year;
        var prefix = $"{branchCode}-{year}-";

        var latestNumber = await _context.Samples
            .IgnoreQueryFilters()
            .Where(sample => sample.Order.BranchId == branchId && sample.Number.StartsWith(prefix))
            .OrderByDescending(sample => sample.Number)
            .Select(sample => sample.Number)
            .FirstOrDefaultAsync(cancellationToken);

        var nextSequence = ParseSequence(latestNumber, prefix) + 1;
        return $"{prefix}{nextSequence:D5}";
    }

    public async Task<string> AssignReferralNumberAsync(Guid branchId, CancellationToken cancellationToken = default)
    {
        var branchCode = await GetBranchCodeAsync(branchId, cancellationToken);
        var year = _dateTimeProvider.UtcNow.Year;
        var prefix = $"REF-{branchCode}-{year}-";

        var latestNumber = await _context.Orders
            .IgnoreQueryFilters()
            .Where(order => order.BranchId == branchId &&
                            order.ReferralNumber != null &&
                            order.ReferralNumber.StartsWith(prefix))
            .OrderByDescending(order => order.ReferralNumber)
            .Select(order => order.ReferralNumber!)
            .FirstOrDefaultAsync(cancellationToken);

        var nextSequence = ParseSequence(latestNumber, prefix) + 1;
        return $"{prefix}{nextSequence:D5}";
    }

    private async Task<string> GetBranchCodeAsync(Guid branchId, CancellationToken cancellationToken)
    {
        var branchCode = await _context.Branches
            .Where(branch => branch.Id == branchId)
            .Select(branch => branch.Code)
            .FirstOrDefaultAsync(cancellationToken);

        if (branchCode is null)
        {
            throw new InvalidOperationException("Філію не знайдено.");
        }

        return branchCode;
    }

    private static int ParseSequence(string? latestNumber, string prefix)
    {
        if (string.IsNullOrWhiteSpace(latestNumber) || !latestNumber.StartsWith(prefix, StringComparison.Ordinal))
        {
            return 0;
        }

        var suffix = latestNumber[prefix.Length..];
        return int.TryParse(suffix, out var sequence) ? sequence : 0;
    }
}
