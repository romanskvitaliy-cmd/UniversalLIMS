# Handoff: PDF Fill — панель заповнення, макет шаблону, публікація версій

> **Для нового агента.** Оновлено: 2026-05-27  
> Репозиторій: `UniversalLIMS` (.NET 8, ASP.NET Core MVC).  
> Мова UI: українська.

Цей документ збирає **погоджену з замовником логіку** і **що вже реалізовано в коді** після сесії про PdfWorkspace Fill. Доповнює `handoff-pdf-workspace-fill.md` (збереження значень замовлення).

---

## 1. Мета продукту (коротко)

1. **Реєстратор** відкриває PDF бланк (`PdfWorkspace/Fill`), заповнює поля для **конкретного замовлення**.
2. Якщо текст «зливається з лінією» — під час заповнення **підправляє позицію/шрифт** і зберігає це в **макет поточної версії шаблону** (наприклад v49).
3. **Наступні замовлення** з **тим самим `templateVersionId`** уже бачать відточений макет; інші версії (v48, v50) — **ні**.
4. **Адміністратор** керує **правами полів** по ролях і **яка версія шаблону активна** для нової роботи.

---

## 2. Два рівні даних (критично не плутати)

| Що змінюємо | Куди в БД | Для кого |
|-------------|-----------|----------|
| **Значення поля** (текст протоколу) | `OrderFieldValue` (`StoredValue`, ключ через `DataField` / `TemplateField.Id`) | Лише **поточне замовлення** |
| **Макет поля** (offset X/Y, шрифт, вирівнювання, line-height, B/I/U у `SvgPathData`) | `TemplateField` + `TemplateFieldSegment` для **`templateVersionId`** | Усі **майбутні** Fill/Final PDF **цієї версії** |

### Дві кнопки збереження в UI (вкладка «Дії» панелі Fill)

| Кнопка | API | Призначення |
|--------|-----|-------------|
| **Зберегти значення (замовлення)** | `POST /PdfWorkspace/Fill/{templateVersionId}/values` | Текст полів → замовлення |
| **Зберегти макет у шаблон** | `POST /PdfWorkspace/Fill/{templateVersionId}/layout` | Позиція + оформлення → версія шаблону |

Додатково для значень: автозбереження (~2.5 с), `Ctrl+S`, індикатор «Збережено» / «Є незбережені зміни».

---

## 3. PDF Fill — панель (реалізовано)

### Де показується

Одна сторінка для всіх входів:

- `GET /PdfWorkspace/Fill/{templateVersionId}?orderId=`
- З реєстру: `Orders/Details` → «Заповнити»
- Лабораторія: `Laboratory/ChooseDocument` → Fill

### Вкладки панелі

| Вкладка | Зміст |
|---------|--------|
| **Текст** | Textarea значення (замовлення) + шрифт, розмір, B/I/U, line-height |
| **Позиція** | Offset X/Y, кнопки зсуву (−5…+5), гориз./верт. вирівнювання (як у Map калібруванні) |
| **Поля** | Список полів + пошук; клік → фокус на PDF |
| **Дії** | Автозбереження значень; обидві кнопки збереження; підказка про v{N} |

Зміни макету **одразу** перемальовують overlay (`mountOverlays` + `segmentLayoutOverrides` у JS).

### Ключові файли (Fill + панель)

| Файл | Роль |
|------|------|
| `Views/PdfWorkspace/Fill.cshtml` | Layout PDF + панель, JSON config |
| `wwwroot/js/pdf-workspace-fill.js` | PDF.js, панель, dirty-state, save values/layout |
| `wwwroot/css/lims-workspace.css` | Стилі `.pdf-fill-panel`, cal-групи |
| `Controllers/PdfWorkspaceController.cs` | `SaveValues`, **`SaveLayout`** |
| `Application/Registration/PdfWorkspaceFillLayoutModels.cs` | DTO layout save |
| `Infrastructure/Templates/TemplateFieldMappingService.cs` | **`SaveFillLayoutRefinementAsync`** |
| `Application/Registration/PdfWorkspaceFillSegmentDto.cs` | + `LineHeight`, `SvgPathData`, `SegmentRowVersion` |

### Стиль тексту в макеті

Префікс як у Map: `ULIMS_TEXT_STYLE:` + JSON у `TemplateFieldSegment.SvgPathData`  
(`b`, `i`, `u` — bold/italic/underline). Логіка дубльована в `pdf-workspace-fill.js`.

### RBAC на Fill

- Сегменти з `GetFillSegmentsAsync` — лише поля з **Read+** для **активної ролі** (`ITemplateFieldPermissionService`).
- **Write** потрібен для редагування **значень**; макет у панелі доступний при виборі поля (уточнення позиції — сценарій реєстратора під час fill).

---

## 4. Збереження макету з Fill (реалізовано, відмінність від Map)

### Проблема Map

`EnsureEditableTemplateVersion` / `EnsureDraftVersion` — **заборона змін layout** для **Published** у конструкторі Map.

### Рішення для Fill

`SaveFillLayoutRefinementAsync` дозволяє уточнювати макет для версій:

