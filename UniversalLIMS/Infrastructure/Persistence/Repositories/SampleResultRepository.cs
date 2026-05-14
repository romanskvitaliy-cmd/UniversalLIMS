using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Laboratory.Abstractions;
using UniversalLIMS.Domain.Laboratory;

namespace UniversalLIMS.Infrastructure.Persistence.Repositories;

public sealed class SampleResultRepository : ISampleResultRepository
{
    private readonly ApplicationDbContext _context;

    public SampleResultRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<SampleResultValue?> GetByIdAsync(
        Guid resultId,
        bool includeAnnulled = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.SampleResultValues.AsQueryable();

        if (includeAnnulled)
        {
            query = query.IgnoreQueryFilters();
        }

        return query.FirstOrDefaultAsync(result => result.Id == resultId, cancellationToken);
    }

    public async Task<IReadOnlyList<SampleResultValue>> GetForSampleAsync(
        Guid sampleId,
        bool includeAnnulled = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.SampleResultValues
            .Where(result => result.SampleId == sampleId);

        if (includeAnnulled)
        {
            query = query.IgnoreQueryFilters();
        }

        return await query
            .OrderBy(result => result.EnteredAtUtc)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsActiveAsync(
        Guid sampleId,
        Guid dataFieldId,
        CancellationToken cancellationToken = default)
    {
        return _context.SampleResultValues.AnyAsync(
            result => result.SampleId == sampleId && result.DataFieldId == dataFieldId,
            cancellationToken);
    }

    public Task<int> CountActiveForSampleAsync(Guid sampleId, CancellationToken cancellationToken = default)
    {
        return _context.SampleResultValues.CountAsync(
            result => result.SampleId == sampleId,
            cancellationToken);
    }

    public void Add(SampleResultValue result)
    {
        _context.SampleResultValues.Add(result);
    }
}
