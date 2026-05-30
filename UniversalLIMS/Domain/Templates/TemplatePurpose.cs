namespace UniversalLIMS.Domain.Templates;

/// <summary>
/// Role of a PDF template in the order workflow (REF vs protocol vs expert conclusion).
/// </summary>
public enum TemplatePurpose
{
    Referral = 1,
    Protocol = 2,
    Conclusion = 3
}
