# Handoff: продовження реалізації (новий агент)

> Оновлено: 2026-05-27  
> Репозиторій: `UniversalLIMS` (.NET 8 MVC)  
> Мова UI: **українська**  
> Робоча гілка: `main` (локально попереду `origin/main`)

Цей документ — **єдиний entrypoint** для наступного агента після сесії Fill + бібліотека текстів + експерт (етап 3, частина 1).

---

## 0. Обов’язкове правило для агента: commit message

Після **кожної** логічної реалізації агент **завжди** дає користувачу готовий текст коміту для **ручного** push у GitHub (користувач комітить сам).

Формат (кілька рядків, не лише заголовок):

```text
type(scope): короткий заголовок українською або англійською

- пункт 1: що змінено
- пункт 2: навіщо / який ефект
- пункт 3: тести або docs, якщо є
```

Приклад (зразок стилю):

```text
feat(registration): group order details by sample

- OrderDocumentItemDto тепер має SampleId
- OrderRegistrationService.GetOrderDetailsAsync повертає всі проби замовлення
- Views/Orders/Details.cshtml групує документи по пробах
```

Типи: `feat`, `fix`, `docs`, `test`, `refactor` (refactor — лише за потреби, мінімально).

**Не комітити:** `debug-40b8bf.log`, `_runout.txt`, `_runerr.txt`, `.vs/`, `bin/`, `obj/`.

**Не робити commit/push від імені агента**, якщо користувач явно не попросив.

---

## 1. Що вже закрито (не переробляти без запиту)

| Блок | Статус | Орієнтир у git |
|------|--------|----------------|
| Бібліотека текстів tag-first (`NormalizedTag`) | Закрито | `b5ac5fa`, `7090918` |
| PDF Fill: autosave layout + values | Закрито | `2a3f2b2`, `558f8a3` |
| UI бібліотеки (toolbar tag) | Закрито | `d0a78d8` |
| QA-док на завтра | Є | `8eb0207`, `docs/qa-tomorrow-fill-tags-checklist.md` |
| Експерт: черга + approve + notes + return to queue | Закрито (MVP) | `d5d3c32`, `5fa0300`, `adcb1fe` |

### Ключові правила домену (не ламати)

- **Значення замовлення** → `OrderFieldValue` / `DataFieldId` (семантика Fill).
- **Макет PDF** → `TemplateField` + `TemplateFieldSegment` для `templateVersionId`.
- **Бібліотека текстів** → ключ **`TemplateField.Tag` / `NormalizedTag`**, не спільний `DataFieldId` для протокольних тегів (`f327_*`, `Food_*`).
- **Лабораторія** → тільки PDF Workspace, **без** повернення «Показники» / `ResultEntry`.
- **Soft delete** → `ISoftAnnulled`, не фізичний DELETE.

---

## 2. Git: останні коміти (контекст)

```
adcb1fe feat(expert-queue): додати явне повернення проби в чергу та індикатор старту розгляду
5fa0300 feat(expert-queue): покращити UX приміток і стабільність approve notes
d5d3c32 feat(expert): add approval notes flow in expert review queue
8eb0207 docs(qa): add tomorrow checklist for fill tags and roles
d0a78d8 fix(pdf-fill): align library toolbar tag and count layout
558f8a3 docs(pdf-fill): document layout autosave and close library stage
2a3f2b2 fix(pdf-fill): autosave layout together with main save
7090918 docs(library): align handoffs with tag-first scope
b5ac5fa fix(library): tag-first scope for field text library in PDF Fill
```

На момент handoff **working tree чистий** — наступні зміни = нові коміти.

---

## 3. Етап 3 (Експерт) — що вже є в коді

### Маршрути (`ExpertController`, policy `ApproveConclusions`)

| Method | URL / дія | Призначення |
|--------|-----------|-------------|
| GET | `/Expert` | Черга проб (фільтри, пагінація) |
| GET | `/Expert/Review?sampleId=` | Відкрити PDF Fill для висновку; `MarkInProgress` |
| POST | `/Expert/Approve` | Затвердити з опційною `notesUk` (modal + швидкий ✓) |
| POST | `/Expert/ReturnToQueue` | `InProgress` → `PendingReview` |

### UI (`Views/Expert/Index.cshtml`)

- Фільтри: номер проби, дати, статус розгляду, **пошук у примітках** (`notesContainsUk`).
- Колонки: статус (+ час затвердження / час старту для «В роботі»), примітка (preview + розгортання).
- Дії: **PDF**, **Затвердити** (modal), швидке затвердження, **Повернути в чергу** (для InProgress).

### Домен

- `ExpertConclusionReview` + `ExpertConclusionStatus` (`PendingReview`, `InProgress`, `Approved`).
- `ExpertReviewQueueService` — проби, де всі не-Pending документи в статусі `ResultsEntered`.
- `ExpertPdfFillService` — цілі Fill для полів Conclusion scope.
- `ExpertConclusionService` — MarkInProgress, Approve, ReturnToPendingReview; notes trim/null/max 2000.

### Тести (цільовий фільтр)

```powershell
dotnet test UniversalLIMS.Tests/UniversalLIMS.Tests.csproj --filter "FullyQualifiedName~Expert"
```

Очікування: усі Expert-тести зелені (на момент handoff — 10+ passed).

