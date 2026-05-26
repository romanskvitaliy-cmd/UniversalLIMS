namespace UniversalLIMS.Application.Registration.Abstractions;

public interface IOrderFieldLinkService
{
    Task<OrderFieldMappingPrepareDto> GetMappingPrepareAsync(
        IReadOnlyList<Guid> templateVersionIds,
        CancellationToken cancellationToken = default);

    Task SaveFieldLinkGroupsAsync(
        Guid orderId,
        IReadOnlyList<OrderFieldLinkGroupInput> groups,
        CancellationToken cancellationToken = default);

    Task ApplySharedFieldValuesAsync(
        Guid orderId,
        IReadOnlyList<OrderFieldLinkGroupInput> groups,
        IReadOnlyList<OrderSharedFieldValueInput> sharedValues,
        CancellationToken cancellationToken = default);

    Task<OrderFieldLinkGroupsDetailDto> GetFieldLinkGroupsForOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OrderFieldMappingSourceOrderDto>> GetFieldMappingSourceOrdersAsync(
        int take = 20,
        CancellationToken cancellationToken = default);

    Task<OrderFieldMappingAdaptResultDto> AdaptFieldLinkGroupsFromOrderAsync(
        Guid sourceOrderId,
        IReadOnlyList<Guid> targetTemplateVersionIds,
        CancellationToken cancellationToken = default);
}
