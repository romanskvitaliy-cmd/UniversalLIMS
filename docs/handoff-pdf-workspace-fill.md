# Handoff: PDF Workspace — заповнення та збереження полів

> Вижимка для нового агента / розробника.  
> Репозиторій: `UniversalLIMS` (.NET 8, ASP.NET Core MVC).  
> Мова UI: українська.

---

## 1. Що це за фіча

**PDF Workspace Fill** — сторінка, де користувач бачить PDF шаблон (протокол) з overlay-полями, заповнює їх і зберігає значення в замовлення (`Order` + `OrderFieldValue`), потім генерує **фінальний PDF** з накладеним текстом.

| URL (приклад) | Призначення |
|---------------|-------------|
| `GET /PdfWorkspace` | Список PDF-версій шаблонів |
| `GET /PdfWorkspace/Fill/{templateVersionId}?orderId=` | UI заповнення |
| `POST /PdfWorkspace/Fill/{templateVersionId}/values` | Збереження значень (JSON) |
| `GET /PdfWorkspace/Fill/{templateVersionId}/final?orderId=` | Фінальний PDF |

Політика доступу: `LimsPolicies.RegisterSamples`.

---

## 2. Проблема, яку вирішували

1. **Злиття полів:** різні теги (`Global.DocNumber`, `ProtocolNumber`, `SamplingLocation`…) зводились до одного канонічного `DataField.Key` (через старий `PdfWorkspaceFieldKeyResolver`) → в `OrderFieldValue` лишалось **одне** значення замість 10+.
2. **savedCount = 0:** `templateFieldId` з JS (рядок) погано біндився на `Guid?` у моделі запиту.
3. **HTTP 500:** кілька `TemplateField` з одним `DataFieldId` → спроба вставити кілька `OrderFieldValue` з однаковим `(OrderId, DataFieldId)` → порушення **unique index**.

**Поточне рішення (просте):** один `TemplateField` = один `DataField` з ключем **`TemplateField.Id.ToString("D")`**. Це гарантує окремий запис у `OrderFieldValues` на кожне поле overlay.

---

## 3. Потік даних (зараз)

```
Fill.cshtml
  → window.__pdfFillSegments (segmentId, templateFieldId, tag, dataFieldKey, x/y/…)
  → pdf-workspace-fill.js

collectFilledValues():
  [{ templateFieldId: "<guid>", value: "текст" }, …]

POST { orderId, values }
  → PdfWorkspaceController.SaveValues
  → PdfWorkspaceFillService.SaveValuesAsync

Для кожного item:
  1. Знайти TemplateField за Id + TemplateVersionId
  2. EnsureDataFieldIdForTemplateFieldAsync(field)
     → DataField.Key = field.Id (GUID string)
  3. Upsert OrderFieldValue (OrderId, DataFieldId, StoredValue)
  4. Один SaveChangesAsync в кінці

Відповідь JSON:
  { orderId, savedCount, totalFields, unmatched, message }
  savedCount = кількість елементів у запиті (тимчасово, навіть якщо частина unmatched)
```

**Читання для UI / PDF:** `GetSavedValuesByKeyAsync` і `LoadOverlaySegmentsWithValuesAsync` шукають значення спочатку за ключем `TemplateField.Id`, потім за `DataField.Key` поля (якщо був старий мапінг).

---

## 4. Ключові файли

| Файл | Роль |
|------|------|
| `Controllers/PdfWorkspaceController.cs` | MVC + API save/final |
| `Infrastructure/Registration/PdfWorkspaceFillService.cs` | Збереження, завантаження, PDF overlay |
| `Infrastructure/Registration/ReferralPdfOverlayRenderer.cs` | Малювання тексту на PDF |
| `Application/Registration/Abstractions/IPdfWorkspaceFillService.cs` | DTO: `PdfWorkspaceFieldValueDto`, `PdfWorkspaceSaveResult` |
| `ViewModels/PdfWorkspace/PdfWorkspaceSaveRequest.cs` | `templateFieldId` як **string**, `value` |
| `Views/PdfWorkspace/Fill.cshtml` | Сторінка + серіалізація segments у JS |
| `wwwroot/js/pdf-workspace-fill.js` | PDF.js preview, overlay inputs, save |
| `UniversalLIMS.Tests/Registration/PdfWorkspaceFillServiceTests.cs` | 2 unit-тести |

