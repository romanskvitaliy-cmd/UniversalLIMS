using UniversalLIMS.Domain.Organization;

namespace UniversalLIMS.Application.Expert;

/// <summary>Зведення по одній експертній філії для адміністратора.</summary>
public sealed class ExpertBranchOverviewDto
{
    public Guid BranchId { get; init; }

    public required string Code { get; init; }

    public required string Name { get; init; }

    public required string City { get; init; }

    public BranchKind Kind { get; init; }

    /// <summary>Проби в активній черзі (не затверджено, не на rework).</summary>
    public int QueueSampleCount { get; init; }

    /// <summary>Підмножина черги зі статусом розгляду «В роботі».</summary>
    public int InProgressSampleCount { get; init; }
}

/// <summary>Огляд експертних філій для адміністратора (без фільтра поточного користувача).</summary>
public sealed class ExpertOverviewDto
{
    public IReadOnlyList<ExpertBranchOverviewDto> Branches { get; init; } = [];

    public int TotalQueueSampleCount { get; init; }

    public int TotalInProgressSampleCount { get; init; }
}
