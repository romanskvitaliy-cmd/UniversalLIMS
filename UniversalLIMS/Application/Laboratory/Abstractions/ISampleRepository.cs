using UniversalLIMS.Domain.Registration;

namespace UniversalLIMS.Application.Laboratory.Abstractions;

/// <summary>
/// Read/write access to <see cref="Sample"/> aggregates for the laboratory workflow.
/// </summary>
public interface ISampleRepository
{
    /// <summary>
    /// Loads a sample including annulled rows (required to enforce annulment rules explicitly).
    /// </summary>
    Task<Sample?> GetByIdIncludingAnnulledAsync(Guid sampleId, CancellationToken cancellationToken = default);
}
