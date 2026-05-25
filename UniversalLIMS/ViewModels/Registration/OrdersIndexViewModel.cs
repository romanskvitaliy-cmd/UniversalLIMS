using UniversalLIMS.Application.Common;
using UniversalLIMS.Application.Registration;
using UniversalLIMS.Domain.Registration;

namespace UniversalLIMS.ViewModels.Registration;

public sealed class OrdersIndexViewModel
{
    public required OrderFilter Filter { get; init; }

    public required PagedResult<OrderListItemDto> Result { get; init; }

    public static IReadOnlyList<(OrderStatus Value, string Label)> StatusOptions { get; } =
    [
        (OrderStatus.Draft, OrderStatusDisplay.ToUk(OrderStatus.Draft)),
        (OrderStatus.Registered, OrderStatusDisplay.ToUk(OrderStatus.Registered))
    ];
}
