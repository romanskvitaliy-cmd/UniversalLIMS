namespace UniversalLIMS.Application.Laboratory;

/// <summary>Зведення по одній лабораторії для адміністратора.</summary>
public sealed class LaboratoryBranchOverviewDto
{
    public Guid BranchId { get; init; }

    public required string Code { get; init; }

    public required string Name { get; init; }

    public required string City { get; init; }

    public int ActiveSampleCount { get; init; }

    public int InProgressSampleCount { get; init; }

    public int ResultsEnteredSampleCount { get; init; }
}

/// <summary>Огляд усіх лабораторій для адміністратора.</summary>
public sealed class LaboratoryOverviewDto
{
    public IReadOnlyList<LaboratoryBranchOverviewDto> Branches { get; init; } = [];

    public int TotalActiveSampleCount { get; init; }

    public int TotalInProgressSampleCount { get; init; }

    public int TotalResultsEnteredSampleCount { get; init; }

    public Guid? ActiveBranchId { get; init; }

    public string? ActiveBranchName { get; init; }
}