### Навігація

- `WorkspaceNavigationCatalog` — пункт «Черга експерта» → `/Expert`.

---

## 4. Що робити далі (пріоритети)

### A. Завтрашній фокус користувача (підтримка, не новий модуль)

Користувач завтра: **ручне тестування**, **заповнення шаблонів**, **розмітка тегами**, **набивання бібліотеки**.

Агенту:

1. Не блокувати це великими фічами.
2. Виправляти лише **критичні** баги з QA.
3. Допомагати **контентом тегів** (seed уже є в `ProtocolTagCatalog` + `docs/data/protocol-tags-*.json`) — за потреби додати JSON → catalog, без зміни архітектури.

Чекліст QA: `docs/qa-tomorrow-fill-tags-checklist.md`.

### B. Етап 3 — наступні інкременти (після стабілізації)

Малими PR/комітами, кожен з commit message для користувача:

| # | Задача | Складність |
|---|--------|------------|
| B1 | Сторінка/блок **деталей проби для експерта** (read-only: документи, статуси, поточна примітка) | M |
| B2 | **Reject / потребує доопрацювання** (новий статус або reuse notes + flag) — лише якщо замовник підтвердить | M |
| B3 | Зв’язок Fill після save експертських полів → auto `MarkInProgress` / підказка «затвердити в черзі» | S |
| B4 | Експорт/друк фінального PDF з черги (лінк уже є в PdfWorkspace) — UX shortcut у черзі | S |
| B5 | Unit-тести на `ReturnToQueue` + фільтр `Approved` edge cases | S |

### C. Етап 4 (НЕ починати без явного OK)

- Повноцінні **протоколи / висновки PDF generation** як окремий workflow.
- Глобальний рефактор permissions / DataField.
- Повернення табличного ResultEntry.

---

## 5. Fill + теги + бібліотека (довідка для агента)

| Тема | Де читати |
|------|-----------|
| Панель Fill, layout save | `docs/handoff-pdf-fill-panel-and-template-lifecycle.md` |
| Збереження values | `docs/handoff-pdf-workspace-fill.md` |
| Tag-first library | `docs/spec-hybrid-tags-and-order-field-mapping.md` §4.4 |
| Каталог тегів (код) | `Infrastructure/Persistence/Seed/ProtocolTagCatalog.cs` |
| JSON джерела | `docs/data/protocol-tags-f327.json`, `food`, `f325`, … |
| Fill JS | `wwwroot/js/pdf-workspace-fill.js` |
| Library API | `PdfWorkspaceController` → `/Fill/{versionId}/library` |

Ліміти бібліотеки: 200 записів/тег/філія, body 4000, shortLabel 200.

---

## 6. Ролі (smoke перед новими фічами)

| Роль | Що перевірити |
|------|----------------|
| Реєстратор | `/Orders`, Create, Details, PDF Fill, бібліотека |
| Лаборант | `/Laboratory`, PDF Fill, send workflow |
| Експерт | `/Expert`, Review → Fill, Approve, ReturnToQueue |
| Адміністратор | `/Laboratories`, permissions Published |

**Не інжектити `ApplicationDbContext` в `ICurrentUserService`** (цикл DI).

---

## 7. Перевірки перед завершенням задачі агентом

```powershell
dotnet build UniversalLIMS/UniversalLIMS.csproj
dotnet test UniversalLIMS.Tests/UniversalLIMS.Tests.csproj
```

Якщо `UniversalLIMS.exe` locked — `taskkill /F /IM UniversalLIMS.exe` або build з `-p:UseAppHost=false`.

SSOT чекліст: `docs/handoff-next-agent.md` §7.

---

## 8. Стартовий prompt для нового агента (копіювати)

```text
Проєкт UniversalLIMS. Прочитай ОБОВ’ЯЗКОВО:
1) docs/handoff-continue-agent-expert-and-stabilization.md (цей файл — пріоритет)
2) docs/handoff-next-agent.md
3) docs/qa-tomorrow-fill-tags-checklist.md

Контекст: Fill + FieldTextLibrary (tag-first) і Expert queue MVP уже в main.
Working tree чистий. Користувач завтра тестує Fill/теги/бібліотеку.

Правила:
- Після КОЖНОЇ реалізації дай готовий багаторядковий commit message (feat/fix/docs + bullets) для ручного GitHub — користувач комітить сам.
- Не великий рефактор. Не етап 4 без OK. Не ResultEntry.
- Мінімальні focused diff.

Задача: [вставити: напр. B1 деталі проби експерта АБО критичний баг з QA АБО доповнення protocol-tags JSON].
```

---

## 9. Пов’язані файли (швидкий grep)

```
Controllers/ExpertController.cs
Infrastructure/Expert/*.cs
Application/Expert/**
Views/Expert/*.cs
ViewModels/Expert/*.cs
Domain/Laboratory/ExpertConclusion*.cs
UniversalLIMS.Tests/Expert/*.cs
Infrastructure/Registration/FieldTextLibraryService.cs
wwwroot/js/pdf-workspace-fill.js
Views/PdfWorkspace/Fill.cshtml
```

---

*Після закриття наступного інкременту оновити §4 цього файлу або додати рядок у `handoff-next-agent.md` §1.*
