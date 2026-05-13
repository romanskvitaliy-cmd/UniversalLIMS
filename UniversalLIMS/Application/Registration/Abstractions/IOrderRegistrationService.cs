using UniversalLIMS.Application.Registration;

namespace UniversalLIMS.Application.Registration.Abstractions;

public interface IOrderRegistrationService
{
    Task<RegisterSampleResult> RegisterSampleAsync(
        RegisterSampleRequest request,
        CancellationToken cancellationToken = default);

    Task<OrderDetailsResult?> GetOrderDetailsAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task RouteSampleAsync(Guid sampleId, CancellationToken cancellationToken = default);
}
