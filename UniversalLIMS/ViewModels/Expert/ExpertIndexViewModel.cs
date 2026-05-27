using UniversalLIMS.Application.Common;
using UniversalLIMS.Application.Expert;
using UniversalLIMS.Domain.Laboratory;

namespace UniversalLIMS.ViewModels.Expert;

public sealed class ExpertIndexViewModel
{
    public ExpertReviewQueueFilter Filter { get; init; } = new();

    public PagedResult<ExpertReviewQueueItemDto> Result { get; init; } = new()
    {
        Items = [],
        Page = 1,
        PageSize = 20
    };

    public IReadOnlyList<(ExpertConclusionStatus? Value, string Label)> ReviewStatusOptions { get; init; } =
    [
        (null, "Усі (крім затверджених)"),
        (ExpertConclusionStatus.PendingReview, ExpertConclusionStatusDisplay.ToUk(ExpertConclusionStatus.PendingReview)),
        (ExpertConclusionStatus.InProgress, ExpertConclusionStatusDisplay.ToUk(ExpertConclusionStatus.InProgress)),
        (ExpertConclusionStatus.Approved, ExpertConclusionStatusDisplay.ToUk(ExpertConclusionStatus.Approved))
    ];
}
