# Handoff для наступного агента — UniversalLIMS

> Оновлено: 2026-05-27  
> Мова UI і доменних назв: **українська**.  
> Цей файл — короткий entrypoint. Детальний актуальний handoff по multi-sample / PDF / lab admin: `docs/handoff-multi-sample-lab-admin.md`.

---

## 0. Швидкі посилання

- Актуальний handoff гілки: `docs/handoff-multi-sample-lab-admin.md`
- Лабораторний цикл: `docs/handoff-stage-2-laboratory.md`
- PDF Fill + lifecycle версій: `docs/handoff-pdf-fill-panel-and-template-lifecycle.md`
- Політика версій шаблонів: `docs/handoff-template-versioning-policy.md`
- Гібридні теги + мапінг реєстратури: `docs/handoff-hybrid-tags-and-registry-mapping.md`
- QA на завтра (Fill + теги + ролі): `docs/qa-tomorrow-fill-tags-checklist.md`

---

## 1. Де ми зараз

| Блок | Статус |
|------|--------|
| Портал ролей, workspace, теми, quick links | Закрито |
| Реєстр замовлень + створення Order/Sample | Закрито |
| Multi-sample create (`Samples[]`) | Закрито |
| PDF Workspace scoped by `OrderDocumentId` / `SampleId` | Закрито |
| Details з групуванням документів по пробах | Закрито |
| Post-create redirect у PDF Fill | Закрито |
| Лабораторний журнал + PDF Workspace | Закрито базовий цикл |
| Admin `/Laboratories` + контекст філії | Закрито |
| UX для lab counts: workflow vs awaiting send | Закрито локальними комітами |
| `savedCount` у PDF save (реально збережені) + cleanup debug/logging | Закрито |
| `Order` -> `Registered` після успішного PDF fill save | Закрито |
| Редагування клієнта зі сторінки справи (`Orders/Details`) | Закрито |
| Inline валідація форми редагування клієнта | Закрито |
| `FieldTextLibrary` tag-first у PDF Fill | Закрито |
| PDF Fill: autosave макету разом із збереженням значень | Закрито |

**Поточна гілка:** `main`, локально попереду `origin/main` на кілька комітів. `debug-40b8bf.log` може оновлюватися локально автоматично, але його **не комітити**.

---

## 2. Що вже є в коді

### Реєстратура
- `IOrderRegistrationService` / `OrderRegistrationService` — список, створення multi-sample, details, send to lab
- `ICustomerService` — пошук/створення клієнта
- Редагування клієнта доступне прямо в `Views/Orders/Details.cshtml` (POST `Orders/UpdateCustomer`)
- `INumberingService` — `ReferralNumber`, `Sample.Number`; враховує pending Added samples у ChangeTracker
- `OrderPostCreateNavigation` — redirect у PDF Fill для single-document create

### UI / API
| URL | Опис |
|-----|------|
| `GET /Orders` | Реєстр замовлень (фільтри, пагінація) |
| `GET /Orders/Create` | Нова справа |
| `POST /Orders/Create` | Створення multi-sample order |
| `GET /Orders/Details/{id}` | Документи згруповані по пробах; fill/send actions |
| `GET /api/orders` | JSON реєстр |
| `POST /api/orders` | JSON створення |
| `GET /api/customers/search?q=` | Пошук клієнта |
| `GET /PdfWorkspace/Fill/{versionId}?orderId=&orderDocumentId=` | Заповнення PDF конкретного документа |

### PDF Workspace
- Fill values scoped by `OrderDocumentId` → `OrderDocument.SampleId`
- Legacy без `orderDocumentId`: order-level (`SampleId = null`)
- Лабораторні PDF links теж передають `orderDocumentId`
- `Saved` у результаті save рахує тільки фактичні insert/update; очищення порожніх іде окремим лічильником
- `Order` переводиться в `Registered` після успішного save без помилок полів
- `FieldTextLibrary`: бібліотека текстів у Fill працює в режимі **tag-first** (`TemplateField.Tag`/`NormalizedTag`); `DataFieldId` лишається fallback для legacy

### Лабораторія / Admin
- `LaboratoryJournalService` — журнал: один рядок на пробу
- Лабораторне заповнення виконується напряму в PDF Workspace; окрема сторінка «Показники» / табличний ResultEntry прибрана
- `FieldTextLibrary` використовується для бібліотеки текстів під теги шаблонів
- `/Laboratories` — огляд філій, session context через `LaboratoryBranchContext`
- `LaboratoryOverviewService` — рахує `ActiveSampleCount` для lab workflow і `AwaitingSendSampleCount` для Pending

