using UniversalLIMS.Domain.Common;
using UniversalLIMS.Domain.Registration;

namespace UniversalLIMS.Domain.Laboratory;

/// <summary>
/// Експертний розгляд висновку по пробі. Окремий від лабораторних результатів (PDF / Conclusion scope).
/// </summary>
public sealed class ExpertConclusionReview : BaseEntity, ISoftAnnulled
{
    public Guid SampleId { get; set; }

    public Sample Sample { get; set; } = null!;

    public ExpertConclusionStatus Status { get; set; } = ExpertConclusionStatus.PendingReview;

    public DateTime? ReviewStartedAtUtc { get; set; }

    public DateTime? ApprovedAtUtc { get; set; }

    public string? ApprovedByUserId { get; set; }

    public string? NotesUk { get; set; }

    public bool IsAnnulled { get; set; }

    public DateTime? AnnulledAtUtc { get; set; }

    public string? AnnulledByUserId { get; set; }

    public string? AnnulmentReason { get; set; }
}
