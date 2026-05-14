using UniversalLIMS.Domain.Registration;

namespace UniversalLIMS.Application.Laboratory.Dtos;

/// <summary>
/// One row in the laboratory sample journal table.
/// </summary>
public sealed class LaboratoryJournalItemDto
{
    public Guid SampleId { get; init; }

    public string SampleNumber { get; init; } = string.Empty;

    public DateTime ReceivedAt { get; init; }

    public Guid InvestigationTypeId { get; init; }

    public string InvestigationTypeName { get; init; } = string.Empty;

    public SampleStatus Status { get; init; }

    public string? ReferralNumber { get; init; }

    public string CustomerFullName { get; init; } = string.Empty;

    public string TargetBranchName { get; init; } = string.Empty;

    public int EnteredResultsCount { get; init; }

    public int RequiredResultsCount { get; init; }
}
