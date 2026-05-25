using UniversalLIMS.Application.Common;

namespace UniversalLIMS.Application.Laboratory.Abstractions;

public interface ILaboratoryJournalService
{
    Task<PagedResult<SampleJournalItemDto>> GetSamplesAsync(
        SampleJournalFilter filter,
        CancellationToken cancellationToken = default);
}
