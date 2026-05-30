# Handoff: v1 — видача та повернення в лабораторію

> Оновлено: 2026-05-30

## Що додано

### Видача (реєстратор)

- `SampleDeliveryStatus` на `Sample`: `None` → `ReadyForPickup` → `Issued`
- Після **затвердження експертом** проба автоматично стає `ReadyForPickup`
- Сторінка **`/Issuance`** — реєстр готових до видачі; кнопка **«Видано»**
- Архів: `/Issuance?showIssued=true`
- Метрика на workspace реєстратора: **«Готово до видачі»**

### Повернення на доопрацювання (експерт)

- `ExpertConclusionStatus.ReturnedForRework`
- У черзі експерта (статус «В роботі»): кнопка **«У лабораторію»** + обов’язкова причина
- Скидає: review → `ReturnedForRework`, документи → `InProgress`, проба → `InProgress`, видача → `None`

## Міграція

```powershell
dotnet ef database update --project UniversalLIMS\UniversalLIMS.csproj --startup-project UniversalLIMS\UniversalLIMS.csproj
```

Міграція `20260530063942_AddSampleDeliveryAndExpertRework` також проставляє `ReadyForPickup` для уже затверджених проб.

## Сповіщення та філії

Див. `docs/architecture-branches-workstations-notifications.md` — цикл poll, 5–10 ПК, кілька пунктів реєстратури через `Branch`.

## Smoke QA

1. Експерт затверджує пробу → у реєстратора з’являється в `/Issuance`.
2. Реєстратор натискає «Видано» → проба в архіві `?showIssued=true`.
3. Експерт у «В роботі» → «У лабораторію» з причиною → проба зникає з черги експерта, у лабораторії документ `InProgress`.

## Файли

| Область | Файли |
|---------|--------|
| Домен | `SampleDeliveryStatus.cs`, `Sample.cs`, `ExpertConclusionStatus.cs`, `ExpertConclusionReview.cs` |
| Сервіси | `SampleDeliveryService.cs`, `ExpertConclusionService.cs` |
| UI | `IssuanceController.cs`, `Views/Issuance/Index.cshtml`, `Expert/Index.cshtml`, `_ExpertSampleActions.cshtml` |
| Тести | `SampleDeliveryServiceTests.cs`, оновлені `ExpertConclusionServiceTests.cs` |
