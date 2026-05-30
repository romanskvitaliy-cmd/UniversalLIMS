namespace UniversalLIMS.Application.Identity;

/// <summary>Узгоджені правила облікових записів філій (логін = код філії).</summary>
public static class BranchPortalAccountConventions
{
    public const string EmailDomain = "@filia.lims";

    public static string BuildEmail(string branchCode) =>
        $"{branchCode.Trim().ToLowerInvariant()}{EmailDomain}";

    public static string BuildDefaultPassword(string branchCode) =>
        $"Filial{branchCode.Trim().ToUpperInvariant()}1!";

    public static string BuildFullName(string branchCity) =>
        $"Філія {branchCity.Trim()}";

    public static bool IsBranchPortalEmail(string? email) =>
        !string.IsNullOrWhiteSpace(email)
        && email.Trim().EndsWith(EmailDomain, StringComparison.OrdinalIgnoreCase);
}
