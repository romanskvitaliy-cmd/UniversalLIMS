# Етап 1: Архітектурний Фундамент

## Мета

Етап 1 закладає production-ready основу UniversalLIMS для подальших модулів: конструктора шаблонів, реєстрації проб, лабораторного журналу та генерації документів.

Головний принцип: бізнес-дані не повинні видалятися фізично, а всі зміни мають бути трасовані.

## Структура

Код організовано як модульний моноліт:

```text
UniversalLIMS/
  Application/
  Domain/
  Infrastructure/
  Data/Migrations/
  Controllers/
  Views/
```

`Domain` містить бізнес-сутності та контракти. `Application` містить абстракції й авторизаційні константи. `Infrastructure` містить EF Core, seed, interceptors та сервіси.

## Ключові сутності

- `ApplicationUser` — користувач Identity з ПІБ, посадою, філією та активністю.
- `Branch` — філія, що використовується для майбутньої фільтрації доступу.
- `DataField` — єдиний словник даних для мапінгу Word Content Control tags.
- `AuditLog` — append-only audit trail.
- `BaseEntity` — базові audit-поля та `RowVersion`.
- `ISoftAnnulled` — контракт анулювання з причиною.

## Audit Trail

Audit реалізовано через `AuditSaveChangesInterceptor`.

Фіксується:

- користувач;
- ПІБ користувача;
- філія;
- дія;
- назва сутності;
- ідентифікатор сутності;
- змінені поля;
- старі значення;
- нові значення;
- причина;
- correlation id;
- IP;
- user agent;
- UTC timestamp.

Seed-операції маркуються як `Seeded` через `ISystemOperationContext`.

## Soft Annulment

Soft delete трактовано як юридичне анулювання.

`SoftAnnulmentSaveChangesInterceptor` перехоплює `EntityState.Deleted` для сутностей `ISoftAnnulled` і переводить його в `Modified`, виставляючи:

- `IsAnnulled = true`;
- `AnnulledAtUtc`;
- `AnnulledByUserId`;
- `AnnulmentReason`.

Анулювання без причини заборонене.

## Ролі та політики

Ролі реалізовані через ASP.NET Core Identity Roles:

- `SystemAdministrator`;
- `Registrar`;
- `LaboratoryTechnician`;
- `Specialist`.

Policy-based authorization підготовлена для майбутніх MVC-модулів:

- `ManageSystem`;
- `RegisterSamples`;
- `EnterLaboratoryResults`;
- `ApproveConclusions`.

Field-Level Security не реалізується через claims. У наступному етапі вона має бути окремою матрицею прав для полів шаблону і ролей.

## Seed

Seed виконується при старті застосунку через `SeedLimsAsync()`.

Створюється:

- 4 ролі LIMS;
- 3 філії: `ZHY`, `BER`, `KOR`;
- базові системні `DataField`.

Seed є idempotent: повторний запуск не дублює дані.

## Діагностика

У `Development` доступна сторінка:

```text
/Diagnostics/Foundation
```

Вона дозволяє перевірити:

- кількість ролей;
- кількість філій;
- кількість полів даних;
- кількість audit-записів;
- застосовані та pending міграції;
- поведінку audit trail;
- soft annulment через тестові поля.

## Поточний результат

Фундамент Етапу 1 готовий до переходу на Етап 2 — Конструктор шаблонів.

Перед підключенням бібліотек для `.docx` потрібно окремо погодити ліцензійні умови, щоб не додати платну залежність без явного рішення.