- `Draft`
- `ReadyForPublication`
- **`Published`** ← сценарій «відточити v49 у роботі»

**Не** для: `Superseded`, `Annulled`.

Оновлює **один сегмент** без `ProcessFieldSegmentsAsync` (щоб не видалити sibling-сегменти того ж поля).

```csharp
// ITemplateFieldMappingService
Task<PdfWorkspaceFillLayoutSaveResult> SaveFillLayoutRefinementAsync(
    Guid templateVersionId,
    IReadOnlyList<PdfWorkspaceFillLayoutFieldUpdate> updates, ...);
```

---

## 5. Версії шаблону та «активна» версія (погоджена логіка)

### Статуси `TemplateVersionStatus`

| Статус | Українською (UI) | Сенс |
|--------|------------------|------|
| `Draft` | Чернетка | Редагування Map, публікація |
| `ReadyForPublication` | Готовий до публікації | Можна опублікувати |
| `Published` | Опубліковано | **Поточна робоча** (якщо `CurrentPublishedVersionId`) |
| `Superseded` | Замінено | Була Published, її замінили новою |
| `Annulled` | Анульовано | Не використовувати |

### Публікація (`TemplateVersionService.PublishAsync`)

При публікації версії **V**:

1. `V.Status = Published`, `V.PublishedAtUtc = UtcNow`, `PublishedByUserId`
2. `Template.CurrentPublishedVersionId = V.Id`, `Template.Status = Active`
3. Усі **інші** `Published` того ж шаблону → **`Superseded`**

**Одночасно лише одна** «поточна» опублікована версія на шаблон (`CurrentPublishedVersionId`).

### Нові замовлення

`OrderRegistrationService.ResolveTemplateVersionIdAsync`:

- За замовчуванням — `template.CurrentPublishedVersionId` (Published, не annulled).
- Явний `templateVersionId` у документі — лише якщо це **саме** поточна Published для типу дослідження.

**Старі справи** тримають свій `OrderDocument.TemplateVersionId` (наприклад v49), навіть якщо активна вже v50.

### Права доступу до полів (адміністратор)

`UpdatePermissionsAsync` / `ArePermissionsEditable`:

- ✅ `Draft`, `ReadyForPublication`, **`Published`**
- ❌ `Superseded`, `Annulled`

Права **прив’язані до версії**, не до «шаблону загалом».  
UI: `Views/TemplateFields/Permissions.cshtml` — попередження для Published.

**Map (розміщення рамок на PDF):** лише Draft / ReadyForPublication.  
**Fill (уточнення offset/шрифту):** також **Published** — через `SaveFillLayoutRefinementAsync`.

---

## 6. Бібліотека текстів на тег (реалізовано 2026-05-26)

### Сенс

- **Філіальна** спільна бібліотека типових текстів (`FieldTextLibraryEntry`), ключ — `NormalizedTag` (`TemplateField.Tag`) у пріоритеті.
- `DataFieldId` використовується лише як fallback (legacy-записи / поля без валідного тега).
- **Не** змінює `OrderFieldValue` у старих справах при правці бібліотеки.
- Керування записами — ролі з **Write** на поле (та сама матриця, що для Fill).

### API

| Method | URL |
|--------|-----|
| GET | `/PdfWorkspace/Fill/{templateVersionId}/library?templateFieldId=&orderId=` |
| POST | `/PdfWorkspace/Fill/{templateVersionId}/library` |
| PUT | `/PdfWorkspace/Fill/{templateVersionId}/library/{entryId}` |
| DELETE | `/PdfWorkspace/Fill/{templateVersionId}/library/{entryId}?templateFieldId=&orderId=` |
| POST | `/PdfWorkspace/Fill/{templateVersionId}/library/{entryId}/use` |

`POST .../values` приймає опційно `libraryAdditions[]` (після успішного збереження значень, ідемпотентний upsert).

### UI (вкладка «Текст»)

- Searchable combo (пошук + вибір → textarea).
- У шапці панелі показується поточний тег поля (щоб явно бачити scope бібліотеки).
- ☐ «Додати до бібліотеки після збереження поля» (скидається після успіху).
- «➕ У бібліотеку» — одразу.
- «Керувати записами» — редагування / видалення.

### Файли

- `Domain/Registration/FieldTextLibraryEntry.cs`
- `Infrastructure/Registration/FieldTextLibraryService.cs`
- `Controllers/PdfWorkspaceController.cs` (library endpoints)
- `wwwroot/js/pdf-workspace-fill.js`, `Views/PdfWorkspace/Fill.cshtml`

---

## 7. Що НЕ реалізовано (беклог, погоджено як бажана логіка)

Замовник описав **нормальну** операційну модель; у коді **ще немає**:

### 7.1 Повторна активація старої версії (наприклад v5)

**Бажання:** адмін переглядає всі версії і в потрібний момент знову робить **активною** вже існуючу v5 (була Published → Superseded).

**Зараз:** `TemplatePublicationValidator` — публікувати можна **лише** `Draft` / `ReadyForPublication`.  
**Superseded** повторно **не** публікується однією кнопкою.

