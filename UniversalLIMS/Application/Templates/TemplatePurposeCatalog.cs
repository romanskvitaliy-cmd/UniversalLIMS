using UniversalLIMS.Domain.Templates;

namespace UniversalLIMS.Application.Templates;

public static class TemplatePurposeCatalog
{
    public static string GetDisplayNameUk(TemplatePurpose purpose) =>
        purpose switch
        {
            TemplatePurpose.Referral => "Направлення (REF)",
            TemplatePurpose.Protocol => "Протокол",
            TemplatePurpose.Conclusion => "Висновок",
            _ => purpose.ToString()
        };

    public static IReadOnlyList<TemplatePurpose> All { get; } =
    [
        TemplatePurpose.Referral,
        TemplatePurpose.Protocol,
        TemplatePurpose.Conclusion
    ];
}
