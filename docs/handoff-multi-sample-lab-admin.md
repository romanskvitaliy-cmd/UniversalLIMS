# Handoff для нового агента — Multi-sample, PDF Workspace, лабораторія, admin flow

> Оновлено: 2026-05-27  
> Мова UI і доменних назв: **українська**.  
> Робота поетапна: короткий звіт після кожного етапу + commit message з описом у кілька рядків.

---

## 0. Швидкий контекст

**Проєкт:** UniversalLIMS (.NET 8 MVC), LIMS для ДУ «Житомирський обласний ЦКПХ МОЗ України».

**Поточний фокус (закрито в цій гілці):**
- Multi-sample order flow (реєстрація → details → PDF fill → лабораторія)
- PDF Workspace scoped by `OrderDocumentId` / `SampleId`
- Admin entry у лабораторії (`/Laboratories`)
- Хвости: post-create PDF redirect, portal role switching, distinct sample numbering

**Working tree:** чистий (останній коміт — див. таблицю §1).

**Не комітити:** `debug-40b8bf.log`, `_runout.txt`, `_runerr.txt`, `.vs/`, `bin/`, `obj/`.

---

## 1. Що зроблено (хронологія комітів)

| Коміт | Зміст |
|-------|--------|
| `a582e79` | Multi-sample UI create (`Samples[]`, `order-create.js`) |
| `560cfbb` | Details групує документи по пробах |
| `9e7f1d5` | Index/success message з кількістю проб |
| `32a1242` | PDF fill scoped by `OrderDocumentId` |
| `877835e` | Lab PDF links передають `orderDocumentId` |
| `73bb978` | Test: result completion не чіпає sibling sample |
| `395bb5d` | Admin `/Laboratories` + nav + metrics |
| `79c817b` | Post-create PDF redirect + portal role switch UX |
| `9ef3360` | Fix duplicate sample numbers у multi-sample create |
| `465b76e` | UX: пояснення лічильників lab overview (Pending не рахуються) |
| `ce063b0` | Метрика «Очікує відправки» на `/Laboratories` |
| `19c0947` | Підказка в реєстрі замовлень про відправку в лабораторію |
| `693ddd0` | Оновлено `handoff-next-agent.md` під актуальний стан |

---

## 2. Архітектура multi-sample (ключове)

### Реєстрація

- `OrderCreateInputModel.Samples[]` → `CreateOrderRequest.Samples`
- `OrdersController.GetSubmittedSamples()` — приймає лише `Samples[]` з create-form; старий top-level fallback прибрано
- `OrderRegistrationService.CreateOrderAsync` — цикл по `samplePlans`, кожна проба + свої `OrderDocument`
- **Нумерація проб:** `NumberingService.AssignSampleNumberAsync` враховує pending `Added` samples у `ChangeTracker` (fix `9ef3360`)

### Details / PDF

- `OrderDetailDto.Samples[]`, `OrderDocumentItemDto.SampleId`
- Посилання «Заповнити» передають `orderDocumentId`
- Post-create redirect (1 документ + `OpenPdfAfterCreate`): `OrderPostCreateNavigation.TryGetSingleDocumentPdfFillRoute` → Fill з `orderDocumentId`

### PDF Workspace

- `IPdfWorkspaceFillService` при наявному `orderDocumentId` читає/пише `OrderFieldValue` з `SampleId` конкретного `OrderDocument`
- Legacy без `orderDocumentId`: order-level (`SampleId = null`)
- Тест: `SaveValuesAsync_ScopesValuesToOrderDocumentSample` (`PdfWorkspaceFillServiceTests`)

### Лабораторія

- Журнал: `LaboratoryJournalService` — один рядок на **пробу**
- PDF fill: `LaboratoryController.Results` → `ChooseDocument` або redirect; у Fill передається `orderDocumentId`
- Result entry: `ResultEntryService` → workflow лише для `document.SampleId == sampleId`; complete може бути scoped до конкретного `OrderDocumentId`
- Admin: `/Laboratories` — огляд філій, вхід у журнал з session context (`LaboratoryBranchContext`)
- **Лічильники:** реєстр показує всі проби/документи; lab overview/journal — лише після `SendDocumentsToLab` (`Status != Pending`). Метрика `AwaitingSendSampleCount` — проби з документами «Очікує» по `TargetBranch`.

### Ролі (важливо для QA)

- **Identity role** ≠ **активна робоча роль** (сесія `ActiveLimsRole`)
- Адміністратор бачить «Лабораторії» лише коли активна роль **Адміністратор**, не Лаборант
- На порталі: «Перейти як …» для перемикання між доступними ролями

---

## 3. Ключові файли

| Область | Файли |
|---------|--------|
| Create multi-sample | `Views/Orders/Create.cshtml`, `wwwroot/js/order-create.js`, `OrdersController.cs` |
| Details | `Views/Orders/Details.cshtml`, `OrderRegistrationService.GetOrderDetailAsync` |
| PDF fill | `PdfWorkspaceFillService.cs`, `PdfWorkspaceController.cs`, `pdf-workspace-fill.js` |
| Post-create nav | `OrderPostCreateNavigation.cs`, `OrdersController` |
| Numbering | `NumberingService.cs`, `NumberingServiceTests.cs` |
| Lab journal | `LaboratoryController.cs`, `LaboratoryJournalService.cs`, `Views/Laboratory/Index.cshtml` |
| Admin labs | `LaboratoriesController.cs`, `LaboratoryOverviewService.cs`, `Views/Laboratories/Index.cshtml` |
| Role portal | `Views/Home/Index.cshtml`, `WorkspaceNavigationCatalog.cs`, `ActiveLimsRoleService` |
| Тести | `OrderRegistrationServiceTests`, `PdfWorkspaceFillServiceTests`, `LaboratoryPdfFillServiceTests`, `ResultEntryServiceTests`, `SampleWorkflowServiceTests`, `LaboratoryOverviewServiceTests` |

