# Handoff для наступного агента — UniversalLIMS

> Оновлено: 2026-05-25  
> Мова UI і доменних назв: **українська**.

---

## 1. Де ми зараз

| Блок | Статус |
|------|--------|
| Портал ролей, workspace, теми, quick links | ✅ Закрито (коміт `1d683d9`) |
| PDF Workspace + конструктор шаблонів (адмін/реєстратор) | ✅ Працює |
| **PR-1** — Реєстр замовлень (read-only) | ✅ Реалізовано, **не закомічено** |
| **PR-2** — Створення Order/Sample + PDF redirect | ✅ Реалізовано, **не закомічено** |
| **Спринт 3** — Лабораторний журнал | ⏳ **Наступний пріоритет** (див. `handoff-stage-2-laboratory.md`) |

**Рішення продукту:** спочатку реєстратура (A) — зроблено; далі лабораторія (B).

---

## 2. Що вже є в коді (реєстратура)

### Сервіси
- `IOrderRegistrationService` / `OrderRegistrationService` — список + створення замовлення
- `ICustomerService` — пошук/створення клієнта
- `INumberingService` — `ReferralNumber`, `Sample.Number`
- `IPdfWorkspaceFillService` — fill/save/final PDF

### UI / API
| URL | Опис |
|-----|------|
| `GET /Orders` | Реєстр замовлень (фільтри, пагінація) |
| `GET /Orders/Create` | Нова справа |
| `POST /Orders/Create` | → redirect на PDF Fill |
| `GET /api/orders` | JSON реєстр |
| `POST /api/orders` | JSON створення |
| `GET /api/customers/search?q=` | Пошук клієнта |
| `GET /PdfWorkspace/Fill/{versionId}?orderId=` | Заповнення PDF |

### Навігація
- `WorkspaceNavigationCatalog` — пункт **Замовлення** для `Registrar`

### Seed
- `DataSeeder.AssignDefaultBranchesToUsersAsync` — користувачам без `BranchId` → філія **ZHY**

### Тести
- `OrderRegistrationServiceTests` (4+3 тести)
- `PortalEntryFlowTests`
- `PdfWorkspaceFillServiceTests` (+ `TestHostEnvironment` для dev auto-order)

---

## 3. Критично: архітектурні заборони (урок з інциденту)

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

## 4. Незакомічені зміни (зробити першим кроком)

```powershell
git status
dotnet test .\UniversalLIMS.sln
```

**Рекомендований коміт (один або два PR):**

```text
feat(registration): order registry, create flow, and customer search API

- Orders index/create UI for registrar
- IOrderRegistrationService with SSOT customer name
- Block implicit PDF orders outside Development
- Seed default branch ZHY for users without BranchId
```

**Не комітити:** `debug-40b8bf.log`, `_runout.txt`, `_runerr.txt`, `.vs/`, `bin/`, `obj/`.

---

## 5. Наступний великий блок — Спринт 3 (лабораторія)

Документ: **`docs/handoff-stage-2-laboratory.md`**

### Порядок PR

#### PR-3a — Журнал проб (read-only)
1. `ILaboratoryJournalService.GetSamplesAsync(SampleJournalFilter)` → `PagedResult<SampleJournalItemDto>`
2. Фільтри: номер проби, дата, статус, філія (`ICurrentUserService.BranchId`)
3. `LaboratoryController` + `Views/Laboratory/Index.cshtml`
4. Policy: `LimsPolicies.EnterLaboratoryResults`
5. Увімкнути quick links лаборанта в `WorkspaceNavigationCatalog`

#### PR-3b — Внесення результатів
1. `IResultEntryService` — CRUD `SampleResultValue` (annul + new row, ISO)
2. `IResultFieldPermissionService` — FLS для `DataFieldScope.Result`
3. `ISampleWorkflowService` — переходи `SampleStatus` / `OrderDocumentStatus`
4. Форма внесення результатів по `SampleId`
5. Seed: `DataField` з `Scope = Result`, `Equipment`

#### PR-3c — Тести + seed
- Unit/integration на journal, result entry, FLS deny
- Перевірка: результати **не** в `OrderFieldValue`

### Домен уже в БД
- `SampleResultValue`, `Equipment` — сутності є
- `SampleStatus`: `Registered`, `Routed`, `InProgress`, `ResultsEntered`

---

## 6. Паралельний беклог (за бажанням)

### Реєстратура (допиляти A)
- [ ] Статус `Order` → `Registered` після успішного fill PDF
- [ ] `SentToLab` на `OrderDocument` при відправці в лабораторію
- [ ] Редагування клієнта з реєстру
- [ ] Оновлення claims філії при логіні (без циклу DI)

### PDF Workspace (`docs/handoff-pdf-workspace-fill.md`)
- [ ] `savedCount` = реально збережені записи (не `Received`)
- [ ] Прибрати тимчасові `Console.WriteLine` / debug log
- [ ] Прогін на шаблоні «Повітря закритих приміщень v32»

### UI / дрібниці
- [ ] Логотип ЦКПХ у hero порталу
- [ ] Адмін: пункт «Замовлення» (опційно)

---

## 7. SSOT чеклист (завжди перед merge)

- [ ] `Customer.FullName` — з `Customers`, не `OrderFieldValue`
- [ ] `Sample.Number` — колонка `Samples`, через `INumberingService`
- [ ] `Order.CustomerId` — без копії ПІБ на `Order`
- [ ] Лабораторні значення — лише `SampleResultValue`
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
**Лаборант:** поки заглушки в quick links — після PR-3a.

---

## 9. Стартовий prompt для агента

```text
Проєкт UniversalLIMS (.NET 8 MVC). Прочитай docs/handoff-next-agent.md та docs/handoff-stage-2-laboratory.md.

Контекст: PR-1/PR-2 реєстратури реалізовані локально (Orders, Create, API). НЕ інжектуй ApplicationDbContext в CurrentUserService.

Задача: PR-3a — лабораторний журнал проб (read-only) для LaboratoryTechnician.
Дотримуйся SSOT, ICurrentUserService.BranchId, LimsPolicies.EnterLaboratoryResults.
Почни з ILaboratoryJournalService + тести, потім Controller/View.
```

---

## 10. Ключові файли

| Область | Файли |
|---------|--------|
| Реєстратура | `OrderRegistrationService.cs`, `OrdersController.cs`, `Views/Orders/` |
| PDF | `PdfWorkspaceFillService.cs`, `PdfWorkspaceController.cs` |
| Security | `LimsPolicies.cs`, `RequireActiveLimsRoleAttribute`, `ApplicationUserClaimsPrincipalFactory` |
| Seed | `DataSeeder.cs` |
| Навігація | `WorkspaceNavigationCatalog.cs` |
| Доки | `handoff-stage-1-registration.md`, `handoff-stage-2-laboratory.md`, `handoff-pdf-workspace-fill.md` |
