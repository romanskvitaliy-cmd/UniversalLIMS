using UniversalLIMS.Application.Security;

namespace UniversalLIMS.Application.Home;

public sealed record RolePortalDefinition(
    string RoleCode,
    string DisplayName,
    string AccentColor,
    string AccentRgb,
    string IconClass,
    string Description);

public static class RolePortalCatalog
{
    public static readonly IReadOnlyList<RolePortalDefinition> All =
    [
        new(LimsRoles.SystemAdministrator, "Адміністратор", "#6366f1", "99, 102, 241", "bi-shield-lock",
            "Шаблони, користувачі, налаштування системи"),
        new(LimsRoles.Registrar, "Реєстратор", "#0ea5e9", "14, 165, 233", "bi-file-earmark-medical",
            "Прийом проб і замовлення"),
        new(LimsRoles.LaboratoryTechnician, "Лаборант", "#10b981", "16, 185, 129", "bi-droplet-half",
            "Лабораторний журнал і проби"),
        new(LimsRoles.Specialist, "Експерт", "#f59e0b", "245, 158, 11", "bi-clipboard2-check",
            "Протоколи та висновки")
    ];

    public static RolePortalDefinition? FindByRoleCode(string? roleCode)
    {
        if (string.IsNullOrWhiteSpace(roleCode))
        {
            return null;
        }

        foreach (var definition in All)
        {
            if (string.Equals(definition.RoleCode, roleCode, StringComparison.Ordinal))
            {
                return definition;
            }
        }

        return null;
    }

    public static string? GetDisplayName(string? roleCode) => FindByRoleCode(roleCode)?.DisplayName;
}
