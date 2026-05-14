using UniversalLIMS.Application.Laboratory.Dtos;

namespace UniversalLIMS.Application.Laboratory.Abstractions;

/// <summary>
/// Read-side query port for laboratory journal projections (EF query objects live in Infrastructure).
/// </summary>
public interface ILaboratoryJournalQuery
{
    /// <summary>
    /// Returns a page of journal rows and the total matching count for the supplied filter.
    /// </summary>
    Task<(IReadOnlyList<LaboratoryJournalItemDto> Items, int TotalCount)> SearchAsync(
        LaboratoryJournalFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the sample header for the laboratory detail card, or <see langword="null"/> when not found.
    /// </summary>
    Task<SampleLaboratoryDetailsDto?> GetSampleHeaderAsync(
        Guid sampleId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns distinct required <c>DataField</c> identifiers with <c>Scope = Result</c> for the sample's documents.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetRequiredResultDataFieldIdsAsync(
        Guid sampleId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns whether the sample is eligible for the laboratory journal (routed and has result fields).
    /// </summary>
    Task<bool> IsLaboratorySampleAsync(Guid sampleId, CancellationToken cancellationToken = default);
}
