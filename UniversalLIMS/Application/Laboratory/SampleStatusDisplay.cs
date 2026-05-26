using UniversalLIMS.Domain.Registration;

namespace UniversalLIMS.Application.Laboratory;

public static class SampleStatusDisplay
{
    public static string ToUk(SampleStatus status) =>
        status switch
        {
            SampleStatus.Registered => "Зареєстрована",
            SampleStatus.Routed => "Направлена",
            SampleStatus.InProgress => "В роботі",
            SampleStatus.ResultsEntered => "Результати внесено",
            _ => status.ToString()
        };

    public static string BadgeClass(SampleStatus status) =>
        status switch
        {
            SampleStatus.Registered => "lims-status-badge--registered",
            SampleStatus.Routed => "lims-status-badge--sent",
            SampleStatus.InProgress => "lims-status-badge--progress",
            SampleStatus.ResultsEntered => "lims-status-badge--done",
            _ => "lims-status-badge--neutral"
        };
}
