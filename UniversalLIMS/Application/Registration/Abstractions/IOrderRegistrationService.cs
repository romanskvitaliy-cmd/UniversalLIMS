using UniversalLIMS.Application.Common;

namespace UniversalLIMS.Application.Registration.Abstractions;

public interface IOrderRegistrationService
{
    Task<PagedResult<OrderListItemDto>> GetOrdersAsync(
        OrderFilter filter,
        CancellationToken cancellationToken = default);

    Task<OrderCreateFormDto> GetCreateFormAsync(CancellationToken cancellationToken = default);

    Task<CreateOrderResult> CreateOrderAsync(
        CreateOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<OrderDetailDto?> GetOrderDetailAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task SendDocumentsToLabAsync(
        SendOrderDocumentsRequest request,
        CancellationToken cancellationToken = default);

    Task UpdateDocumentRoutingAsync(
        UpdateOrderDocumentRoutingRequest request,
        CancellationToken cancellationToken = default);

    Task<AppendOrderSamplesResult> AppendSamplesAsync(
        AppendOrderSamplesRequest request,
        CancellationToken cancellationToken = default);
}
