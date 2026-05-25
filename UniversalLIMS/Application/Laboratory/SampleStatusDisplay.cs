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
}
