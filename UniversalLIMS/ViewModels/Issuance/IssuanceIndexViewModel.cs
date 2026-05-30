using UniversalLIMS.Application.Common;
using UniversalLIMS.Application.Registration;

namespace UniversalLIMS.ViewModels.Issuance;

public sealed class IssuanceIndexViewModel
{
    public required SampleDeliveryQueueFilter Filter { get; init; }

    public required PagedResult<SampleDeliveryQueueItemDto> Result { get; init; }
}
