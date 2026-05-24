# Trendplus Prompt Templates

## 1. Small scoped fix

```text
Repo: ivanjovicic/Trendplus

Zadatak:
[kratko]

Scope:
Menjati samo:
- ...

Ne menjati:
- business recommendation logiku
- nevezane ekrane
- globalne stilove osim ako je task CSS

Pravila:
- no fake zero
- backend source of truth
- shared formatteri
- TrustHeader/ErrorState/EmptyState
- UTF-8 bez mojibake
- theme tokens

Provere:
- npm run check:analytics-guardrails
- npm run build
- dotnet build/test ako backend
```

---

## 2. Fake-zero backend prompt

```text
Repo: ivanjovicic/Trendplus

Zadatak:
Sprečiti fake-zero response u [endpoint].

Uraditi:
1. Empty dataset -> Meta.Empty.
2. SQL/missing MV/timeout -> Error/Problem.
3. Partial/fallback -> Warning.
4. Ne vraćati DTO sa nulama kada query nije uspeo.
5. Dodati test.

Acceptance:
- UI može razlikovati error/empty/insufficient.
- Backend greška ne izgleda kao 0.
```

---

## 3. Trust header rollout prompt

```text
Repo: ivanjovicic/Trendplus

Zadatak:
Dodati ili učvrstiti AnalyticsTrustHeader na [ekran].

Uraditi:
- title/description
- mode recommendation/signal/report
- period
- lastRefresh
- dataSource
- dataQualityStatus
- dataQualityHref
- refreshStatusHref

Ne izmišljati refresh.
Ako nema metadata, prikazati unknown/nije dostupno.
```

---

## 4. Scorecard prompt

```text
Repo: ivanjovicic/Trendplus

Zadatak:
Učvrstiti Supplier Scorecard period i gating.

Pravila:
- no silent fallback
- requested/effective dataset visible
- fallback -> recommendationAllowed=false
- UI kaže Pomoćni signal
- empty -> insufficient_data
- no fake zero
- report prenosi ograničenja
```

---

## 5. Encoding cleanup

```text
Repo: ivanjovicic/Trendplus

Zadatak:
Popraviti mojibake u analytics UI.

Pretrage:
Ä, Å, â, �, DobavljaÄ, marÅ, osveÅ, uÄ, Å¡, Å¾

Ne menjati business logiku.
Pokrenuti frontend build i guardrails.
```

---

## 6. Report hardening

```text
Repo: ivanjovicic/Trendplus

Zadatak:
Učvrstiti [report] za pilot/prodaju.

Report mora imati:
- period
- generated at
- last refresh
- data quality
- recommendationAllowed/fallback warnings
- methodology
- print CSS
- graceful export failure
```

---

## 7. Token-saving Codex prompt

```text
Repo: ivanjovicic/Trendplus

Radi sa malim scope-om.
Prvo pročitaj relevantne shared standarde:
- .github/copilot-instructions.md
- AGENTS.md
- docs/ai/CODEX_TASK_CHECKLIST.md

Ne učitavaj ceo repo.
Ne radi masovni rewrite.
Ako komanda zapne ili task ode široko, stani i napiši:
- šta je potvrđeno
- šta je nejasno
- najmanji sledeći korak
```

---

## 8. UI/theme prompt

```text
Repo: ivanjovicic/Trendplus

Zadatak:
Učvrstiti UI/theme za [ekran].

Pravila:
- bez hardcoded boja
- bez inline color style
- CSS variables
- responsive layout
- horizontal scroll blizu tabele
- status ne zavisi samo od boje
- loading/error/empty jasni
```

---

## 9. Worker/refresh prompt

```text
Repo: ivanjovicic/Trendplus

Zadatak:
Učvrstiti refresh/worker status za [oblast].

Pravila:
- web process ne radi heavy refresh
- worker process radi heavy jobs
- UI prikazuje fresh/stale/critical/unknown
- worker warning ako nije aktivan
- manual run ima feedback
- unknown se ne prikazuje kao fresh
```

---

## 10. Test prompt

```text
Repo: ivanjovicic/Trendplus

Zadatak:
Dodati regression testove za [problem].

Pokriti:
- happy path
- empty state
- error/fake-zero path
- fallback/partial ako postoji
- frontend guardrail ako je UI drift

Ne menjati business logiku osim ako test otkrije bug.
```
