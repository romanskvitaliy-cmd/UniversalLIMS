# REF-MOZ-001 — підготовка шаблону направлення (адмін)

> **Задача roadmap:** D-контент-1  
> **Оновлено:** 2026-05-31  
> **Аудиторія:** адмін LIMS, технічний куратор пілоту ЦКПХ

## Що в репозиторії

| Файл | Призначення |
|------|-------------|
| `docs/assets/templates/REF-MOZ-001.pdf` | PDF-макет для Upload (спрощений пілотний бланк) |
| `docs/data/protocol-tags-ref.json` | Каталог тегів REF_* + SSOT-ключі |
| `docs/data/ref-moz-001-field-map.json` | Орієнтовні координати полів для Map |
| `docs/tools/GenerateRefMoz001Pdf/` | Перегенерація PDF після правки макету |

> **Офіційний бланк МОЗ:** коли з’явиться погоджений Word/PDF від ЦКПХ — замініть файл у Upload **новою версією** того ж шаблону `REF-MOZ-001`. Теги та координати збережіть або відкалібруйте заново.

## Крок 1 — Templates

1. `/Templates` → **Створити**
2. **Код:** `REF-MOZ-001`
3. **Назва:** `Направлення на лабораторне дослідження (пілот)`
4. **Призначення:** `Направлення` (`TemplatePurpose.Referral`)
5. Зберегти → відкрити **Details**

## Крок 2 — Upload PDF

1. **Versions** → **Upload**
2. Файл: `docs/assets/templates/REF-MOZ-001.pdf`
3. Після upload — **Map** (конструктор полів)

## Крок 3 — Map (розставити теги)

У dropdown «оберіть тег» з’явились групи:

- **SSOT — замовник, справа, проба, філія** (`Order.ReferralNumber`, `Customer.*`, `Sample.*`, `Branch.*`) — автозаповнення зі справи
- **Направлення REF-MOZ (префікс REF_)** — поля для мапінгу з протоколами

Орієнтовні координати — у `docs/data/ref-moz-001-field-map.json`.  
Порядок роботи:

1. Додати поле → обрати тег → розмістити на лінії бланку
2. Для багаторядкових (`REF_InvestigationPurpose`, `REF_Indicators`) — збільшити висоту сегмента
3. **Калібрування:** якщо текст «пливе» — scope «шаблон» у Fill або зсув у Map
4. Не використовувати auto-match за підписом — лише exact tag

### Мінімальний набір для пілоту

| Тег | Джерело значення |
|-----|------------------|
| `Order.ReferralNumber` | SSOT — номер справи |
| `Customer.FullName`, `Customer.Address`, `Customer.ContactPhone` | SSOT — замовник |
| `REF_SamplingDate`, `REF_SamplingLocation`, `REF_SampleName` | Ввід + **MapOrderFields** з протоколом |
| `Sample.Number`, `Branch.Name` | SSOT — проба / філія |

## Крок 4 — Permissions

`/TemplateFields/Permissions` для версії:

| Роль | Доступ |
|------|--------|
| **Registrar** | **Write** на всі поля REF |
| LaboratoryTechnician | **None** (направлення не заповнює lab) |
| Specialist | **None** |
| Administrator | Write (за потреби) |

## Крок 5 — Publish

1. Перевірити preview PDF у Map
2. **Publish** версії
3. Статус шаблону → **Active**
4. На `/Orders/Create` у рядку проби з’явиться select **«Бланк направлення REF-*»** з `REF-MOZ-001`

## Перевірка

1. Create: 1 проба + REF-MOZ-001 + протокол (напр. f327)
2. MapOrderFields: об’єднати `REF_SamplingDate` ↔ `f327_SamplingDate`
3. Fill → «Друк направлення» на Details — лише REF PDF
4. «Відправити в лабораторію» — REF **не** потрапляє в lab-журнал (лише протокол)

## Перегенерація PDF

```powershell
dotnet run --project docs\tools\GenerateRefMoz001Pdf\GenerateRefMoz001Pdf.csproj
```

Після зміни PDF — **нова версія** шаблону в UI (не перезаписувати файл опублікованої версії напряму).

## Пов’язані документи

- `docs/ref-registrar-quickstart-uk.md` — інструкція реєстратору
- `docs/glossary-registration-uk.md` — терміни справа / проба / REF
- `docs/pilot-smoke-c1-checklist.md` — сценарій D (REF, 1 проба)
- `docs/pilot-d8a-qa-checklist.md` — D8a (3 проби × REF + протокол)
