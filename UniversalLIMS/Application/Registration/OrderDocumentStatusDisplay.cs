using UniversalLIMS.Domain.Registration;

namespace UniversalLIMS.Application.Registration;

public static class OrderDocumentStatusDisplay
{
    public static string ToUk(OrderDocumentStatus status) =>
        status switch
        {
            OrderDocumentStatus.Pending => "Очікує",
            OrderDocumentStatus.SentToLab => "У лабораторії",
            OrderDocumentStatus.InProgress => "В роботі",
            OrderDocumentStatus.ResultsEntered => "Результати внесено",
            _ => status.ToString()
        };

    public static string SummarizeWorkflow(IReadOnlyList<OrderDocumentStatus> statuses)
    {
        if (statuses.Count == 0)
        {
            return "Без документів";
        }

        if (statuses.All(status => status == OrderDocumentStatus.Pending))
        {
            return "Заповнення реєстратором";
        }

        if (statuses.All(status => status >= OrderDocumentStatus.SentToLab))
        {
            return "Усі в лабораторії";
        }

        if (statuses.Any(status => status == OrderDocumentStatus.Pending)
            && statuses.Any(status => status >= OrderDocumentStatus.SentToLab))
        {
            return "Частково відправлено";
        }

        if (statuses.Any(status => status == OrderDocumentStatus.InProgress))
        {
            return "В процесі";
        }

        return "В процесі";
    }

    public static string BadgeClass(OrderDocumentStatus status) =>
        status switch
        {
            OrderDocumentStatus.Pending => "lims-status-badge--pending",
            OrderDocumentStatus.SentToLab => "lims-status-badge--sent",
            OrderDocumentStatus.InProgress => "lims-status-badge--progress",
            OrderDocumentStatus.ResultsEntered => "lims-status-badge--done",
            _ => "lims-status-badge--neutral"
        };
}
