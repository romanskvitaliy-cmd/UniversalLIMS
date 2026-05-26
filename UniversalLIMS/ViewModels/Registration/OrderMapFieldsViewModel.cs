using UniversalLIMS.Application.Registration;

namespace UniversalLIMS.ViewModels.Registration;

public sealed class OrderMapFieldsViewModel
{
    public required OrderCreateFormDto Form { get; init; }

    public required OrderCreateInputModel CreateInput { get; init; }

    public required OrderFieldMappingPrepareDto Mapping { get; init; }

    public IReadOnlyList<OrderFieldMappingSourceOrderDto> CopySourceOrders { get; init; } = [];
}
