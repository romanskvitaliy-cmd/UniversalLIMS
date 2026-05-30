using UniversalLIMS.Application.Common;

namespace UniversalLIMS.Application.Registration.Abstractions;

public interface ISampleDeliveryService
{
    Task<PagedResult<SampleDeliveryQueueItemDto>> GetQueueAsync(
        SampleDeliveryQueueFilter filter,
        CancellationToken cancellationToken = default);

    Task<bool> MarkIssuedAsync(Guid sampleId, CancellationToken cancellationToken = default);
}
