using UniversalLIMS.Application.Laboratory.Abstractions;
using UniversalLIMS.Infrastructure.Persistence;

namespace UniversalLIMS.Infrastructure.Persistence;

/// <summary>
/// EF Core backed unit-of-work over <see cref="ApplicationDbContext"/>.
/// </summary>
public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;

    public EfUnitOfWork(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
