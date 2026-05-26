# Handoff: гібридні теги + мапінг полів (повний контекст для агента)

> **Прочитай цей файл першим** перед роботою над реєстратурою, тегами або Fill.  
> Оновлено: 2026-05-26  
> Репозиторій: UniversalLIMS (.NET 8 MVC), UI — українська.

---

## 1. Продуктова ідея (фінальна, погоджена)

### 1.1 Проблема

~42–60 PDF-протоколів, багато полів на шаблон. Одні й ті самі **змістовні** дані (дата відбору, замовник…) зустрічаються в різних бланках з **різними** підписами/тегами.

### 1.2 Рішення (гібрид, **не** жорсткий глобальний SSOT на все)

| Шар | Суть |
|-----|------|
| **Локальні теги** | Кожен протокол має префікс: `f327_`, `Food_`, `f320_`… Поля можуть **дублюватися** між протоколами (`f327_SamplingDate` ≠ `Food_SamplingDate`). |
| **Глобальний DataField** | Опційно для полів у словнику (`Sample.SamplingDate`, `Protocol.Number`…). |
| **Мапінг у реєстратурі** | При **кількох** шаблонах на одне замовлення реєстратор **сам** об’єднує поля (без auto-match за текстом). |
| **Одне введення** | Після мапінгу — спільна форма → значення пишеться в усі `DataFieldId` членів групи. |

### 1.3 Критичні заборони

- **Не** auto-match за `Title` / підписом поля.
- **Не** копіювати `Customer.*` / `Sample.Number` в `OrderFieldValue` (див. `handoff-stage-1-registration.md`).
- **Не** ламати overlay: `TemplateFieldSegment`, `ReferralPdfOverlayRenderer`, layout save з Fill.
- **Не** плутати: значення → `OrderFieldValue`; макет → `TemplateField` / `Segment`.

### 1.4 Збереження значень (технічно)

```
PDF/UI:  TemplateFieldId + TemplateVersionId
БД:      OrderFieldValue.DataFieldId
Ключі:   f327_pH, Food_Conclusion (локальні) або workspace GUID = TemplateField.Id (якщо не змаплено)
```

---

## 2. Що вже зроблено (Етап 0) — коміти

| Коміт | Зміст |
|-------|--------|
| `497506e` | **Етап 0:** spec, JSON `docs/data/protocol-tags-*.json`, `ProtocolTagCatalog` + seeder, optgroup f327/Food у `Map.cshtml`, Fill через семантичний `DataFieldId` |
| `c9b4ea9` | **Fix:** порядок static init у `ProtocolTagCatalog` — спочатку `F327`, `Food`, потім `All = [.. F327, .. Food]` (інакше `TypeInitializationException`) |

### 2.1 Файли Етапу 0

| Файл | Роль |
|------|------|
| `docs/spec-hybrid-tags-and-order-field-mapping.md` | Короткий spec + таблиця етапів |
| `docs/data/protocol-tags-f327.json` | Каталог f327 (для імпорту / Grok) |
| `docs/data/protocol-tags-food.json` | Каталог Food |
| `Infrastructure/Persistence/Seed/ProtocolTagCatalog.cs` | Список тегів для seeder |
| `Infrastructure/Persistence/Seed/ProtocolTagCatalogSeeder.cs` | Додає `DataField` при старті |
| `Views/TemplateFields/Map.cshtml` | `#pdfTagLibrary` — optgroup **f327_** / **Food_** |
| `Infrastructure/Registration/PdfWorkspaceFillService.cs` | `ResolveStorageDataFieldIdsAsync` — пріоритет семантичного `DataField`, не лише GUID-workspace |

### 2.2 Перевірка Етапу 0

1. Перезапуск застосунку (seed).
2. Map → «Тег з бібліотеки» → групи f327 / Food.
3. Fill → поле з тегом `f327_pH` + мапінг на `DataField` → значення в `OrderFieldValue` по **семантичному** ключу.

---

## 3. Етапи реалізації (далі)

