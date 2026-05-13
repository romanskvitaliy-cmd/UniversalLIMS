# Handoff: Етап 1 — Реєстратура та SSOT

> Документ для нового розробника / агента.  
> Мова: українська (UI і доменні display names).  
> Етап 0 (конструктор PDF overlay) **закритий** — не змінювати без явного запиту.

---

## 1. Поточна позиція на карті

| Етап | Статус |
|------|--------|
| 0 — Фундамент + PDF-Overlay конструктор | 🟢 Завершено |
| **1 — Реєстратура та SSOT** | **⚪ Активна робота** |
| 2 — Лабораторний цикл | ⚪ Заборонено починати |
| 3 — Валідація та затвердження | ⚪ Заборонено починати |
| 4 — Генерація протоколів | ⚪ Заборонено (окрім п. 4.3) |
| 5 — Адмін / ISO | 🟡 Частково (audit, ролі, soft annulment) |

---

## 2. Архітектурне рішення: НЕ EAV для ядра реєстратури

**Заборонено** зберігати базові атрибути замовника та проби (`ПІБ`, `телефон`, `номер проби` тощо) в універсальній таблиці ключ–значення. Це EAV-антипатерн: ламає пошук клієнтів, індексацію та продуктивність LIMS.

**Два шари даних (не плутати):**

| Шар | Де живуть дані | Призначення |
|-----|----------------|-------------|
| **Статичні доменні колонки** | `Customer`, `Sample` | Ідентичність, пошук, звіти, FK, індекси |
| **Динамічні поля шаблону** | `OrderFieldValue` | Лише специфічні поля, яких немає в статичній моделі |
| **Мапінг на PDF** | `DataField.Key` → резолвер | Зв’язок тега шаблону з джерелом даних (колонка або `OrderFieldValue`) |

`DataField` — **словник і контракт мапінгу** для конструктора шаблонів, **не** заміна колонок `Customer` / `Sample`.

---

## 3. Канонічні назви сутностей (без варіантів)

**Правило:** у коді, БД, міграціях, API і UI — **лише** таблиця нижче.

| Канонічна сутність | Namespace | Таблиця БД | Опис |
|--------------------|-----------|------------|------|
| `Customer` | `Domain.Registration` | `Customers` | База замовників |
| `InvestigationType` | `Domain.Registration` | `InvestigationTypes` | Довідник типу дослідження |
| `Order` | `Domain.Registration` | `Orders` | Замовлення / справа |
| `Sample` | `Domain.Registration` | `Samples` | Зразок у межах Order |
| `OrderDocument` | `Domain.Registration` | `OrderDocuments` | Екземпляр документа по шаблону |
| `OrderFieldValue` | `Domain.Registration` | `OrderFieldValues` | Динамічні поля реєстратури (не ядро Customer/Sample) |

**Заборонені назви:** `FieldValue`, `OrderSample`, `Case`, `CaseDocument`, `SampleFieldValue`, `DocumentFieldValue`, `RegistrationData`, `ISSOTService`.

**Зв’язки (фіксовані):**
- `Customer` 1→N `Order`
- `Order` 1→N `Sample`
- `Order` 1→N `OrderDocument` (через `SampleId` або `OrderId` — **один** узгоджений шлях при імплементації)
- `Order` 1→N `OrderFieldValue`; опційно `SampleId` для полів рівня проби
- `InvestigationType` N↔M `Template` через `InvestigationTypeTemplates`

**Етап 2 (не зараз):** результати лаборантів — **окрема** сутність (наприклад `SampleResultValue`), **не** `OrderFieldValue`.

---

## 4. SSOT — що це означає в цій системі

**SSOT для замовника:** `Order` посилається на `CustomerId`.  
**Заборонено** копіювати `FullName`, `ContactPhone` тощо в `Order`, `Sample` або `OrderDocument`.

```
Правильно:
  Order.CustomerId → Customer.FullName

Заборонено:
  Order.CustomerName
  OrderDocument.CustomerFullName
  OrderFieldValue для DataField.Key = "Customer.FullName"
```

**SSOT для номера проби:** значення в `Sample.Number` (колонка), **не** в `OrderFieldValue`.

**OrderFieldValue** — **тільки** для динамічних полів, яких немає в статичній моделі `Customer` / `Sample` / `Order`, але які потрібні шаблонам через `DataField`.

---

## 5. Статичні колонки (обов’язкові)

