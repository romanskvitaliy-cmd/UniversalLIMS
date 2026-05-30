# D8a — QA Per Sample (3 проби × REF + протокол)

> **Roadmap:** D8a, D-контент-4 (частково)  
> **Автотест:** `PilotRefPerSampleFlowTests.D8a_ThreeSamples_ReferralStaysAtRegistrar_OnlyProtocolsRoutableToLab`  
> **Оновлено:** 2026-05-31

## Передумови

- Міграції застосовано
- **REF-MOZ-001** опубліковано на стенді (`docs/ref-moz-001-admin-setup.md`)
- Реєстратор REG-ZHY, лаборант MIX-BER (або аналог)
- D7 у `main` (групи MapOrderFields по пробах)

## Сценарій

| # | Крок | Хто | Очікування |
|---|------|-----|------------|
| 1 | `/Orders/Create` — 1 замовник, **3 рядки проб** | Реєстратор | Кожен рядок: REF-MOZ-001 + **різний** протокол (f327 / Food / інший) |
| 2 | Зберегти → `MapOrderFields` (якщо ≥2 документи) | Реєстратор | **3 секції:** «Проба 1…», «Проба 2…», «Проба 3…»; у кожній — **Направлення** + **Протокол** |
| 3 | Об’єднати `REF_SamplingDate` ↔ тег протоколу **лише в межах проби 1** | Реєстратор | Проба 2/3 — окремі групи полів |
| 4 | Завершити мапінг → Fill REF і протоколи | Реєстратор | Значення на PDF; SSOT (номер справи, замовник) підтягуються |
| 5 | `Orders/Details` → «Друк направлення» | Реєстратор | PDF **лише REF** (3 окремі документи або по пробах — залежно від UI) |
| 6 | «Відправити в лабораторію» — **лише протоколи** | Реєстратор | REF лишаються Pending / у реєстрації; протоколи SentToLab |
| 7 | Журнал лаборанта | Лаборант | **3 проби** у вхідних; REF **не** в таблиці документів lab для Fill |

## Критерії PASS

- [ ] 6 `OrderDocument` (3×REF + 3×протокол), кожен REF прив’язаний до своєї `SampleId`
- [ ] MapOrderFields: REF проби N **не** змішаний з протоколом проби M (N≠M)
- [ ] Попередження D7, якщо той самий шаблон на 2+ пробах (не блокує, але видно)
- [ ] `CanSendToLab` лише для протоколів (REF — ні)
- [ ] Після відправки — toast лаборанта (~45 с poll)

## Команда автотесту

```powershell
dotnet test UniversalLIMS.Tests/UniversalLIMS.Tests.csproj --filter "FullyQualifiedName~PilotRefPerSampleFlowTests"
dotnet test UniversalLIMS.Tests/UniversalLIMS.Tests.csproj --filter "FullyQualifiedName~PilotRefDContent4Tests"
```

## Якщо FAIL

| Симптом | Перевірити |
|---------|------------|
| Немає REF у Create | Publish REF-MOZ-001, `TemplatePurpose.Referral` |
| Одна купа полів на Map | D7: `SampleGroups` у JSON, `order-field-mapping.js` |
| REF у lab-журналі | `TargetBranchId` REF = філія реєстратора; `CanSendToLab` у Details |
| Дубль шаблону при Create | Правило: один `TemplateVersionId` на справу — обрати 3 різні протоколи |

## Пов’язані документи

- `docs/pilot-smoke-c1-checklist.md` — сценарій D (1 проба + REF)
- `docs/ref-registrar-quickstart-uk.md` — щоденний flow реєстратора