### Тести
- `OrderRegistrationServiceTests`
- `PdfWorkspaceFillServiceTests`
- `LaboratoryPdfFillServiceTests`
- `LaboratoryOverviewServiceTests`
- `NumberingServiceTests`
- `OrderPostCreateNavigationTests`
- `LaboratoryJournalServiceTests`
- `PortalEntryFlowTests`

---

## 3. Критично: архітектурні заборони (урок з інциденту)

### ❌ НЕ повертати «Показники» / табличний ResultEntry

Це вже прибирали вручну і повторно прибрали в цій сесії. Не відновлювати:
- кнопку «Показники» в лабораторному журналі;
- `LaboratoryController.ResultEntry`;
- `ResultEntryService`, `IResultEntryService`, `ResultEntryDtos`;
- табличну сторінку `Views/Laboratory/Results.cshtml`.

Правильний workflow: лаборант відкриває **PDF** з журналу; значення вносяться у PDF Workspace через теги шаблону, permissions і `FieldTextLibrary`.

### ❌ НЕ інжектити `ApplicationDbContext` в `ICurrentUserService`

Цикл DI:

```
DbContext → Interceptors → CurrentUserService → DbContext
```

→ старт зависає, `ERR_CONNECTION_REFUSED`, множинні `UniversalLIMS.exe`.

**Філія користувача зараз:**
- claim `LimsClaimTypes.BranchId` (з `ApplicationUserClaimsPrincipalFactory`)
- seed проставляє `ApplicationUser.BranchId` у БД
- після зміни філії в БД — **повторний вхід** (claims оновляться)

**Якщо потрібно читати BranchId з БД без re-login:**
- окремий `IUserBranchResolver` + `IDbContextFactory` **без interceptors** і **Scoped** lifetime, або
- оновлення claims у `SignInManager`/після логіну — **не** через основний `DbContext` у `CurrentUserService`.

### PDF Workspace без orderId
- **Production:** `InvalidOperationException` — спочатку створити замовлення в `/Orders/Create`
- **Development:** дозволено auto-order (тестовий клієнт) — `IHostEnvironment.IsDevelopment()`

---

## 4. Поточний git / перевірки

Перед новою роботою:

```powershell
git status
dotnet build UniversalLIMS/UniversalLIMS.csproj
dotnet test UniversalLIMS.Tests/UniversalLIMS.Tests.csproj --filter "FullyQualifiedName~OrderRegistrationServiceTests|FullyQualifiedName~PdfWorkspaceFillServiceTests|FullyQualifiedName~LaboratoryPdfFillServiceTests|FullyQualifiedName~LaboratoryOverviewServiceTests|FullyQualifiedName~NumberingServiceTests|FullyQualifiedName~OrderPostCreateNavigationTests|FullyQualifiedName~LaboratoryJournalServiceTests"
```

Останній focused прогін: build зелений, 52 тести зелені.

**Не комітити:** `debug-40b8bf.log`, `_runout.txt`, `_runerr.txt`, `.vs/`, `bin/`, `obj/`.

---

## 5. Ручний QA зараз

### Реєстратор
1. Створити замовлення з 2+ дослідженнями: різні `Sample.Number`, без `IX_Samples_OrderId_Number`.
2. Відкрити Details: документи згруповані по пробах.
3. Заповнити PDF кожного документа окремо: значення не змішуються між `OrderDocumentId`.
4. Для single-document create + «Відкрити PDF»: URL містить `orderDocumentId`.

### Адміністратор
1. Перемкнути активну роль на **Адміністратор**.
2. `/Laboratories`: перевірити різницю «У workflow» vs «Очікує відправки».
3. Відкрити журнал усіх лабораторій і журнал конкретної філії.
4. Перемкнутися назад на Лаборанта: навігація має змінитися.

### Лаборант
1. Відкрити журнал: видно лише відправлені в лабораторію проби.
2. Для multi-sample: partial send має показувати тільки відправлені документи.
3. Якщо у проби кілька PDF, `Results` має вести через `ChooseDocument` або відкривати конкретний `orderDocumentId`.
4. Перевірити, що в журналі немає кнопки «Показники»; робота йде через PDF і бібліотеку текстів тегів.

### Важлива QA-нотатка

`/Orders` показує всі створені проби/документи. `/Laboratories` і журнал рахують тільки те, що вже відправлено в лабораторію (`OrderDocumentStatus != Pending`). Pending документи тепер додатково видно як «Очікує відправки».

---

## 6. Беклог

