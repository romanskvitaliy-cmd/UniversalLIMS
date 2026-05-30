namespace UniversalLIMS.Application.Identity;

/// <summary>Відомі системі паролі для перевірки через CheckPasswordAsync (хеш не розшифровується).</summary>
public static class UserKnownPasswordCatalog
{
    private static readonly IReadOnlyDictionary<string, string> DevTestUserPasswords =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["adminLIMS@gmail.com"] = "LIMS147",
            ["registrarLIMS@gmail.com"] = "LIMS258",
            ["labLIMS@gmail.com"] = "LIMS456",
            ["expertLIMS@gmail.com"] = "LIMS159"
        };

    public static IEnumerable<(string Password, string SourceLabel)> GetCandidates(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            yield break;
        }

        if (BranchPortalAccountConventions.IsBranchPortalEmail(email))
        {
            var code = email.Split('@')[0].Trim().ToUpperInvariant();
            yield return (BranchPortalAccountConventions.BuildDefaultPassword(code), "типовий пароль філії");
            yield return ($"Filial{code}!", "попередній формат пароля");
        }

        if (DevTestUserPasswords.TryGetValue(email.Trim(), out var testPassword))
        {
            yield return (testPassword, "тестовий обліковий запис");
        }
    }
}
