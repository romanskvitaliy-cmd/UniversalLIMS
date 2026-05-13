# UniversalLIMS

UniversalLIMS — лабораторна інформаційна система для центрів контролю та профілактики хвороб.

## Технічний стек

- .NET 8
- ASP.NET Core MVC
- ASP.NET Core Identity
- Entity Framework Core
- SQL Server / LocalDB для розробки

## Поточний стан

Реалізовано фундамент Етапу 1:

- філійність через `Branch`;
- користувачі через `ApplicationUser`;
- ролі LIMS через ASP.NET Core Identity Roles;
- `AuditLog` для audit trail;
- soft annulment через `ISoftAnnulled`;
- EF Core interceptors для audit trail та анулювання;
- системний словник `DataField`;
- seed ролей, філій і базових системних полів;
- dev-only сторінка перевірки фундаменту.

Також реалізовано legacy MVP Етапу 2 (Word/.docx) та зафіксовано перехід на цільовий контур PDF + Overlay:

- `Template`, `TemplateVersion`, `TemplateField`, `TemplateFieldPermission`;
- завантаження версій шаблонів та збереження документів у захищеному storage;
- legacy-підтримка `.docx` з читанням Word Content Controls із заповненим `Tag`;
- мапінг Content Control tags на `DataField`;
- створення нових `DataField` з невідомих tags;
- контроль `MaxLength`, орієнтовної місткості поля, кількості рядків і overflow policy;
- field-level security по ролях Identity;
- validation перед публікацією версії.
- soft annulment для версій шаблонів;
- dev-only діагностика конструктора шаблонів;
- автоматизовані тести для критичних інфраструктурних сценаріїв.

Деталі:

- [docs/stage-1-foundation.md](docs/stage-1-foundation.md)
- [docs/stage-2-template-constructor.md](docs/stage-2-template-constructor.md)
- [docs/stage-2-pdf-overlay.md](docs/stage-2-pdf-overlay.md)

## Команди

Встановлення локальних .NET tools:

```powershell
dotnet tool restore
```

Збірка:

```powershell
dotnet build .\UniversalLIMS.sln
```

Застосування міграцій:

```powershell
dotnet tool run dotnet-ef database update --project .\UniversalLIMS\UniversalLIMS.csproj --startup-project .\UniversalLIMS\UniversalLIMS.csproj
```

Запуск:

```powershell
dotnet run --project .\UniversalLIMS\UniversalLIMS.csproj
```

## Діагностика

У режимі `Development` доступна сторінка:

```text
/Diagnostics/Foundation
```

Вона показує стан ролей, філій, словника полів, audit trail та міграцій.

Для Етапу 2 в режимі `Development` доступна сторінка:

```text
/Diagnostics/TemplateConstructor
```

Вона показує стан шаблонів, версій, Content Control fields, permissions та неприв'язаних required полів.

## Тести

```powershell
dotnet test .\UniversalLIMS.sln
```

## Ліцензування

Платні бібліотеки не додано. `Syncfusion.DocIO` не підключено до окремого ліцензійного погодження; поточний reader Content Controls працює тільки для read-only аналізу `.docx`, не для генерації документів.
