using UniversalLIMS.Application.Common;

namespace UniversalLIMS.Application.Laboratory.Abstractions;

public interface ILaboratoryJournalService
{
    Task<PagedResult<SampleJournalItemDto>> GetSamplesAsync(
        SampleJournalFilter filter,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IncomingSampleNotificationDto>> GetIncomingSinceAsync(
        DateTime sinceUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IncomingReworkSampleNotificationDto>> GetReworkSinceAsync(
        DateTime sinceUtc,
        CancellationToken cancellationToken = default);
}
