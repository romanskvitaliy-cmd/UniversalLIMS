using UniversalLIMS.Domain.Registration;

namespace UniversalLIMS.Application.Registration;

public static class SampleDeliveryStatusDisplay
{
    public static string ToUk(SampleDeliveryStatus status) =>
        status switch
        {
            SampleDeliveryStatus.None => "—",
            SampleDeliveryStatus.ReadyForPickup => "Готово до видачі",
            SampleDeliveryStatus.Issued => "Видано",
            _ => status.ToString()
        };

    public static string BadgeClass(SampleDeliveryStatus status) =>
        status switch
        {
            SampleDeliveryStatus.ReadyForPickup => "lims-status-badge--pending",
            SampleDeliveryStatus.Issued => "lims-status-badge--done",
            _ => "lims-status-badge--neutral"
        };
}
