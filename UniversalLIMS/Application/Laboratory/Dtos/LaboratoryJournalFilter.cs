using UniversalLIMS.Domain.Registration;

namespace UniversalLIMS.Application.Laboratory.Dtos;

/// <summary>
/// Filter criteria for the laboratory sample journal.
/// </summary>
public sealed class LaboratoryJournalFilter
{
    /// <summary>Lower bound (inclusive) for sample receipt/registration date.</summary>
    public DateTime? ReceivedDateFrom { get; init; }

    /// <summary>Upper bound (inclusive) for sample receipt/registration date.</summary>
    public DateTime? ReceivedDateTo { get; init; }

    /// <summary>Restrict to a single investigation type when set.</summary>
    public Guid? InvestigationTypeId { get; init; }

    /// <summary>Restrict to a single sample workflow status when set.</summary>
    public SampleStatus? SampleStatus { get; init; }

    /// <summary>
    /// Free-text search across sample number, referral number, and customer identity fields.
    /// </summary>
    public string? SearchText { get; init; }

    /// <summary>
    /// Optional target laboratory branch scope.
    /// When omitted, the service may apply the current user's branch from <c>ICurrentUserService</c>.
    /// </summary>
    public Guid? TargetBranchId { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 50;
}
