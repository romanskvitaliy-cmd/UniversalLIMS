namespace UniversalLIMS.Application.Laboratory.Abstractions;

/// <summary>
/// Coordinates persistence of laboratory unit-of-work boundaries.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Persists all pending changes tracked by the current unit of work.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
