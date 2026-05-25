using UniversalLIMS.Domain.Organization;
using UniversalLIMS.Domain.Registration;

namespace UniversalLIMS.Infrastructure.Registration;

public sealed class RegistrationRenderContext
{
    public required Order Order { get; init; }

    public required Customer Customer { get; init; }

    public required Branch Branch { get; init; }

    public required Sample Sample { get; init; }

    public IReadOnlyDictionary<string, string?> DynamicValuesByKey { get; init; }
        = new Dictionary<string, string?>(StringComparer.Ordinal);
}

public sealed class RegistrationFieldValueResolver
{
    private static readonly HashSet<string> ReservedStaticKeys =
    [
        "Customer.FullName",
        "Customer.OrganizationName",
        "Customer.ContactPhone",
        "Customer.Address",
        "Customer.Email",
        "Customer.Edrpou",
        "Customer.Rnokpp",
        "Branch.Code",
        "Branch.Name",
        "Sample.Number",
        "Sample.RegistrationNumber",
        "Sample.RegisteredAt",
        "Order.ReferralNumber"
    ];

    public string? Resolve(string? dataFieldKey, RegistrationRenderContext context)
    {
        if (string.IsNullOrWhiteSpace(dataFieldKey))
        {
            return null;
        }

        if (ReservedStaticKeys.Contains(dataFieldKey))
        {
            return ResolveStatic(dataFieldKey, context);
        }

        return context.DynamicValuesByKey.TryGetValue(dataFieldKey, out var dynamicValue)
            ? dynamicValue
            : null;
    }

    private static string? ResolveStatic(string dataFieldKey, RegistrationRenderContext context) =>
        dataFieldKey switch
        {
            "Customer.FullName" => context.Customer.FullName,
            "Customer.OrganizationName" => context.Customer.OrganizationName,
            "Customer.ContactPhone" => context.Customer.ContactPhone,
            "Customer.Address" => context.Customer.Address,
            "Customer.Email" => context.Customer.Email,
            "Customer.Edrpou" => context.Customer.Edrpou,
            "Customer.Rnokpp" => context.Customer.Rnokpp,
            "Branch.Code" => context.Branch.Code,
            "Branch.Name" => context.Branch.Name,
            "Sample.Number" => context.Sample.Number,
            "Sample.RegistrationNumber" => context.Sample.Number,
            "Sample.RegisteredAt" => context.Sample.RegisteredAt.ToString("dd.MM.yyyy HH:mm"),
            "Order.ReferralNumber" => context.Order.ReferralNumber,
            _ => null
        };
}
