namespace UniversalLIMS.Domain.Organization;

public static class BranchKindRules
{
    public static BranchKind InferFromCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return BranchKind.Laboratory;
        }

        var normalized = code.Trim().ToUpperInvariant();

        if (normalized.StartsWith("REG-", StringComparison.Ordinal)
            || string.Equals(normalized, "REG", StringComparison.Ordinal))
        {
            return BranchKind.Registration;
        }

        if (normalized.StartsWith("LAB-", StringComparison.Ordinal))
        {
            return BranchKind.Laboratory;
        }

        if (normalized.StartsWith("EXP-", StringComparison.Ordinal)
            || string.Equals(normalized, "EXP", StringComparison.Ordinal))
        {
            return BranchKind.Expert;
        }

        if (normalized.StartsWith("MIX-", StringComparison.Ordinal))
        {
            return BranchKind.Mixed;
        }

        return BranchKind.Laboratory;
    }
}
