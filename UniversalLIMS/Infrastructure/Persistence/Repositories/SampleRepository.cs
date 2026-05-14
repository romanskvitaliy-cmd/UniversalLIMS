using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Laboratory.Abstractions;
using UniversalLIMS.Domain.Registration;

namespace UniversalLIMS.Infrastructure.Persistence.Repositories;

public sealed class SampleRepository : ISampleRepository
{
    private readonly ApplicationDbContext _context;

    public SampleRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<Sample?> GetByIdIncludingAnnulledAsync(Guid sampleId, CancellationToken cancellationToken = default)
    {
        return _context.Samples
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(sample => sample.Id == sampleId, cancellationToken);
    }
}
