using UniversalLIMS.Application.Common;

namespace UniversalLIMS.Application.Expert.Abstractions;

public interface IExpertReviewQueueService
{
    Task<PagedResult<ExpertReviewQueueItemDto>> GetQueueAsync(
        ExpertReviewQueueFilter filter,
        CancellationToken cancellationToken = default);
}
