using UniversalLIMS.Domain.Laboratory;

namespace UniversalLIMS.Application.Expert;

/// <summary>Рядок черги експерта: проба з внесеними лабораторними результатами, готова до висновку.</summary>
public sealed class ExpertReviewQueueItemDto
{
    public Guid SampleId { get; init; }

    public required string SampleNumber { get; init; }

    public Guid OrderId { get; init; }

    public string? ReferralNumber { get; init; }

    public required string CustomerFullName { get; init; }

    public DateTime RegisteredAt { get; init; }

    public required string InvestigationTypeName { get; init; }

    public required string TargetBranchName { get; init; }

    public int DocumentCount { get; init; }

    public ExpertConclusionStatus? ReviewStatus { get; init; }
}
