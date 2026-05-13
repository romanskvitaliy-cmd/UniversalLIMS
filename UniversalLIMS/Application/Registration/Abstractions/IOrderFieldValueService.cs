using UniversalLIMS.Application.Registration;

namespace UniversalLIMS.Application.Registration.Abstractions;

public interface IOrderFieldValueService
{
    Task UpsertAsync(
        Guid orderId,
        IReadOnlyList<OrderFieldValueInput> values,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OrderFieldValueInput>> GetAsync(
        Guid orderId,
        Guid? sampleId = null,
        CancellationToken cancellationToken = default);
}
