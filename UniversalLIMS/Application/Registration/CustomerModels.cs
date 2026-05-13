using UniversalLIMS.Domain.Registration;

namespace UniversalLIMS.Application.Registration;

public sealed class CustomerSearchResult
{
    public Guid Id { get; init; }

    public string FullName { get; init; } = string.Empty;

    public string? OrganizationName { get; init; }

    public string? ContactPhone { get; init; }

    public string? Edrpou { get; init; }
}

public class CreateCustomerRequest
{
    public CustomerKind Kind { get; init; }

    public string FullName { get; init; } = string.Empty;

    public string? OrganizationName { get; init; }

    public string? ContactPhone { get; init; }

    public string? Email { get; init; }

    public string? Address { get; init; }

    public string? Edrpou { get; init; }

    public string? Rnokpp { get; init; }

    public string? Notes { get; init; }
}

public sealed class UpdateCustomerRequest : CreateCustomerRequest;