### 5.1 Customer

```text
Customer
  Id
  FullName              (обов'язково; індекс для пошуку)
  OrganizationName      (nullable)
  ContactPhone          (nullable)
  Address               (nullable)
  + BaseEntity (CreatedAtUtc, RowVersion, …)
  + ISoftAnnulled
```

- Пошук клієнтів — по `FullName`, `OrganizationName`, `ContactPhone` (SQL `LIKE` / full-text за потреби).
- **Не** виносити ці поля в `OrderFieldValue`.

### 5.2 Sample

```text
Sample
  Id
  OrderId
  Number                (обов'язково; унікальність у межах філії/року — через INumberingService)
  RegisteredAt          (обов'язково; UTC)
  InvestigationTypeId
  Status
  + BaseEntity, audit
```

- `INumberingService` **записує** в `Sample.Number`, **не** в `OrderFieldValue`.

### 5.3 Order

```text
Order
  Id
  CustomerId            (FK → Customer; єдине джерело ПІБ/телефону для справи)
  BranchId
  Status
  ReferralNumber        (nullable; номер направлення — статична колонка Order)
  + BaseEntity, audit
```

**Заборонено на Order:** `CustomerName`, `CustomerPhone`, `CustomerAddress` та інші копії з `Customer`.

---

## 6. OrderFieldValue — лише динаміка

### 6.1 Призначення

Зберігання **специфічних динамічних** значень реєстратури, прив’язаних до `DataField`, коли немає відповідної статичної колонки.

**Приклади (якщо з’являться в шаблонах):** «Місце відбору проби», «Підстава дослідження», «Додатковий коментар реєстратора».

### 6.2 Модель

```text
OrderFieldValue
  Id
  OrderId               (обов'язково)
  SampleId              (nullable; null = поле на рівні замовлення)
  DataFieldId           (FK → DataField)
  ValueText
  RowVersion
  + audit за потреби
```

**Унікальність:** `(OrderId, SampleId, DataFieldId)` — unique index.

### 6.3 Що НЕ класти в OrderFieldValue

| DataField.Key (seed) | Де зберігається фактичне значення |
|----------------------|-----------------------------------|
| `Customer.FullName` | `Customer.FullName` |
| `Customer.OrganizationName` | `Customer.OrganizationName` |
| `Customer.ContactPhone` | `Customer.ContactPhone` |
| `Sample.Number` | `Sample.Number` |
| `Sample.RegisteredAt` | `Sample.RegisteredAt` |
| `Branch.Code`, `Branch.Name` | `Branch` через `Order.BranchId` |
| `Conclusion.Text` | **Етап 3**, не Етап 1 |

Seed-ключі `DataField` **залишаються** для мапінгу тегів шаблону. При PDF-рендері (Етап 4) резолвер за `DataField.Key` читає **колонку сутності**, а не `OrderFieldValue`, якщо ключ у таблиці вище.

### 6.4 DataFieldScope в Етапі 1

| Scope | Зберігання в Етапі 1 |
|-------|----------------------|
| `Registration` | `Customer` / `Order` (статичні колонки) або `OrderFieldValue` (лише динаміка) |
| `Sample` | `Sample` (статичні колонки) або `OrderFieldValue` з `SampleId` |
| `System` | Резолв з `Branch`, нумератора — **не** EAV |
| `Result` | **Етап 2** — окрема таблиця результатів |
| `Conclusion` | **Етап 3** |

---

## 7. Жорсткі заборони

| # | Заборона |
|---|----------|
| 1 | EAV для `FullName`, `Phone`, `Address`, `Sample.Number`, `RegisteredAt` |
| 2 | Копіювання полів `Customer` на `Order` / `Sample` / `OrderDocument` |
| 3 | Сутність `FieldValue` (канонічна назва — `OrderFieldValue`) |
| 4 | Зберігання значень шаблону в JSON на `OrderDocument` |
| 5 | Результати лаборанта в `OrderFieldValue` |
| 6 | UI / сервіси Етапу 2+ без явного запиту |

---

## 8. Межі Етапу 1

### 8.1 В scope

