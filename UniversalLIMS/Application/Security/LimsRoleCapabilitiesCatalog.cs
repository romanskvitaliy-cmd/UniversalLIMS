using UniversalLIMS.Application.Home;

namespace UniversalLIMS.Application.Security;

/// <summary>Довідник системних прав кожної робочої ролі LIMS (політики, модулі, контекст філії).</summary>
public sealed record LimsRoleCapabilityDefinition(
    string RoleCode,
    string DisplayName,
    string AccentColor,
    string IconClass,
    string Summary,
    IReadOnlyList<string> PolicyLabels,
    IReadOnlyList<string> ModuleLabels,
    string BranchScopeLabel);

public static class LimsRoleCapabilitiesCatalog
{
    public static readonly IReadOnlyList<LimsRoleCapabilityDefinition> All =
    [
        Build(
            LimsRoles.SystemAdministrator,
            "Повний доступ до системи: шаблони, філії, користувачі, усі журнали. Може перемикатися на будь-яку робочу роль.",
            ["Керування системою", "Реєстрація проб", "Лабораторні результати", "Затвердження висновків", "PDF Workspace"],
            ["Філії та лабораторії", "Користувачі та ролі", "Шаблони документів", "Реєстр замовлень", "Лабораторний журнал", "Черга експерта", "PDF Workspace"],
            "Усі філії (можна обрати контекст)"),
        Build(
            LimsRoles.Registrar,
            "Прийом проб, створення замовлень і заповнення PDF-бланків реєстратури.",
            ["Реєстрація проб", "PDF Workspace"],
            ["Нова справа", "Реєстр справ", "PDF Workspace"],
            "Лише філія користувача"),
        Build(
            LimsRoles.LaboratoryTechnician,
            "Лабораторний журнал проб і внесення результатів досліджень.",
            ["Лабораторні результати", "PDF Workspace"],
            ["Лабораторний журнал", "Заповнення PDF результатів"],
            "Лише філія користувача"),
        Build(
            LimsRoles.Specialist,
            "Експертна перевірка, формування та затвердження висновків.",
            ["Затвердження висновків", "PDF Workspace"],
            ["Черга експерта", "Архів висновків"],
            "Лише філія користувача")
    ];

    public static LimsRoleCapabilityDefinition? FindByRoleCode(string? roleCode)
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

    private static LimsRoleCapabilityDefinition Build(
        string roleCode,
        string summary,
        IReadOnlyList<string> policyLabels,
        IReadOnlyList<string> moduleLabels,
        string branchScopeLabel)
    {
        var portal = RolePortalCatalog.FindByRoleCode(roleCode);
        return new LimsRoleCapabilityDefinition(
            roleCode,
            portal?.DisplayName ?? roleCode,
            portal?.AccentColor ?? "#64748b",
            portal?.IconClass ?? "bi-person-badge",
            summary,
            policyLabels,
            moduleLabels,
            branchScopeLabel);
    }
}
