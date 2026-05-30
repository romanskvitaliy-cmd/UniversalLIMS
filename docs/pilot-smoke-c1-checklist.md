# C1 — Smoke QA пілот ЦКПХ (Житомир)

> **Призначення:** ручна перевірка end-to-end ланцюжка на стенді пілоту.  
> **Автоматизація:** `UniversalLIMS.Tests/Pilot/PilotSmokeFlowTests.cs` (сервісний рівень).  
> **Оновлено:** 2026-05-30

## Передумови

- Міграції застосовано (останній: `20260530201022_AddTemplatePurpose`)
- Філії пілоту: **ZHY**, **BER**, **MIX-BER**, **MIX-KOR** (або аналог)
- Користувачі з `BranchId`:
  - реєстратор REG-ZHY
  - лаборант MIX-BER / MIX-KOR
  - експерт `expertLIMS@gmail.com` (BER) + другий експерт KOR
- REF-шаблон опубліковано (або тимчасово без REF — лише протокол)
- Дзвіночок у navbar увімкнено (toast/голос)

## Сценарій A — два експерти, різні філії

| # | Крок | Хто | Очікування |
|---|------|-----|------------|
| 1 | Create: 2 проби → BER + KOR | Реєстратор | 2 `OrderDocument` на пробу (REF+протокол якщо є REF) |
| 2 | MapOrderFields (якщо ≥2 шаблони) | Реєстратор | Спільні поля один раз |
| 3 | Fill + «Відправити в лабораторію» | Реєстратор | Документи «У лабораторії» |
| 4 | Poll ~45 с або refresh | Лаборант BER | Toast «нова проба»; **KOR не бачить BER** |
| 5 | SampleDetails → Fill PDF → «Відправити експерту» | Лаборант BER/KOR | Проба `ResultsEntered` |
| 6 | Poll | Експерт BER | Toast; черга `/Expert` — **лише BER-проба** |
| 7 | Poll | Експерт KOR | **Лише KOR-проба** |
| 8 | Approve BER | Експерт BER | Toast реєстратора → `/Issuance` |
| 9 | «Видано» | Реєстратор | Архів `?showIssued=true` |

## Сценарій B — rework (KOR)

| # | Крок | Хто | Очікування |
|---|------|-----|------------|
| 1 | У черзі «В роботі» → «У лабораторію» + причина | Експерт KOR | Проба зникає з черги експерта |
| 2 | Poll | Лаборант KOR | Toast rework з **причиною** → `/Laboratory/SampleDetails/{id}` |
| 3 | Лаборант BER | — | **Не** отримує toast KOR rework |
| 4 | Fill → «Відправити експерту» знову | Лаборант KOR | Проба знову в черзі KOR експерта |

## Сценарій C — адмін hub (G5/G6)

| # | Сторінка | Очікування |
|---|----------|------------|
| 1 | `/Laboratories` | Блок «Експертиза», лічильники черги, лінк `/Expert` |
| 2 | `/Laboratories` | Картки MIX-BER/KOR + «Користувачі філії» |
| 3 | `/Users` | Badge типу філії; чіпи фільтра; фільтр по філії з картки |

## Сценарій D — REF (якщо шаблон готовий)

| # | Крок | Очікування |
|---|------|------------|
| 1 | Create: REF + протокол у рядку проби | 2 документи на пробу |
| 2 | MapOrderFields | REF проби 1 + протокол проби 1 (не з пробою 2) |
| 3 | «Друк направлення» на Details | Лише REF PDF |

## Критерії PASS

- [ ] B1/B2: експерт бачить лише проби своєї філії (BER ≠ KOR)
- [ ] C6: toast після входу (lookback 24 год), не лише «з моменту відкриття»
- [ ] C2: rework toast з причиною → SampleDetails
- [ ] Issuance після approve; «Видано» в архіві
- [ ] REF не потрапляє в lab-журнал (лише протоколи)

## Якщо FAIL

| Симптом | Перевірити |
|---------|------------|
| Експерт бачить чужі проби | `User.BranchId`, `ExpertReviewQueueService.ApplyExpertBranchFilter` |
| Немає toast лаборанта | `TargetBranchId` документа, poll API `/api/laboratory/notifications/incoming` |
| Rework без причини | `ReworkReasonUk` у `ExpertConclusionReview`, JS `lims-route-notify.js` |
| Немає в Issuance | `SampleDeliveryStatus` після `ApproveAsync` |

## Команда тестів (локально)

```powershell
dotnet test UniversalLIMS.Tests/UniversalLIMS.Tests.csproj --filter "FullyQualifiedName~PilotSmokeFlowTests"
```
