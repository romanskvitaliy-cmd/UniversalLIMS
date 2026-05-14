using UniversalLIMS.Domain.Laboratory;

namespace UniversalLIMS.Application.Laboratory.Abstractions;

/// <summary>
/// Persistence port for immutable <see cref="SampleResultValue"/> rows (SSOT for laboratory results).
/// </summary>
public interface ISampleResultRepository
{
    Task<SampleResultValue?> GetByIdAsync(
        Guid resultId,
        bool includeAnnulled = false,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SampleResultValue>> GetForSampleAsync(
        Guid sampleId,
        bool includeAnnulled = false,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsActiveAsync(
        Guid sampleId,
        Guid dataFieldId,
        CancellationToken cancellationToken = default);

    Task<int> CountActiveForSampleAsync(Guid sampleId, CancellationToken cancellationToken = default);

    void Add(SampleResultValue result);
}