1. CRUD `Customer` з колонками `FullName`, `OrganizationName`, `ContactPhone`, `Address`
2. `Order` з `CustomerId` (без дублювання ПІБ)
3. `Sample` з `Number`, `RegisteredAt`
4. `InvestigationType` → автопризначення published `Template` → `OrderDocument`
5. Маршрутизація `OrderDocument` на `Branch`
6. `INumberingService` → `Sample.Number`, `Order.ReferralNumber`
7. `OrderFieldValue` — лише динамічні поля (якщо є в шаблонах)
8. `IOrderFieldValueService` — CRUD динамічних значень
9. PDF-направлення (виняток 4.3 нижче)
10. Тести + MVC під `RegisterSamples`

### 8.2 Поза scope

| Заборонено | Етап |
|------------|------|
| Лабораторний журнал, внесення результатів | 2 |
| `SampleResultValue` / аналог | 2 |
| `TemplateFieldPermission` у runtime форм | 2 |
| Нормативи, висновок, затвердження | 3 |
| Повна генерація протоколів, email | 4 |
| Рефакторинг конструктора / Word→PDF | 0 |

**Правило зупинки:** UI для `LaboratoryTechnician` або `Specialist` = не Етап 1.

### 8.3 PDF-направлення (єдиний виняток з Етапу 4)

- Сервіс: `IReferralPdfGenerator`
- Резолвер значень: `Customer` / `Sample` / `Order` колонки + `OrderFieldValue` для динамічних ключів
- Без універсального rendering framework

---

## 9. Архітектура коду

```text
Domain/Registration/
Application/Registration/Abstractions/
Infrastructure/Registration/
Controllers/          CustomersController, OrdersController
ViewModels/Registration/
```

### Сервіси (канонічні імена)

| Інтерфейс | Відповідальність |
|-----------|------------------|
| `ICustomerService` | CRUD `Customer` (статичні колонки) |
| `IOrderRegistrationService` | Order, Sample, OrderDocument, маршрутизація |
| `IOrderFieldValueService` | Лише динамічні `OrderFieldValue` |
| `INumberingService` | `Sample.Number`, `Order.ReferralNumber` |
| `IReferralPdfGenerator` | PDF-направлення |

**Не створювати:** `IFieldValueService`, `FieldValue`, `ICaseService`.

---

## 10. OrderDocument — snapshot шаблону

При створенні обов’язково:
- `TemplateId`
- `TemplateVersionId` (published на момент створення)

---

## 11. Статуси (мінімум Етапу 1)

- **Order:** `Draft` → `Registered`
- **Sample:** `Registered` → `Routed`
- **OrderDocument:** `Pending` → `SentToLab`

Статуси Етапу 2+ — **не додавати** зараз.

---

## 12. Перевірка перед merge

```powershell
dotnet test .\UniversalLIMS.sln
```

Чеклист:

- [ ] `Customer.FullName`, `OrganizationName`, `ContactPhone`, `Address` — колонки в `Customers`
- [ ] `Sample.Number`, `RegisteredAt` — колонки в `Samples`
- [ ] `Order.CustomerId` є; на `Order` немає `CustomerName` / `CustomerPhone`
- [ ] Немає сутності `FieldValue`; є `OrderFieldValue`
- [ ] Базові ключі `Customer.*` / `Sample.*` **не** дублюються в `OrderFieldValues`
- [ ] `OrderDocument.TemplateVersionId` заповнений
- [ ] Немає UI лаборанта / експерта
- [ ] Пошук клієнта працює по SQL на `Customers`, не по EAV

---

## 13. Стартовий prompt для агента

```text
Проєкт UniversalLIMS (.NET 8 MVC, EF Core, Identity). Етап 0 закритий.
Реалізуй Етап 1 строго за docs/handoff-stage-1-registration.md:
- Customer: статичні FullName, OrganizationName, ContactPhone, Address
- Sample: статичні Number, RegisteredAt
- Order: лише CustomerId (SSOT через FK, без копії ПІБ)
- OrderFieldValue: ТІЛЬКИ динамічні поля; НЕ EAV для ядра реєстратури
- Заборонено сутність FieldValue
Без Етапу 2. Почни з Domain/Registration + міграція + тести.
```

---

## 14. Що вже є в репозиторії (не переробляти)

- PDF overlay конструктор, Word→PDF, `Template*`, `TemplateFieldSegment`
- `DataField` (словник мапінгу), `Branch`, audit, soft annulment
- Ролі: `Registrar`, policy `RegisterSamples`

Деталі Етапу 0: `docs/stage-1-foundation.md`, `docs/stage-2-pdf-overlay.md`.
