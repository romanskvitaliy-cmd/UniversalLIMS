using UniversalLIMS.Application.Registration;

namespace UniversalLIMS.Application.Registration.Abstractions;

public interface ICustomerService
{
    Task<IReadOnlyList<CustomerSearchResult>> SearchAsync(
        string? query,
        int take = 20,
        CancellationToken cancellationToken = default);

    Task<CustomerSearchResult?> GetAsync(Guid customerId, CancellationToken cancellationToken = default);

    Task<Guid> CreateAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default);

    Task UpdateAsync(Guid customerId, UpdateCustomerRequest request, CancellationToken cancellationToken = default);

    Task AnnulAsync(Guid customerId, string reason, CancellationToken cancellationToken = default);
}
