# Етап 2: Конструктор шаблонів (Legacy Word MVP)

> Увага: цей документ фіксує вже реалізований MVP на базі `.docx` і Word Content Controls.
> Поточний цільовий напрямок проєкту змінено на `PDF + Overlay` і описано окремо у `docs/stage-2-pdf-overlay.md`.

Етап додає адміністративний MVP для керування оригінальними `.docx` шаблонами, їх версіями, Word Content Controls, мапінгом на `DataField` та field-level security по ролях Identity.

## Реалізовано

- Доменні сутності: `Template`, `TemplateVersion`, `TemplateField`, `TemplateFieldPermission`.
- Бібліотека тегів через `DataField` з `MaxLength`, `Format`, `ValidationRegex`, `ExampleValue`.
- Версійність `Template -> TemplateVersion` з immutable правилом для published версій на рівні application services.
- EF Core конфігурації, індекси, `RowVersion`, query filters для soft annulment.
- Локальне непублічне сховище файлів у `App_Data/TemplateDocuments`.
- Read-only витягування Word Content Controls із `.docx` через ZIP/XML без Syncfusion.DocIO.
- Application contracts: `IDocxContentControlReader`, `ITemplateDocumentStorage`, `ITemplatePublicationValidator`, `ITemplateVersionService`, `ITemplateFieldMappingService`.
- MVC екрани українською під policy `ManageSystem`.
- Анулювання версій шаблонів через soft annulment з обов'язковою причиною.
- Dev-only діагностика `/Diagnostics/TemplateConstructor`.
- Сторінка версії шаблону та мапінгу полів: кнопка **«Відкрити в Word для редагування»** формує посилання `ms-word:ofe|u|…` на HTTPS URL з **одноразовим короткоживучим токеном** (`OpenOriginalForWord`), бо десктопний Word не передає cookies браузерної сесії. Запасний варіант — **«Завантажити .docx»**.
- Міграція `ReplaceBookmarksWithTemplateFields` видаляє застарілі таблиці `TemplateBookmarks` / `BookmarkPermissions` і замінює їх на `TemplateFields` / `TemplateFieldPermissions`.

## Важливі обмеження

- Генерація документів не реалізована в цьому етапі.
- HTML/CSS/Razor генерація документів не використовується.
- Syncfusion.DocIO не підключено через ліцензійний ризик. Межа adapter вже підготовлена для майбутнього погодженого підключення.
- Поточний `ZipDocxContentControlReader` призначений тільки для читання Word Content Controls, не для pixel-perfect generation.

## Статус етапу

- Етап реалізовано як проміжний MVP та переведено в legacy-контур.
- Нові доробки конструктора мають виконуватись у парадигмі `PDF + Overlay`.

## Оновлення PDF Overlay (поточний стан)

> Нижче зафіксовано останні UX-доробки в `TemplateFields/Map` для конструктора `PDF + Overlay`.

- Реалізовано `Smart Guides` під час drag активного тега (тонкі лінії вирівнювання по X/Y, а також по краях сторінки).
- Реалізовано `Smart Snap` до сусідніх тегів і країв сторінки (поріг 7px), з перемикачем `Smart Snap` на пульті.
- Стан пульта збережується в `localStorage`, включно з `Smart Snap`, кроком nudge, collapse-станом секцій і позицією.
- Додано компактні кнопки розміру у форматі `значення + знак` (наприклад, `172+`, `32-`) із візуальним badge `+/-`.
- Для кнопок `Рух` і `Розмір` додано режим утримання (press-and-hold) з автоповтором.
- Позиціонування пульта захищено від виходу за межі екрана навіть у manual-режимі.
- У debug-рядку додано індикатор `snap=on/off` для швидкого QA.

## Мінімальний workflow

1. Адміністратор відкриває `Шаблони`.
2. Створює `Template`.
3. Завантажує оригінальний `.docx`, що створює draft `TemplateVersion`.
4. Система читає Word Content Controls із заповненим `Tag` і створює `TemplateField`.
5. Адміністратор мапить tags на активні `DataField` або створює новий `DataField` з невідомого tag.
6. Адміністратор задає місткість поля: `EstimatedCapacityChars`, `MaxLines`, `AllowMultiline`, `OverflowPolicy`.
7. Адміністратор налаштовує доступ ролей до кожного поля.
8. Публікація блокується, якщо не виконані production validation rules.
9. Якщо версію потрібно вивести з обігу, адміністратор анулює її з причиною; фізичного видалення немає.

## Validation перед публікацією

- Версія має бути `Draft` або `ReadyForPublication`.
- Оригінальний файл існує у сховищі.
- SHA-256 файлу збігається з metadata версії.
- У версії є Word Content Controls із `Tag`.
- Кожен required `TemplateField` прив'язаний до активного `DataField`.
- Для кожного `TemplateField` налаштовані права для всіх LIMS ролей.
- Немає duplicate normalized tags.
- Якщо `OverflowPolicy = Block`, `DataField.MaxLength` не має перевищувати `TemplateField.EstimatedCapacityChars`.

## Перевірки

```powershell
dotnet test .\UniversalLIMS.sln
dotnet build .\UniversalLIMS.sln
dotnet ef database update --project .\UniversalLIMS\UniversalLIMS.csproj --startup-project .\UniversalLIMS\UniversalLIMS.csproj
```
