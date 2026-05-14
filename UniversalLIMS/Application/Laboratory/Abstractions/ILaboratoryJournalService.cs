using UniversalLIMS.Application.Common;
using UniversalLIMS.Application.Laboratory.Dtos;

namespace UniversalLIMS.Application.Laboratory.Abstractions;

/// <summary>
/// Application service for the laboratory sample journal and read-side sample cards.
/// </summary>
public interface ILaboratoryJournalService
{
    /// <summary>
    /// Returns a paginated laboratory journal of samples that have configured result fields.
    /// </summary>
    Task<PagedResult<LaboratoryJournalItemDto>> GetJournalAsync(
        LaboratoryJournalFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the laboratory detail card for a sample, including active result rows.
    /// </summary>
    /// <exception cref="Domain.Common.Exceptions.EntityNotFoundException">Sample was not found.</exception>
    /// <exception cref="Domain.Common.Exceptions.BusinessRuleViolationException">
    /// Sample is not part of the laboratory journal.
    /// </exception>
    Task<SampleLaboratoryDetailsDto> GetSampleDetailsAsync(
        Guid sampleId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns <see langword="true"/> when every required result field has an active value on the sample.
    /// </summary>
    Task<bool> CanFinalizeSampleAsync(Guid sampleId, CancellationToken cancellationToken = default);
}
