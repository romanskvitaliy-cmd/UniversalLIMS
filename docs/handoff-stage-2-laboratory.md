# Handoff для нового агента: ZhytomyrLIMS (UniversalLIMS)

**Роль:** Senior Architect / Lead Developer (.NET 8, 15+ років досвіду)  
**Місія:** Продовжити розробку строго за дорожньою картою.  
**Поточний фокус:** Спринт 3 — Лабораторний цикл (Етап 2)

---

## 1. Загальна інформація про проєкт

ZhytomyrLIMS — лабораторна інформаційна система для ДУ «Житомирський обласний ЦКПХ МОЗ України», орієнтована на повну відповідність ДСТУ ISO/IEC 17025:2019.

**Цільова архітектура:**

- PDF як незмінна підкладка + Overlay даних за координатами (Етап 0)
- Єдине джерело правди (SSOT) — гібридна модель
- Повна простежуваність і аудит

---

## 2. Поточний стан проєкту (на момент передачі)

| Етап | Назва етапу | Статус |
|------|-------------|--------|
| 0 | Фундамент + PDF-Overlay конструктор | Завершено |
| 1 | Реєстратура + SSOT | Завершено |
| 2 | Лабораторний цикл | В роботі |
| 3–4 | Висновки, генерація протоколів | Не почато |

**Закриті етапи:**

- Повноцінний візуальний конструктор шаблонів (PDF Overlay)
- Версійність шаблонів, Field-Level Security, Audit Trail, Soft Annulment
- Реєстратура: Customer, Order, Sample, OrderDocument, OrderFieldValue (тільки динамічні поля)

---

## 3. Критичні архітектурні правила (обов'язково дотримуватися)

### SSOT (Гібридна модель)

- Статичні бізнес-дані (FullName, Phone, Number тощо) зберігаються в `Customer` та `Sample`.
- `OrderFieldValue` використовується **тільки** для динамічних полів реєстратури.
- Лабораторні результати зберігаються в окремій сутності `SampleResultValue`.

### Immutable Logic

- Фізичне видалення заборонено. Тільки `ISoftAnnulled` + причина.

### Field-Level Security

- Права доступу до полів (`TemplateFieldPermission`) налаштовуються в конструкторі.
- Runtime-перевірка обов'язкова.

### Інші правила

- Всі Foreign Keys — `DeleteBehavior.Restrict`
- Документи генеруються тільки через PDF Overlay (Syncfusion)
- UI — українською мовою
- Бізнес-логіка — в Application сервісах

---

## 4. Спринт 3 — Лабораторний цикл (поточне завдання)

> Актуалізація 2026-05-27: окрема таблична сторінка «Показники» / `ResultEntryService`
> прибрана з UI. Лаборант працює напряму з PDF Workspace, тегами шаблонів і
> `FieldTextLibrary`. `SampleResultValue` лишається в домені/міграціях як історичний
> фундамент, але не є поточним UI workflow.
>
> **Заборона:** не відновлювати кнопку «Показники», `LaboratoryController.ResultEntry`,
> `ResultEntryService` або `Views/Laboratory/Results.cshtml` без окремого рішення власника продукту.

**Мета:** Реалізувати роботу лаборанта — журнал проб, внесення результатів, статусну модель.

**In Scope:**

- Лабораторний журнал (список проб + фільтри)
- Заповнення лабораторних PDF через шаблонні теги та бібліотеку текстів
- Field-Level Security на рівні PDF/template fields
- Статусна модель (`SampleStatus`, `OrderDocumentStatus`)
- Базовий довідник обладнання (опціонально)

**Out of Scope:**

- Висновки експерта
- Нормативна перевірка та підсвітка перевищень
- Генерація фінальних протоколів
- Email-відправка

---

## 5. Доменна модель Спринту 3

### Нові сутності

**`SampleResultValue`** (ОБОВ'ЯЗКОВО імплементує `ISoftAnnulled`):

- Зберігання лабораторних результатів.
- Поля: `SampleId`, `DataFieldId`, `StoredValue`, `EnteredAtUtc`, `EnteredByUserId`, `EquipmentId` (nullable).
- Унікальність: `(SampleId, DataFieldId)`.
- Примітка: Якщо лаборант помилився, результат не видаляється. Він анулюється (`IsAnnulled = true` + причина), а потім створюється новий запис. Це вимога ISO 17025.

**`Equipment`** (базовий довідник обладнання, імплементує `ISoftAnnulled`):

- Поля: `Code`, `NameUk`, `SerialNumber` (nullable), `BranchId` (nullable), `IsActive`.
- Використовується для вибору обладнання при внесенні результату (опціонально на цьому етапі).

### Розширення існуючих сутностей

- Додати нові значення в `SampleStatus` (наприклад: `InProgress`, `ResultsEntered`).
- Додати нові значення в `OrderDocumentStatus`.

---

## 6. Порядок виконання Спринту 3

1. Domain + EF Configurations + Міграція
2. Application Layer (сервіси та моделі)
3. Infrastructure (реалізації сервісів)
4. ViewModels + Controllers + Views
5. Тести
6. Seed (`DataField Scope=Result`, `LabEquipment`)

**Ключові сервіси:**

- `ILaboratoryJournalService`
- `ILaboratoryPdfFillService`
- `IPdfWorkspaceFillService`
- `IFieldTextLibraryService`

---

## 7. Як працювати далі

- Дотримуйся структури проєкту (Domain → Application → Infrastructure → Presentation)
- Перед кожним значним змінами перевіряй handoff-документи
- Після завершення Спринту 3 — повідом мене для апруву та переходу до Етапу 3
