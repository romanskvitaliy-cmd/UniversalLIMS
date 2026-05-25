using UniversalLIMS.Application.Security;

namespace UniversalLIMS.Application.Home;

public sealed record WorkspaceNavItem(
    string Title,
    string Controller,
    string Action,
    string IconClass);

public static class WorkspaceNavigationCatalog
{
    public static IReadOnlyList<WorkspaceNavItem> GetNavItems(string roleCode, bool isDevelopment = false) =>
        roleCode switch
        {
            LimsRoles.SystemAdministrator => BuildAdminNav(isDevelopment),
            LimsRoles.Registrar =>
            [
                new("Кабінет", "Home", "Workspace", "bi-house-door"),
                new("Замовлення", "Orders", "Index", "bi-journal-plus"),
                new("PDF Workspace", "PdfWorkspace", "Index", "bi-file-earmark-pdf"),
            ],
            LimsRoles.LaboratoryTechnician =>
            [
                new("Кабінет", "Home", "Workspace", "bi-house-door"),
            ],
            LimsRoles.Specialist =>
            [
                new("Кабінет", "Home", "Workspace", "bi-house-door"),
            ],
            _ => []
        };

    public static IReadOnlyList<WorkspaceQuickLinkVm> GetQuickLinks(string roleCode) =>
        roleCode switch
        {
            LimsRoles.SystemAdministrator =>
            [
                Link("Шаблони документів", "Створення та публікація шаблонів", "bi-layout-text-window-reverse",
                    "/Templates", true),
                Link("PDF Workspace", "Заповнення PDF-полів замовлення", "bi-file-earmark-pdf",
                    "/PdfWorkspace", true),
                Link("Перевірка фундаменту", "Діагностика системи (розробка)", "bi-tools",
                    "/Diagnostics/Foundation", true),
            ],
            LimsRoles.Registrar =>
            [
                Link("PDF Workspace", "Прийом проб і заповнення направлень", "bi-file-earmark-pdf",
                    "/PdfWorkspace", true),
                Link("Замовлення", "Реєстр замовлень і направлень", "bi-journal-plus",
                    "/Orders", true),
            ],
            LimsRoles.LaboratoryTechnician =>
            [
                Link("Лабораторний журнал", "Облік проб (незабаром)", "bi-droplet-half",
                    null, false),
                Link("Результати", "Внесення результатів (незабаром)", "bi-clipboard2-pulse",
                    null, false),
            ],
            LimsRoles.Specialist =>
            [
                Link("Протоколи", "Перегляд протоколів (незабаром)", "bi-clipboard2-check",
                    null, false),
                Link("Висновки", "Затвердження висновків (незабаром)", "bi-patch-check",
                    null, false),
            ],
            _ => []
        };

    private static IReadOnlyList<WorkspaceNavItem> BuildAdminNav(bool isDevelopment)
    {
        var items = new List<WorkspaceNavItem>
        {
            new("Кабінет", "Home", "Workspace", "bi-house-door"),
            new("Шаблони", "Templates", "Index", "bi-layout-text-window-reverse"),
            new("PDF Workspace", "PdfWorkspace", "Index", "bi-file-earmark-pdf"),
        };

        if (isDevelopment)
        {
            items.Add(new("Перевірка фундаменту", "Diagnostics", "Foundation", "bi-tools"));
            items.Add(new("Перевірка шаблонів", "Diagnostics", "TemplateConstructor", "bi-bug"));
        }

        return items;
    }

    private static WorkspaceQuickLinkVm Link(
        string title,
        string description,
        string icon,
        string? url,
        bool available) =>
        new()
        {
            Title = title,
            Description = description,
            IconClass = icon,
            Url = url,
            IsAvailable = available
        };
}
