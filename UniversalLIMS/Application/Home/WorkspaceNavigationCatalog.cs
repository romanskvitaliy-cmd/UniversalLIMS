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
                new("Головна", "Home", "Workspace", "bi-house-door-fill"),
                new("Нова проба", "Orders", "Create", "bi-plus-circle"),
                new("Реєстр", "Orders", "Index", "bi-journal-plus"),
                new("PDF Workspace", "PdfWorkspace", "Index", "bi-file-earmark-pdf"),
            ],
            LimsRoles.LaboratoryTechnician =>
            [
                new("Головна", "Home", "Workspace", "bi-house-door-fill"),
                new("Журнал проб", "Laboratory", "Index", "bi-droplet-half"),
            ],
            LimsRoles.Specialist =>
            [
                new("Головна", "Home", "Workspace", "bi-house-door-fill"),
                new("Черга експерта", "Expert", "Index", "bi-clipboard2-check"),
            ],
            _ => []
        };

    public static IReadOnlyList<WorkspaceQuickLinkVm> GetQuickLinks(string roleCode, bool isDevelopment = false) =>
        roleCode switch
        {
            LimsRoles.SystemAdministrator =>
            [
                Link("Лабораторії", "Огляд усіх філій і вхід у журнал проб", "bi-building",
                    "/Laboratories", true),
                Link("Філії", "Довідник назв і адрес лабораторних філій", "bi-diagram-3",
                    "/Branches", true),
                Link("Лабораторний журнал", "Проби в роботі з урахуванням обраного контексту", "bi-droplet-half",
                    "/Laboratory", true),
                Link("Шаблони документів", "Створення та публікація шаблонів", "bi-layout-text-window-reverse",
                    "/Templates", true),
                Link("PDF Workspace", "Заповнення PDF-полів замовлення", "bi-file-earmark-pdf",
                    "/PdfWorkspace", true),
                Link("Черга експерта", "Затвердження висновків по пробах", "bi-clipboard2-check",
                    "/Expert", true),
                Link("Перевірка фундаменту", "Діагностика системи (розробка)", "bi-tools",
                    "/Diagnostics/Foundation", true),
            ],
            LimsRoles.Registrar =>
            [
                Link("Прийом проб", "Створити замовлення, пробу, направлення та обрати бланки", "bi-clipboard2-plus",
                    "/Orders/Create", true),
                Link("Реєстр замовлень", "Пошук, статуси, маршрути та направлення", "bi-journal-plus",
                    "/Orders", true),
                Link("PDF Workspace", "Перевірка та ручне заповнення PDF-бланків", "bi-file-earmark-pdf",
                    "/PdfWorkspace", true),
            ],
            LimsRoles.LaboratoryTechnician =>
            [
                Link("Лабораторний журнал", "Список проб філії з фільтрами", "bi-droplet-half",
                    "/Laboratory", true),
                Link("Результати", "Оберіть пробу в журналі → заповнення PDF", "bi-clipboard2-pulse",
                    "/Laboratory", true),
            ],
            LimsRoles.Specialist =>
            [
                Link("Черга експерта", "Проби з внесеними результатами — висновок у PDF", "bi-clipboard2-check",
                    "/Expert", true),
                Link("Затверджені", "Архів затверджених висновків", "bi-patch-check",
                    "/Expert?reviewStatus=2", true),
            ],
            _ => []
        };

    private static IReadOnlyList<WorkspaceNavItem> BuildAdminNav(bool isDevelopment)
    {
        var items = new List<WorkspaceNavItem>
        {
            new("Головна", "Home", "Workspace", "bi-house-door-fill"),
            new("Лабораторії", "Laboratories", "Index", "bi-building"),
            new("Філії", "Branches", "Index", "bi-diagram-3"),
            new("Журнал проб", "Laboratory", "Index", "bi-droplet-half"),
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