### Лабораторія
- [ ] Етап 3–4: експерт, протоколи, висновки.

### Реєстратура
- [x] `Order` → Registered після успішного PDF fill.
- [x] Редагування клієнта з реєстру.

### PDF Workspace
- Детально: **`docs/handoff-pdf-fill-panel-and-template-lifecycle.md`** (панель, layout save, версії)
- Значення замовлення: `docs/handoff-pdf-workspace-fill.md`
- [x] `savedCount` = реально збережені записи (не `Received`)
- [x] Прибрати тимчасові `Console.WriteLine` / debug log
- [x] **Republish** — `RepublishAsync` для Superseded
- [x] **Дві дати публікації** — `FirstPublishedAtUtc` + `RepublishedAtUtc`
- [x] Unit-тест `SaveFillLayoutRefinementAsync` для Published
- [x] `FieldTextLibrary` — ключ по тегу (`NormalizedTag`), не по спільному `DataFieldId`
- [x] Autosave layout після успішного save values, якщо `isLayoutDirty`

### UI / дрібниці
- [ ] Логотип ЦКПХ у hero порталу

---

## 7. SSOT чеклист (завжди перед merge)

- [ ] `Customer.FullName` — з `Customers`, не `OrderFieldValue`
- [ ] `Sample.Number` — колонка `Samples`, через `INumberingService`
- [ ] `Order.CustomerId` — без копії ПІБ на `Order`
- [ ] Лабораторне заповнення — через PDF Workspace / теги шаблону / `FieldTextLibrary`, без окремого табличного ResultEntry
- [ ] `ISoftAnnulled` на annul, не DELETE
- [ ] `dotnet test .\UniversalLIMS.sln` — зелений

---

## 8. Локальний запуск

```powershell
sqllocaldb start MSSQLLocalDB
cd UniversalLIMS
dotnet run --launch-profile https
# https://localhost:7113
```

**Якщо build: file locked by UniversalLIMS:**
```powershell
taskkill /F /IM UniversalLIMS.exe
```

**Ролі:** Registrar → `/Orders`, `/Orders/Create`, `/PdfWorkspace`  
**Лаборант:** `/Laboratory`  
**Адміністратор:** `/Laboratories`, після активної ролі **Адміністратор**.

---

## 9. Стартовий prompt для агента

```text
Проєкт UniversalLIMS (.NET 8 MVC). Прочитай:
1) docs/handoff-next-agent.md
2) docs/handoff-multi-sample-lab-admin.md
3) docs/handoff-stage-2-laboratory.md — якщо задача про лабораторію
4) docs/handoff-pdf-fill-panel-and-template-lifecycle.md — якщо задача про PDF Fill / шаблони / публікацію

Контекст: multi-sample flow, PDF scoped by OrderDocumentId, admin /Laboratories,
post-create redirect, portal role switch, sample numbering fix. НЕ інжектуй ApplicationDbContext в CurrentUserService.

Задача: [уточнити — QA або беклог §6].
Правила: не відкатувати чужі зміни; не комітити debug-40b8bf.log.
Режим роботи: комітити не після кожного мікрокроку, а після завершеного логічного блоку (30-90 хв).
Формат коміту: багаторядковий (subject + 3-6 bullets з описом змін).
Після етапу дати короткий звіт і текст commit message.
```

---

## 10. Ключові файли

| Область | Файли |
|---------|--------|
| Реєстратура | `OrderRegistrationService.cs`, `OrdersController.cs`, `Views/Orders/` |
| Multi-sample UI | `Views/Orders/Create.cshtml`, `wwwroot/js/order-create.js` |
| PDF | `PdfWorkspaceFillService.cs`, `PdfWorkspaceController.cs`, `pdf-workspace-fill.js` |
| Лабораторія | `LaboratoryController.cs`, `LaboratoryJournalService.cs`, `LaboratoryPdfFillService.cs`, `Views/Laboratory/` |
| Admin labs | `LaboratoriesController.cs`, `LaboratoryOverviewService.cs`, `Views/Laboratories/Index.cshtml` |
| Security | `LimsPolicies.cs`, `RequireActiveLimsRoleAttribute`, `ApplicationUserClaimsPrincipalFactory` |
| Seed | `DataSeeder.cs` |
| Навігація | `WorkspaceNavigationCatalog.cs` |
| Доки | `handoff-multi-sample-lab-admin.md`, `handoff-pdf-fill-panel-and-template-lifecycle.md`, `handoff-pdf-workspace-fill.md`, `handoff-stage-1-registration.md`, `handoff-stage-2-laboratory.md` |