**Видалено (не відновлювати без потреби):**

- `PdfWorkspaceFieldMatcher.cs`
- `PdfWorkspaceStorageKey.cs`
- `PdfWorkspaceFieldKeyResolver.cs`

---

## 5. Модель БД (важливо)

`OrderFieldValue` **не має** `TemplateFieldId` / `OrderDocumentId`:

```csharp
OrderFieldValue { OrderId, SampleId?, DataFieldId, StoredValue }
```

Unique: `(OrderId, DataFieldId)` коли `SampleId IS NULL`.

`OrderDocument` лише зв’язує order + template version (логується в save, не пишеться в `OrderFieldValue`).

`DataField` — словник; для PDF Workspace створюються записи з `Key = TemplateField.Id` (GUID).

---

## 6. Діагностика

**Фронт (F12 Console):**

```text
[PdfWorkspace Fill] save payload: { orderId, values: [...] }
```

**Сервер (Console / Output):**

```text
=== SAVE VALUES CALLED (Controller) ===
=== SAVE VALUES CALLED ===
Mapped TemplateFieldId=...
SaveChanges OK: N OrderFieldValues
```

При 500 контролер повертає `{ message, detail, inner }`; JS показує `detail`.

**Типові збої:**

| Симптом | Причина |
|---------|---------|
| `savedCount: 0`, порожній payload | `dataset.templateFieldId` порожній на input (немає в segments) |
| `TemplateField NOT FOUND` | GUID не з цієї `TemplateVersionId` |
| 500 unique constraint | знову два поля на один `DataFieldId` (не використовувати спільний DataField для save) |
| Фінальний PDF порожній | значення не знайдені при `LoadOverlaySegmentsWithValuesAsync` (перевірити ключі в `OrderFieldValues` → `DataFields.Key`) |

---

## 7. Тести

```bash
dotnet test UniversalLIMS.Tests --filter "FullyQualifiedName~PdfWorkspaceFillServiceTests"
```

- `SaveValuesAsync_PersistsByDataFieldKeyWhenMapped` — одне поле з існуючим DataField
- `SaveValuesAsync_CreatesSeparateRecordsPerTemplateFieldTag` — 4 поля → 4 окремі `OrderFieldValue` (ключі = GUID полів)

---

## 8. Що ще можна зробити (беклог)

- [ ] Прибрати тимчасові `Console.WriteLine` після стабілізації (залишити `ILogger`).
- [ ] `savedCount` рахувати як реально збережені в БД, а не `values.Count`.
- [ ] Узгодити з продуктом: чи потрібен мапінг на канонічні `DataField.Key` (`Protocol.Number`…) **і** унікальність — зараз пріоритет унікальності через `TemplateField.Id`.
- [ ] Міграція старих збережених значень (якщо в БД лишились ключі-теги/resolver).
- [ ] Перевірити фінальний PDF на реальному шаблоні «Повітря закритих приміщень v32» (~11 полів).

---

## 9. Реєстрація в DI

`Program.cs` — `IPdfWorkspaceFillService` → `PdfWorkspaceFillService` (перевірити наявність при змінах).

---

## 10. Контекст етапу проєкту

Див. також:

- `docs/handoff-stage-1-registration.md` — реєстратура, `OrderFieldValue`, заборона EAV для ядра
- Етап 0 (конструктор overlay) — **не ламати** координати/сегменти без запиту

**Не комітити:** `.vs/`, `bin/`, `obj/`.

---

*Останнє оновлення handoff: травень 2026 — після виправлення 500 (duplicate OrderFieldValue) та спрощення save до `templateFieldId` + `value`.*
