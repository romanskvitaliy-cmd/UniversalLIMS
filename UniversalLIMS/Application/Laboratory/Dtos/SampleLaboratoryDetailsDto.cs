using UniversalLIMS.Domain.Registration;

namespace UniversalLIMS.Application.Laboratory.Dtos;

/// <summary>
/// Detailed laboratory card for a single sample, including active result rows.
/// </summary>
public sealed class SampleLaboratoryDetailsDto
{
    public Guid SampleId { get; init; }

    public string SampleNumber { get; init; } = string.Empty;

    public DateTime ReceivedAt { get; init; }

    public DateTime? RoutedAtUtc { get; init; }

    public SampleStatus Status { get; init; }

    public string? Notes { get; init; }

    public Guid InvestigationTypeId { get; init; }

    public string InvestigationTypeName { get; init; } = string.Empty;

    public Guid OrderId { get; init; }

    public string? ReferralNumber { get; init; }

    public string CustomerFullName { get; init; } = string.Empty;

    public string? CustomerOrganizationName { get; init; }

    public string TargetBranchName { get; init; } = string.Empty;

    public int RequiredResultsCount { get; init; }

    public int EnteredResultsCount { get; init; }

    public IReadOnlyList<LaboratoryResultDto> Results { get; init; } = [];
}
