using UniversalLIMS.Domain.Laboratory;

namespace UniversalLIMS.Application.Expert;

public static class ExpertConclusionStatusDisplay
{
    public static string ToUk(ExpertConclusionStatus status) =>
        status switch
        {
            ExpertConclusionStatus.PendingReview => "Очікує розгляду",
            ExpertConclusionStatus.InProgress => "В роботі",
            ExpertConclusionStatus.Approved => "Затверджено",
            _ => status.ToString()
        };

    public static string BadgeClass(ExpertConclusionStatus status) =>
        status switch
        {
            ExpertConclusionStatus.PendingReview => "lims-status-badge--pending",
            ExpertConclusionStatus.InProgress => "lims-status-badge--progress",
            ExpertConclusionStatus.Approved => "lims-status-badge--done",
            _ => "lims-status-badge--neutral"
        };
}