| Етап | Статус | Зміст | Коміт (орієнтир) |
|------|--------|--------|------------------|
| **0** | ✅ Зроблено | Каталог, Map, Fill, seed | див. вище |
| **1** | ✅ | `OrderFieldLinkGroup` / `Member`, `MapOrderFields`, `IOrderFieldLinkService` | `feat(registration): order field link groups + mapping step` |
| **2** | ✅ (базово) | Textarea спільних значень на Map → `ApplySharedFieldValuesAsync` | включено в етап 1 |
| **3+** | ⏳ | `TemplateVersionId` у бібліотеці текстів; drag&drop; профіль мапінгу | окремі PR |

**Паралельно (контент, не блокує код):** замовник + Grok додають `docs/data/protocol-tags-{code}.json` по 1–2 шаблонах → розширити `ProtocolTagCatalog.cs` + optgroup у Map порціями 30–70 тегів.

---

## 4. Етап 1 — технічний дизайн

### 4.1 Сутності

```text
OrderFieldLinkGroup
  OrderId, Label?, SortOrder
  Members → OrderFieldLinkMember[]

OrderFieldLinkMember
  GroupId, TemplateFieldId, TemplateVersionId
```

Мапінг зберігається **на замовлення** (не глобально на всі 60 протоколів).

### 4.2 UX-потік

1. `Orders/Create` — замовник, тип, **2+ PDF-шаблони**.
2. Кнопка **«Далі: мапінг спільних полів»** (formaction `PrepareFieldMapping`) — лише якщо ≥2 шаблонів.
3. `Orders/MapOrderFields` — таблиця полів по шаблонах; чекбокси + **«Об’єднати вибрані»**; список груп.
4. `Orders/CreateWithFieldMapping` — створення замовлення + збереження груп (+ Etap 2: спільні значення).
5. Один шаблон — як раніше, **без** мапінгу.

### 4.3 FLS

Показувати поля з Read+; запис у групу — лише якщо Write на поле (або приховати без Write).

### 4.4 Рознесення значень (Етап 2)

Для кожної групи з одним введеним текстом → для кожного `TemplateFieldId` у групі → `ResolveStorageDataFieldId` → upsert `OrderFieldValue` (унікальність `(OrderId, DataFieldId)` при `SampleId IS NULL`).

---

## 5. Додавання тегів для нових шаблонів (процес)

1. JSON: `docs/data/protocol-tags-{code}.json` (зразок — f327, food).
2. Додати масив у `ProtocolTagCatalog.cs` (нова властивість `F320` тощо) + оновити `All`.
3. Optgroup у `Map.cshtml` `#pdfTagLibrary`.
4. Перезапуск → seeder додасть `DataField`.
5. Перевірка в Map / Fill.

**Не** завантажувати 2000+ тегів одразу.

---

## 6. Пов’язані документи

| Документ | Коли читати |
|----------|-------------|
| `handoff-hybrid-tags-and-registry-mapping.md` | **Цей файл** — старт |
| `spec-hybrid-tags-and-order-field-mapping.md` | Короткий spec |
| `handoff-stage-1-registration.md` | SSOT Customer/Sample |
| `handoff-pdf-workspace-fill.md` | Fill, workspace vs semantic |
| `handoff-pdf-fill-panel-and-template-lifecycle.md` | Панель Fill, layout |

---

## 7. Стартовий prompt для агента

```text
UniversalLIMS. Прочитай docs/handoff-hybrid-tags-and-registry-mapping.md (повний контекст).

Етап 0 зроблено (коміти 497506e, c9b4ea9). Не ламай PdfWorkspaceFillService overlay/layout.

Поточна задача: [Етап 1 / Етап 2 / додати теги з docs/data/protocol-tags-XXX.json].

Правила: коміт на кожен глобальний етап; не комітити debug*.log; dotnet test перед завершенням.
```

---

*Зібрано з чату 2026-05-26: погоджена гібридна модель, відмова від auto-match, паралельна робота (код + теги з Grok).*