---

## 4. Перевірки (останній прогін)

```powershell
dotnet build UniversalLIMS/UniversalLIMS.csproj
dotnet test UniversalLIMS.Tests/UniversalLIMS.Tests.csproj --filter "FullyQualifiedName~OrderRegistrationServiceTests|FullyQualifiedName~PdfWorkspaceFillServiceTests|FullyQualifiedName~LaboratoryPdfFillServiceTests|FullyQualifiedName~ResultEntryServiceTests|FullyQualifiedName~SampleWorkflowServiceTests|FullyQualifiedName~LaboratoryOverviewServiceTests|FullyQualifiedName~NumberingServiceTests|FullyQualifiedName~OrderPostCreateNavigationTests"
```

Раніше проходили: Laboratory journal tests 7/7, lab workflow 19/19, focused registration 35/35.

---

## 5. Ручний QA (наступний крок для людини/агента)

### QA-нотатка: «3 документи в реєстрі, 1 у лабораторіях»

**Очікувана поведінка (не баг):** після create усі документи `Pending`, workflow «Заповнення реєстратором». Вони **не** входять у lab overview. Число «1» на `/Laboratories` — інша раніше відправлена проба або одна відправлена з поточного замовлення. Після «Відправити в лабораторію» для всіх 3 — overview +3 (розподіл по `TargetBranch`).

### Реєстратор
1. Створити замовлення з **2+ дослідженнями** → без SqlException, різні номери проб
2. Details → fill PDF **кожного** документа окремо → значення не змішуються
3. Create з 1 документом + «Відкрити PDF» → URL містить `orderDocumentId`

### Адміністратор
4. Портал → **Увійти як Адміністратор** (не Лаборант)
5. **Лабораторії** → перевірити «Очікує відправки» vs «У workflow»; картки філій → журнал ZHY / усі філії
6. «Змінити роль» → «Перейти як Лаборант» → інший nav

### Лаборант
7. Журнал → PDF / Показники по пробі
8. Multi-sample order: partial send to lab, fill у лабораторії з правильним документом
9. Проба з 2 PDF: «Результати внесено» для одного документа не закриває sibling document; статус проби стає `ResultsEntered` лише коли всі lab-документи завершені.

---

## 6. Відомі обмеження / беклог

| Пріоритет | Задача |
|-----------|--------|
| Низький | `Order` → Registered після успішного PDF fill |
| Низький | `savedCount` у PDF save response; прибрати debug `Console.WriteLine` / `debug-40b8bf.log` usage |
| Roadmap | Етап 3–4: експерт, протоколи, висновки |

---

## 7. Архітектурні заборони (з попередніх handoff)

- **НЕ** інжектити `ApplicationDbContext` в `ICurrentUserService` (цикл DI)
- SSOT: `Customer.FullName`, `Sample.Number` — не з `OrderFieldValue`
- Лабораторні значення — лише `SampleResultValue`
- Annul, не DELETE (`ISoftAnnulled`)

---

## 8. Локальний запуск

```powershell
sqllocaldb start MSSQLLocalDB
cd UniversalLIMS
dotnet run --launch-profile https
# https://localhost:7113 (або http://localhost:5178)
```

Якщо `UniversalLIMS.exe` locked: `taskkill /F /IM UniversalLIMS.exe`

**Demo roles:** Registrar, LaboratoryTechnician, SystemAdministrator (Diagnostics може promote to admin).

---

## 9. Стартовий prompt для агента

```text
Проєкт UniversalLIMS (.NET 8 MVC). Прочитай:
1) docs/handoff-multi-sample-lab-admin.md (цей файл)
2) docs/handoff-stage-2-laboratory.md — лабораторний цикл
3) docs/handoff-pdf-fill-panel-and-template-lifecycle.md — PDF Fill / шаблони

Контекст: multi-sample flow, PDF scoped by OrderDocumentId, admin Laboratories, 
sample numbering fix — закомічено. Working tree чистий.

Задача: [уточнити — зазвичай ручний QA або наступний беклог].

Правила:
- Не відкатувати чужі зміни
- Не комітити debug-40b8bf.log
- Після етапу: короткий звіт + commit message з bullet-описом
- dotnet test перед merge
```

---

## 10. Шаблон commit message (для наступних етапів)

```text
feat(scope): короткий заголовок

- Пункт 1 що змінилось
- Пункт 2 поведінка / тест
- Пункт 3 якщо є migration/UI
```

Приклади з цієї сесії:

```text
feat(admin): add laboratory overview and admin entry flow

- Laboratories overview page lists branches with workflow sample counts
- Admin can enter journal for all labs or a selected branch via session context
- Admin workspace nav, quick links, metrics, and primary action point to laboratories
```

```text
fix(registration): assign distinct sample numbers in multi-sample create

- NumberingService considers pending Added samples when computing next sequence
- Multi-sample order creation no longer violates IX_Samples_OrderId_Number
```
