# Spec: гібридні теги + мапінг полів у реєстратурі

> Оновлено: 2026-05-26  
> Статус: **Етап 0** — spec + каталог тегів + Fill для семантичного `DataField`  
> Репозиторій: UniversalLIMS (.NET 8 MVC)

---

## 1. Мета

- ~42–60 PDF-протоколів, ~50–70 полів на шаблон.
- **Локальні теги** з префіксом протоколу (`f327_`, `Food_`, `f320_`…).
- **Дублювання** однакових за змістом полів у різних протоколах (різні ключі).
- **Мапінг у реєстратурі** при кількох шаблонах: реєстратор сам об’єднує поля → одне введення → рознесення значень.
- **Без** автоматичного злиття за текстом підпису поля.

---

## 2. Два шари (не плутати)

| Шар | Де | Призначення |
|-----|-----|-------------|
| Значення | `OrderFieldValue` → `DataFieldId` | Текст для замовлення |
| Макет | `TemplateField` + `TemplateFieldSegment` | Позиція на PDF для версії шаблону |

**UI/PDF:** `TemplateFieldId` + `TemplateVersionId`  
**БД значень:** завжди `DataFieldId` (ключ `f327_pH`, `Sample.SamplingDate` або workspace-GUID для немаплених полів)

---

## 3. Типи тегів

### 3.1 Локальні (з префіксом)

- `f327_*` — питна вода, форма 327  
- `Food_*` — харчові проби  
- Інші: `f320_`, `f325_`, `f413_`…

### 3.2 Глобальні (опційно)

Існуючий словник `DataField`: `Sample.SamplingDate`, `Protocol.Number`, `Order.CustomerName`…  
Використовувати для полів, які свідомо винесені в каталог; **не** обов’язково для всіх протоколів.

### 3.3 Повторюваність

`f327_SamplingDate` і `Food_SamplingDate` — **різні** теги. Зв’язок «одне значення» — через **мапінг замовлення** або спільний `DataFieldId` після об’єднання при збереженні.

---

## 4. Поведінка по модулях

### 4.1 Map (конструктор)

- Теги з `#pdfTagLibrary` + ручний ввід.
- Каталог: `Infrastructure/Persistence/Seed/ProtocolTagCatalog.cs` + optgroup у `Map.cshtml`.
- Seed при старті: `ProtocolTagCatalogSeeder` → `DataFields`.

### 4.2 Реєстратура (Етап 1–2, TODO)

1. Вибір кількох `TemplateVersion`.
2. Крок **«Мапінг спільних полів»** (skip якщо 1 шаблон).
3. Групи: `OrderFieldLinkGroup` + `OrderFieldLinkMember`.
4. Форма спільних полів → save → upsert `OrderFieldValue` для кожного `DataFieldId` у групі.
5. FLS: Read/Write по полях.

### 4.3 PDF Fill

- Збереження: якщо `TemplateField` прив’язаний до **семантичного** `DataField` (ключ ≠ GUID поля) — писати в нього; інакше workspace `DataField` з ключем `TemplateField.Id`.
- Не змінювати `ReferralPdfOverlayRenderer` без окремого запиту.

### 4.4 Бібліотека текстів

- `FieldTextLibraryEntry`: глобальні через `DataFieldId`; локальні — через той самий `DataField` після seed (`f327_*`, `Food_*`).
- Опційно пізніше: `TemplateVersionId` для фраз шаблону.

---

## 5. Етапи реалізації та коміти

| Етап | Зміст | Коміт (орієнтир) |
|------|--------|------------------|
| **0** | Spec, каталог f327/Food, Map optgroup, seed, Fill semantic DataField | `feat(tags): etap 0 — catalog f327/Food, Fill semantic DataField` |
| **1** | `OrderFieldLinkGroup`, UI мапінгу в Create | `feat(registration): order field link groups + mapping step` |
| **2** | Спільна форма + рознесення значень | `feat(registration): shared field values after mapping` |
| **3+** | Бібліотека по версії, drag&drop, профілі мапінгу | окремі PR |

**Паралельно (контент):** списки тегів по 1–2 шаблонах → `docs/data/protocol-tags-*.json` → наступний seed/Map optgroup.

---

## 6. Файли Етапу 0

| Файл | Роль |
|------|------|
| `docs/spec-hybrid-tags-and-order-field-mapping.md` | Цей документ |
| `docs/data/protocol-tags-f327.json` | Каталог f327 (джерело правди для імпорту) |
| `docs/data/protocol-tags-food.json` | Каталог Food |
| `Infrastructure/Persistence/Seed/ProtocolTagCatalog.cs` | Статичний список для seeder |
| `Infrastructure/Persistence/Seed/ProtocolTagCatalogSeeder.cs` | `DataFields` у БД |
| `Views/TemplateFields/Map.cshtml` | Optgroup у `#pdfTagLibrary` |
| `PdfWorkspaceFillService.cs` | Пріоритет семантичного `DataFieldId` |

---

## 7. Заборони

- Auto-match полів за `Title` / підписом.
- Копіювання `Customer.*` в `OrderFieldValue` (див. `handoff-stage-1-registration.md`).
- Зміна координат overlay «заодно» з мапінгом реєстратури.

---

## 8. Пов’язані handoff

- `handoff-pdf-workspace-fill.md`
- `handoff-pdf-fill-panel-and-template-lifecycle.md`
- `handoff-stage-1-registration.md`
