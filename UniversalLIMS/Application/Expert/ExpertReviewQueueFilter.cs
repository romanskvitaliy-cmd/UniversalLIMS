using UniversalLIMS.Domain.Laboratory;

namespace UniversalLIMS.Application.Expert;

public sealed class ExpertReviewQueueFilter
{
    public string? SampleNumber { get; set; }

    public string? NotesContainsUk { get; set; }

    public ExpertConclusionStatus? ReviewStatus { get; set; }

    public DateTime? DateFrom { get; set; }

    public DateTime? DateTo { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}
