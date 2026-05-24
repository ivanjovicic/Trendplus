# Codex Task Checklist for Trendplus

## Stop rules

Prekini i prijavi stanje ako:
- ne možeš da utvrdiš source of truth
- ista komanda puca dva puta
- build traje predugo ili environment blokira proveru
- task traži promene u više nepovezanih oblasti
- vidiš konflikt između frontend i backend semantike
- ne znaš da li je stanje empty ili error

Ne nastavljaj naslepo.

---

## Pre taska

- [ ] Znam koji ekran/endpoint menjam.
- [ ] Znam da li je recommendation, signal ili report.
- [ ] Znam koji backend endpoint hrani UI.
- [ ] Proverio/la sam shared helper/component.
- [ ] Proverio/la sam da li postoje testovi.
- [ ] Planiram mali scope.

---

## Analytics frontend checklist

- [ ] `AnalyticsTrustHeader` postoji ili je opravdano izostavljen.
- [ ] Error koristi `AnalyticsErrorState`.
- [ ] Empty koristi `AnalyticsEmptyState`.
- [ ] API error ne prikazuje KPI nule.
- [ ] `insufficient_data` ne izgleda kao validna preporuka.
- [ ] Koriste se shared formatteri.
- [ ] Nema lokalnog recommendation scoring-a.
- [ ] Data quality link postoji kada treba.
- [ ] Refresh/freshness je vidljiv.
- [ ] Nema mojibake.
- [ ] CSS koristi theme tokene.

---

## Analytics backend checklist

- [ ] Empty dataset ima empty meta.
- [ ] SQL/missing MV/timeout nema fake zero.
- [ ] Fallback/partial ima warning meta.
- [ ] Error ima correlationId ako je dostupan.
- [ ] Error logging je safe za dugačke poruke.
- [ ] `lastRefreshAtUtc` nije izmišljen.
- [ ] `dataQualityStatus` nije fake `good`.

---

## Supplier scorecard checklist

- [ ] requested period je vidljiv.
- [ ] effective dataset je vidljiv.
- [ ] no silent fallback.
- [ ] fallback -> recommendationAllowed=false.
- [ ] UI kaže `Pomoćni signal` ako nije finalno.
- [ ] empty state objašnjava razloge.
- [ ] zero revenue rows nisu validan signal.
- [ ] missing supplier name je rešen ili prijavljen.

---

## Product Decision checklist

- [ ] Backend daje recommendation.
- [ ] Svaki red ima reason/reasonCodes.
- [ ] confidence/reliability dolaze iz backend-a.
- [ ] Data quality blocker vidljiv.
- [ ] Add to Action Queue koristi sourceKey.
- [ ] Queue duplikati sprečeni.
- [ ] "Zašto?" objašnjenje postoji.

---

## Inventory checklist

- [ ] Dopuna/OOS/dead stock/transfer jasni.
- [ ] Workflow predlozi mogu u Action Queue.
- [ ] Već dodati predlog ima `U centralnim akcijama`.
- [ ] Export/scheduler je sekundaran.
- [ ] Greške panela ne ruše celu stranu.

---

## Action Queue checklist

- [ ] sourceType/sourceKey stabilni.
- [ ] status validacija backend + service.
- [ ] `done/rejected` setuju resolved.
- [ ] open status briše resolved.
- [ ] note/history ide u audit ako postoji.
- [ ] detail panel prikazuje metadata.

---

## Reports checklist

- [ ] Period.
- [ ] Refresh/freshness.
- [ ] Data quality.
- [ ] Methodology.
- [ ] Warnings.
- [ ] Print CSS.
- [ ] Export error fallback.
- [ ] Preview expired state.

---

## Commands

Frontend:
```powershell
cd Klijent/clientapp
npm run check:analytics-guardrails
npm run build
```

Backend:
```powershell
dotnet build
dotnet test
```

Migrations:
```powershell
dotnet ef migrations list `
  --project .\Infrastructure\Infrastructure.csproj `
  --startup-project .\Api\Api.csproj `
  --context AnalyticsDbContext
```

---

## Final report template

```text
Promenjeno:
- ...

Provere:
- dotnet build: pass/fail/not run
- dotnet test: pass/fail/not run
- npm run check:analytics-guardrails: pass/fail/not run
- npm run build: pass/fail/not run

Nisam uradio:
- ...

Rizici:
- ...

Sledeće:
- ...
```
