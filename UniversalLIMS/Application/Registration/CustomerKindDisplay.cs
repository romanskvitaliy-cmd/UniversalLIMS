using UniversalLIMS.Domain.Registration;

namespace UniversalLIMS.Application.Registration;

public static class CustomerKindDisplay
{
    public static string ToUk(CustomerKind kind) =>
        kind switch
        {
            CustomerKind.Individual => "Фізична особа",
            CustomerKind.LegalEntity => "Юридична особа",
            _ => kind.ToString()
        };
}
