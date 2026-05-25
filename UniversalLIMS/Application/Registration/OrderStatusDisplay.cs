using UniversalLIMS.Domain.Registration;

namespace UniversalLIMS.Application.Registration;

public static class OrderStatusDisplay
{
    public static string ToUk(OrderStatus status) =>
        status switch
        {
            OrderStatus.Draft => "Чернетка",
            OrderStatus.Registered => "Зареєстровано",
            _ => status.ToString()
        };
}
