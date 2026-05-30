namespace UniversalLIMS.Domain.Templates;

public static class TemplatePurposeRules
{
    public static TemplatePurpose InferFromCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return TemplatePurpose.Protocol;
        }

        var normalized = code.Trim().ToUpperInvariant();

        if (normalized.StartsWith("REF-", StringComparison.Ordinal)
            || normalized.StartsWith("REF_", StringComparison.Ordinal)
            || string.Equals(normalized, "REF", StringComparison.Ordinal))
        {
            return TemplatePurpose.Referral;
        }

        if (normalized.StartsWith("CONCLUSION-", StringComparison.Ordinal)
            || normalized.StartsWith("CONCLUSION_", StringComparison.Ordinal)
            || string.Equals(normalized, "CONCLUSION", StringComparison.Ordinal))
        {
            return TemplatePurpose.Conclusion;
        }

        return TemplatePurpose.Protocol;
    }
}