**Обхід зараз:** клон у нову чернетку (`CreateNewVersionAsync`) → публікація (новий номер версії / нові GUID полів).

**Пропозиція реалізації:**

- `RepublishAsync(templateVersionId)` або «Зробити поточною» для `Superseded`/`Published`
- Той самий flow: попередня Published → Superseded, обрана → Published + `CurrentPublishedVersionId`

### 7.2 Дві дати публікації

**Бажання:**

- **Перша** публікація v5 — дата **зберігається назавжди**
- **Повторне** включення v5 — **друга** дата (остання активація)

**Зараз:** одне поле `TemplateVersion.PublishedAtUtc` — при повторному `PublishAsync` **перезаписується** (`UtcNow`), перша дата **губиться**.

**Пропозиція:**

- `FirstPublishedAtUtc` (immutable після першої публікації)
- `LastPublishedAtUtc` або `RepublishedAtUtc` при кожній реактивації
- UI в `TemplateVersions/Details` і списку версій

### 7.3 Відображення «Активний» в UI

У БД: `Published` + `Template.CurrentPublishedVersionId == version.Id`.  
В UI інколи хочуть підпис **«Активний»** — можна додати в `Templates/Details` бейдж поруч із Published.

---

## 8. Сценарій «відточення шаблону» (еталон для тестів)

1. Опубліковано **v49** (`CurrentPublishedVersionId`).
2. Реєстратор створює справу → документ на **v49** → Fill.
3. Текст у полі «Дата відбору» низько — на вкладці **Позиція** зсув Y, на **Текст** — шрифт.
4. **Зберегти макет у шаблон** → `POST .../layout` → 200, «Макет збережено…».
5. Нова справа / інший клієнт, знову **v49** → поле вже на правильному місці **без** повторного підкручування.
6. Після публікації **v50** — нові справи на v50; старі на v49 лишаються зі своїм `TemplateVersionId`.

---

## 9. API та конфіг клієнта Fill

```json
{
  "templateVersionId": "...",
  "orderId": "...",
  "saveUrl": "/PdfWorkspace/Fill/{id}/values",
  "layoutSaveUrl": "/PdfWorkspace/Fill/{id}/layout",
  "versionNumber": 49,
  "segments": [
    {
      "segmentId", "templateFieldId", "tag", "title",
      "page", "x", "y", "width", "height",
      "textOffsetX", "textOffsetY",
      "fontSize", "fontName", "horizontalAlignment", "verticalAlignment",
      "textAlignment", "lineHeight", "svgPathData", "isPrimary",
      "rowVersionBase64", "canWrite", "accessLevel"
    }
  ],
  "savedValues": { "templateFieldId-or-tag": "..." }
}
```

Обидва POST з `[IgnoreAntiforgeryToken]` (як values); JS все одно шле `RequestVerificationToken` з `#pdfFillAntiforgeryForm`.

---

## 10. Тести та перевірка

```powershell
dotnet build .\UniversalLIMS\UniversalLIMS.csproj
dotnet test .\UniversalLIMS.sln --filter "FullyQualifiedName~PdfWorkspaceFillServiceTests"
```

Ручний чеклист:

- [ ] Панель: Текст / Позиція / Поля / Дії
- [ ] Значення → reload → збережені для `orderId`
- [ ] Макет → нова справа, **та сама версія** → offset/шрифт застосовані
- [ ] Права Published — матриця зберігається
- [ ] Публікація v50 → v49 Superseded, нові замовлення на v50

**Unit-тест для `SaveFillLayoutRefinementAsync` на Published** — бажано додати в `UniversalLIMS.Tests` (поки може бути відсутній).

---

## 11. Пов’язані документи

| Документ | Зміст |
|----------|--------|
| `handoff-pdf-workspace-fill.md` | Збереження `OrderFieldValue`, ключі GUID, діагностика |
| `handoff-stage-1-registration.md` | Реєстратура, SSOT |
| `handoff-next-agent.md` | Загальний стан проєкту, спринт лабораторії |
| `handoff-stage-2-laboratory.md` | Журнал проб, результати |

---

## 12. Стартовий prompt для агента

```text
Проєкт UniversalLIMS (.NET 8 MVC). Прочитай:
- docs/handoff-pdf-fill-panel-and-template-lifecycle.md (цей файл — пріоритет)
- docs/handoff-pdf-workspace-fill.md

Контекст: PDF Fill має бічну панель (текст + позиція як калібрування),
два збереження (замовлення vs макет версії). Макет зберігається для Published
через POST .../layout і SaveFillLayoutRefinementAsync.

НЕ плутати: значення → OrderFieldValue; макет → TemplateField/Segment для templateVersionId.

Беклог (погоджено, не зроблено): Republish Superseded, дві дати публікації,
UI «Активний» для CurrentPublishedVersionId.

Задача: [вставити конкретну задачу користувача].
Дотримуйся SSOT, не ламай координати без запиту, dotnet test перед завершенням.
Не комітити .vs/, bin/, obj/, debug*.log.
```

---

*Документ узгоджено з замовником у чаті 2026-05-25: панель Fill, макет лише для поточної версії, права адміна на Published, одна активна Published + беклог republish/дві дати.*
