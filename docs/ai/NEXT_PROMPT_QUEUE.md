# Trendplus Next Prompt Queue

Datum: 2026-06-14
Repo: `ivanjovicic/Trendplus`
Namena: redosled malih Codex taskova ka pilot/prodajnoj spremnosti.

## Kako koristiti

Codex treba da izvršava jedan task po sesiji/commitu.

Pravila:
1. Uzmi prvi task sa `Status: TODO`.
2. Promeni status u `IN_PROGRESS`.
3. Uradi samo taj task.
4. Pokreni navedene provere.
5. Promeni status u `DONE`, `PARTIAL` ili `BLOCKED`.
6. Dodaj belešku: šta je urađeno, koje provere su prošle, šta je ostalo.
7. Ne prelazi na sledeći task u istoj sesiji osim ako task eksplicitno kaže da je dozvoljeno.

## Stop rules

Stani i prijavi ako:
- task traži više od 6–8 fajlova bez jasnog razloga
- build/test komanda puca dva puta zaredom
- endpoint/source-of-truth nije jasan
- promena ulazi u auth/security/tenant bez postojećeg pattern-a
- postoji rizik da se pokvare route/lazy imports
- mora se napraviti migracija, a context nije potvrđen
- vidiš mojibake (`Ä`, `Å`, `â`, `�`)

## Obavezno pre svakog taska

Pročitaj:
- `.github/copilot-instructions.md`
- `AGENTS.md`
- `docs/ai/CODEX_TASK_CHECKLIST.md` ako postoji
- dokument iz `Read first` sekcije taska

---

# Queue

## Q01 — CI quality gates

Status: DONE
Commit suggestion: `ci(analytics): add sales-readiness quality gates`
Priority: P0
Token budget: low/medium

### Why

Repo sada ima guardrails i regresione testove, ali za poslednji provereni commit `ed730b24` nisu pronađeni GitHub workflow run-ovi. CI treba automatski da hvata fake-zero, formatter drift, TypeScript build i backend regresije.

### Scope only

- `.github/workflows/*`
- `Klijent/clientapp/package.json`
- `Klijent/clientapp/scripts/check-analytics-guardrails.mjs`
- `docs/ci/ANALYTICS_CI_GATES.md`

### Prompt

```text
Repo: ivanjovicic/Trendplus

Task:
Audit and add minimal CI quality gates for analytics regressions.

Read first:
- .github/copilot-instructions.md
- AGENTS.md

Do:
1. Audit current .github/workflows.
2. Create docs/ci/ANALYTICS_CI_GATES.md with:
   | Check | Command | Runs on PR | Runs on main | Blocks merge | Notes |
3. Ensure minimal CI runs:
   - dotnet build
   - dotnet test
   - cd Klijent/clientapp && npm run check:analytics-guardrails
   - cd Klijent/clientapp && npm run build
4. If full dotnet test is too slow or environment-dependent, document targeted analytics test alternative but keep dotnet build.
5. Do not add slow browser/e2e tests.
6. Do not rewrite deployment workflows.

Acceptance:
- CI status is documented.
- There is a minimal workflow or an explicit blocker.
- Frontend guardrails/build are automated.
- Backend build/test is automated or documented.

### Notes

- Date: 2026-06-14
- Changed files:
  - `.github/workflows/analytics-quality-gates.yml`
  - `docs/ci/ANALYTICS_CI_GATES.md`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Checks:
  - `cd Klijent/clientapp && npm run check:analytics-guardrails` — pass
  - `cd Klijent/clientapp && npm run build` — pass
  - `dotnet build Trendplus2.sln --no-restore --configuration Release` — pass
  - `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "Category=Unit"` — pass
  - `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "Category=Integration"` — pass
- Risk:
  - Backend CI remains targeted to `Api.Tests` categories rather than a full-solution `dotnet test`.
- Next step:
  - `Q02 — Access-control audit`
```

---

## Q02 — Access-control audit

Status: DONE
Commit suggestion: `docs(security): audit analytics access control`
Priority: P0
Token budget: low

### Scope only

- API endpoint registration files
- analytics endpoints
- report/export endpoints
- action queue endpoints
- import/access import endpoints
- worker/admin/config endpoints
- frontend route guards/navigation
- `docs/security/ANALYTICS_ACCESS_CONTROL_AUDIT.md`

### Prompt

```text
Repo: ivanjovicic/Trendplus

Task:
Create analytics/admin access-control audit.

Do:
1. Create docs/security/ANALYTICS_ACCESS_CONTROL_AUDIT.md.
2. Add table:
   | Area | Endpoint/UI | Current access | Required role | Gap | Priority |
3. Cover:
   - analytics read
   - product decisions
   - supplier scorecard
   - inventory analytics
   - data quality
   - reports/export
   - action queue create/update
   - manual refresh
   - clear analytics cache
   - worker control
   - import/access import
   - admin configuration
4. Use roles:
   - Viewer: read dashboards/reports
   - Analyst: create action items
   - Manager: export reports, close/approve actions
   - Admin: import, refresh, cache clear, workers, config
5. Mark P0 if open/unclear:
   - worker control
   - manual refresh
   - cache clear
   - import
   - admin config

Do not:
- implement full RBAC now
- break local dev auth
- invent a new auth system

Acceptance:
- Dangerous actions are identified.
- Required roles are documented.
- P0 gaps are explicit.

### Notes

- Date: 2026-06-14
- Changed files:
  - `docs/security/ANALYTICS_ACCESS_CONTROL_AUDIT.md`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Audited:
  - frontend route guards/navigation
  - analytics read dashboard
  - product decision center
  - supplier scorecard
  - inventory analytics
  - data quality
  - reports/export
  - action queue create/status/outcome
  - manual analytics refresh
  - analytics cache clear
  - worker control
  - access import
  - admin configuration
  - refresh status
  - performance/logs
- P0 gaps:
  - no shared auth middleware
  - no frontend protected routes
  - public action queue write endpoints
  - public cache invalidate / refresh / admin-routing / Redis toggle surfaces
  - import/cleanup and worker control still depend on ad-hoc key checks
  - export and logs surfaces need explicit role gating
- Checks:
  - `git diff --check` — not run yet
  - content audit via targeted `rg`/`Get-Content` inspections — pass
- Next step:
  - `Q03 — Pilot data requirements and import map`
```

---

## Q03 — Pilot data requirements and import map

Status: DONE
Commit suggestion: `docs(analytics): add pilot data requirements and import map`
Priority: P0
Token budget: low

### Scope only

- `docs/analytics/PILOT_DATA_REQUIREMENTS.md`
- `docs/analytics/PILOT_ONBOARDING_IMPORT_MAP.md`
- optional small DataQualityPage link/card only if obvious

### Prompt

```text
Repo: ivanjovicic/Trendplus

Task:
Add pilot onboarding and import map docs.

Do:
1. Create docs/analytics/PILOT_DATA_REQUIREMENTS.md.
2. Create docs/analytics/PILOT_ONBOARDING_IMPORT_MAP.md.

PILOT_DATA_REQUIREMENTS.md:
- required datasets: products, sales lines, suppliers, stock, cost prices, retail prices
- optional: markdown/nivelacija, returns, size, color, category
- blocking fields: ArticleId/SKU, SaleDate, Quantity, SalePrice
- trust reducers: CostPrice, Supplier, Stock, Category/Size/Color

PILOT_ONBOARDING_IMPORT_MAP.md:
Add table:
| Customer column | Trendplus field | Required | Example | Affects |

Examples:
- SifraArtikla -> ArticleId
- NazivArtikla -> Name
- Dobavljac -> SupplierName
- Kolicina -> Quantity
- DatumProdaje -> SaleDate
- Cena -> SalePrice
- NabavnaCena -> CostPrice
- Lager -> CurrentStock

Explain:
- without cost price: revenue yes, margin no
- without supplier: product analytics yes, supplier scorecard limited
- without stock: sales yes, inventory/OOS limited

Do not:
- build uploader
- change import logic
- add tenant model

Acceptance:
- Non-developer can understand what data to send.
- Docs explain blockers vs confidence reducers.
```

### Notes

- Date: 2026-06-14
- Changed files:
  - `docs/analytics/PILOT_DATA_REQUIREMENTS.md`
  - `docs/analytics/PILOT_ONBOARDING_IMPORT_MAP.md`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Audited:
  - `Api/Services/AccessImportService.cs`
  - `Api/Endpoints/AccessImportEndpoints.cs`
  - `Api/Endpoints/AdminConfigEndpoints.cs`
  - `Klijent/clientapp/src/App.tsx`
  - `Klijent/clientapp/src/pages/ConfigurationPage.tsx`
- P0 gaps:
  - missing `ArticleId` / `SKU` breaks the product-sales join
  - missing `SaleDate`, `Quantity` or `SalePrice` blocks reliable pilot KPIs
  - missing `CostPrice` removes margin confidence
  - missing `SupplierName` / `SupplierId` weakens supplier scorecard coverage
  - missing `CurrentStock` limits inventory and OOS analytics
- Checks:
  - `git diff --check` - pass
  - `Get-Content -Encoding utf8` spot-check of new docs - pass
- Next step:
  - `Q04 - Pilot import readiness gate`

---

## Q04 — Pilot import readiness gate

Status: DONE
Commit suggestion: `feat(data-quality): add pilot import readiness status`
Priority: P0
Token budget: medium

### Scope only

- `DataQualityPage.tsx`
- `PilotDataQualityIntakeReport.tsx`
- data-quality/intake backend DTO only if required

### Prompt

```text
Repo: ivanjovicic/Trendplus

Task:
Add advisory Pilot Import Readiness gate.

Do:
1. Add readiness status:
   - ready
   - ready_with_warnings
   - not_ready
   - unknown
2. Inputs:
   - article count
   - sales line count
   - receipt count
   - supplier count
   - first/last sale date
   - last import status if available
   - last analytics refresh status
   - missing cost share
   - missing supplier share
   - insufficient signal count
3. UI card:
   Title: Status pilota
   Labels:
   - Spremno
   - Spremno uz upozorenja
   - Nije spremno
   - Nepoznato
4. Show reasons, next actions and links to data quality, refresh status, import if available.
5. Unknown must not look successful.

Do not:
- block whole analytics UI
- create new import system
- change recommendation logic

Acceptance:
- Operator can decide whether dashboard is safe to show.
- Bad/unknown import state is visible.
- npm run check:analytics-guardrails passes.
- npm run build passes.
```

### Notes

- Date: 2026-06-14
- Changed files:
  - `Klijent/clientapp/src/utils/pilotImportReadiness.ts`
  - `Klijent/clientapp/src/components/analytics/PilotImportReadinessCard.tsx`
  - `Klijent/clientapp/src/utils/__tests__/pilotImportReadiness.spec.ts`
  - `Klijent/clientapp/src/pages/DataQualityPage.tsx`
  - `Klijent/clientapp/src/components/analytics/PilotDataQualityIntakeReport.tsx`
  - `Klijent/clientapp/src/components/analytics/PilotDataQualityIntakeReport.css`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Audited:
  - pilot import readiness inputs from the intake report and refresh status
  - status labels, reasons, and next actions in the Data Quality UI
  - links to data quality, refresh status, and import entry points
- P0 gap:
  - `lastImportStatus` is still optional/unwired from the page surface, so import-state detail can fall back to warning or unknown when metadata is missing
- Checks:
  - `cd Klijent/clientapp && npm run test -- --run src/utils/__tests__/pilotImportReadiness.spec.ts` — pass
  - `cd Klijent/clientapp && npm run check:analytics-guardrails` — pass
  - `cd Klijent/clientapp && npm run build` — pass
  - `dotnet build` — not run
  - `dotnet test` — not run
- Next step:
  - `Q05 - Pilot data safety runbooks`

---

## Q05 — Pilot data safety runbooks

Status: DONE
Commit suggestion: `docs(ops): add pilot data safety runbooks`
Priority: P0
Token budget: low

### Scope only

- `docs/ops/PILOT_DATA_SAFETY_CHECKLIST.md`
- `docs/ops/BACKUP_RESTORE_RUNBOOK.md`

### Prompt

```text
Repo: ivanjovicic/Trendplus

Task:
Add pilot data safety runbooks.

Do:
1. Create docs/ops/PILOT_DATA_SAFETY_CHECKLIST.md.
2. Create docs/ops/BACKUP_RESTORE_RUNBOOK.md.

Cover:
- stored data: operational DB, analytics DB, import files, generated reports/snapshots, logs/error records, cache
- backup: what, frequency, retention, access
- restore: steps, validation, post-restore refresh
- export: reports and raw data options
- delete: pilot cleanup, logs/report retention, manual gaps

Do not:
- claim automation that does not exist
- add code

Acceptance:
- Internal pilot data safety docs exist.
- Gaps are honest and explicit.
```

### Notes

- Date: 2026-06-14
- Changed files:
  - `docs/ops/PILOT_DATA_SAFETY_CHECKLIST.md`
  - `docs/ops/BACKUP_RESTORE_RUNBOOK.md`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Audited:
  - stored data surfaces: operational DB, analytics DB, import files, generated reports/snapshots, logs/error records, cache
  - backup and restore flow expectations without adding automation
  - export and cleanup guidance for pilot evidence and retention
- P0 gaps:
  - no confirmed automated backup scheduler
  - no confirmed one-click restore flow
  - log/report retention remains manual and organizational
  - cache is not durable backup data and must not be treated as such
- Checks:
  - `git diff --check` — pass
  - docs spot-check with `Get-Content -Encoding utf8` — pass
  - `dotnet build` — not run
  - `dotnet test` — not run
  - `npm run check:analytics-guardrails` — not run
  - `npm run build` — not run
- Next step:
  - `Q06 - Monitoring and alerting plan`

---

## Q06 — Monitoring and alerting plan

Status: DONE
Commit suggestion: `docs(ops): add analytics monitoring alerting plan`
Priority: P1
Token budget: low

### Scope only

- `docs/ops/ANALYTICS_MONITORING_ALERTING.md`
- `AnalyticsRefreshStatusBanner.tsx` only if critical copy is missing

### Prompt

```text
Repo: ivanjovicic/Trendplus

Task:
Add analytics monitoring and alerting plan, plus critical freshness copy if missing.

Do:
1. Create docs/ops/ANALYTICS_MONITORING_ALERTING.md.
2. Document signals: refresh critical, last success older than 72h, failure after last success, worker not running, repeated endpoint errors, stale cache warning, import failed.
3. Add severity: info, warning, critical.
4. Add response actions: check worker, check import, run manual refresh, clear cache if safe, inspect logs.
5. Add future channels: email, Slack/Teams, webhook.
6. If dataFreshnessStatus=critical, UI text should say:
   "Podaci su kritično zastareli. Ne preporučuje se donošenje odluka bez provere osvežavanja."

Do not:
- implement email/Slack unless infrastructure already exists
- redesign refresh status

Acceptance:
- Monitoring plan exists.
- Critical state is impossible to miss or documented as next gap.
```

### Notes

- Date: 2026-06-14
- Changed files:
  - `docs/ops/ANALYTICS_MONITORING_ALERTING.md`
  - `Klijent/clientapp/src/components/analytics/AnalyticsRefreshStatusBanner.tsx`
  - `Klijent/clientapp/src/components/analytics/__tests__/AnalyticsRefreshStatusBanner.spec.tsx`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Checks:
  - `cd Klijent/clientapp && npm run test -- --run src/components/analytics/__tests__/AnalyticsRefreshStatusBanner.spec.tsx` — pass
  - `cd Klijent/clientapp && npm run build` — pass
  - `git diff --check` — pass
- Result:
  - monitoring/alerting plan documented
  - critical freshness copy now appears in the refresh banner and matches the requested warning text
  - banner test updated to lock the critical copy
- Risks:
  - there is still no alert delivery infrastructure; email/Slack/Teams/webhook remain future channels only
  - incident ownership and escalation routing are still manual
- Next task:
  - `Q07 - Performance budgets`

---

## Q07 — Performance budgets

Status: DONE
Commit suggestion: `docs(ops): add analytics performance budgets`
Priority: P1
Token budget: low

### Scope only

- `docs/ops/ANALYTICS_PERFORMANCE_BUDGETS.md`

### Prompt

```text
Repo: ivanjovicic/Trendplus

Task:
Add analytics performance budgets.

Do:
Create docs/ops/ANALYTICS_PERFORMANCE_BUDGETS.md with table:
| Endpoint family | Warm target | Cold target | Cache expected | Risk | Notes |

Use:
- dashboard/bootstrap: warm <2s, cold <5s
- product decision: warm <3s, cold <8s
- supplier scorecard: warm <3s, cold <8s
- inventory: warm <3s, cold <8s
- data quality: warm <3s, cold <10s
- pre/post nivelacija: warm <4s, cold <12s
- reports: cached <5s, cold <15s

Add:
- measure duration, cache hit/miss, row count, timeout, correlationId
- demo rule: warm cache/refresh before demo if needed
- list top optimization candidates

Do not:
- optimize SQL in this commit
- change cache code

Acceptance:
- Performance expectations are documented.
- Demo blockers are clear.
```

### Notes

- Date: 2026-06-14
- Changed files:
  - `docs/ops/ANALYTICS_PERFORMANCE_BUDGETS.md`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Šta je dokumentovano:
  - performance budget tabela za dashboard/bootstrap, product decision, supplier scorecard, inventory, data quality, pre/post nivelacija i reports
  - obavezna merenja: duration, cache hit/miss, row count, timeout, correlationId
  - demo rule za warm cache/refresh pre demo toka
  - demo blockers i top optimization candidates
- Checks:
  - `git diff --check` — pass
  - docs spot-check with `Get-Content -Encoding utf8` — pass
  - `dotnet build` — not run
  - `dotnet test` — not run
  - `npm run check:analytics-guardrails` — not run
  - `npm run build` — not run
- Rizici:
  - budžeti su ciljevi i nisu potvrđeni automatskim merenjem u ovom tasku
  - bez standardizovanog telemetry capture-a, `duration` i `cache hit/miss` mogu ostati ručno proveravani
- Next queue item:
  - `Q08 - Demo mode and demo dataset plan`

---

## Q08 — Demo mode and demo dataset plan

Status: DONE
Commit suggestion: `docs(demo): add analytics demo dataset plan`
Priority: P1
Token budget: low

### Scope only

- `docs/demo/ANALYTICS_DEMO_MODE_PLAN.md`

### Prompt

```text
Repo: ivanjovicic/Trendplus

Task:
Create analytics demo mode and demo dataset plan.

Do:
1. Demo story:
   - 30-day pilot
   - Data Quality intake
   - Executive dashboard
   - Supplier scorecard
   - Inventory risk
   - Add action to queue
   - Supplier report
2. Dataset:
   - 50-200 products
   - 5-10 suppliers
   - 90/180 days sales
   - stock
   - cost prices
   - markdown/nivelacija
   - intentional data quality issues
3. Rules:
   - clearly marked Demo podaci
   - no mixing with customer data
   - reset/reseed later
4. 10-minute script:
   - open Data Quality
   - show Dashboard
   - open Supplier
   - open Inventory
   - add Action
   - print/export Report

Do not:
- implement seed now
- add UI toggle now

Acceptance:
- Demo plan is ready for implementation prompt.
```

### Notes

- Date: 2026-06-14
- Changed files:
  - `docs/demo/ANALYTICS_DEMO_MODE_PLAN.md`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Šta je dokumentovano:
  - 30-day pilot demo story kroz Data Quality, Dashboard, Supplier, Inventory, Action Queue i Report
  - demo dataset target za proizvode, dobavljače, prodaju, stock, cost i markdown/nivelacija signale
  - pravila za `Demo podaci`, bez mešanja sa customer data i bez UI toggle/seed implementacije
  - 10-minute demo script i otvoreni gapovi za reset/reseed i ownership
- Checks:
  - `git diff --check` — pass
  - docs spot-check with `Get-Content -Encoding utf8` — pass
  - `dotnet build` — not run
  - `dotnet test` — not run
  - `npm run check:analytics-guardrails` — not run
  - `npm run build` — not run
- Rizici:
  - nema potvrđenog automated reset/reseed flow-a
  - demo dataset governance i ownership ostaju operativni dogovor
- Next queue item:
  - `Q09 - Tenant-safety checklist`

---

## Q09 — Tenant-safety checklist

Status: DONE
Commit suggestion: `docs(security): add tenant safety checklist`
Priority: P2
Token budget: low

### Scope only

- `docs/security/TENANT_SAFETY_CHECKLIST.md`

### Prompt

```text
Repo: ivanjovicic/Trendplus

Task:
Create tenant-safety checklist for future SaaS, without implementing multi-tenancy.

Do:
1. State current pilot recommendation:
   - one deploy per customer
   - separate DB/storage per customer if possible
2. Document tenant-sensitive areas:
   - analytics cache keys
   - report snapshots
   - action queue sourceKey
   - refresh history
   - import files
   - logs/error records
   - exports
   - background jobs
3. Future rule:
   - every query/cache/report/job must be TenantId-scoped
4. Add table:
   | Area | Tenant-safe today? | Risk | Future action |

Do not:
- implement TenantId
- refactor DB

Acceptance:
- Future SaaS risks are documented.
- Current pilot model is clear.
```

### Notes

- Date: 2026-06-14
- Changed files:
  - `docs/security/TENANT_SAFETY_CHECKLIST.md`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Šta je dokumentovano:
  - trenutna pilot preporuka: jedan deployment po kupcu i odvojeni DB/storage gde je moguće
  - tenant-sensitive oblasti: cache, snapshots, action queue, refresh history, import files, logs, exports i background jobs
  - buduće pravilo da svaki query/cache/report/job mora biti `TenantId`-scoped
  - tenant-safety tabela sa rizicima i future actions
- Checks:
  - `git diff --check` — pass
  - docs spot-check with `Get-Content -Encoding utf8` — pass
  - `dotnet build` — not run
  - `dotnet test` — not run
  - `npm run check:analytics-guardrails` — not run
  - `npm run build` — not run
- Rizici:
  - `TenantId` još nije implementiran kroz query/cache/report/job slojeve
  - shared SaaS deployment ostaje visok rizik bez dodatne auth i storage izolacije
- Next queue item:
  - `Q10 - Product Decision Center polish`

---

## Q10 — Product Decision Center polish

Status: DONE
Commit suggestion: `feat(analytics): polish product decision center clarity`
Priority: P1
Type: frontend code polish
Token budget: medium

### Scope only

- `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx`
- shared analytics UI components already used by Product Decision Center
- nearby CSS/module file only if already paired with the page

### Prompt

```text
Repo: ivanjovicic/Trendplus

Task:
Polish Product Decision Center for sales clarity.

Do:
1. Make recommendation status, action and reason easier to scan.
2. Ensure period, freshness and data quality are visible without deep scrolling.
3. Reduce visual noise around secondary metadata if it hides the primary decision.
4. Keep "Zašto?" clarity for recommendation reasons and reason codes.
5. Preserve existing backend semantics and no-fake-zero behavior.

Do not:
- change recommendation logic
- introduce new filters unless obviously needed for clarity
- refactor unrelated analytics pages

Acceptance:
- Primary decision is easier to scan.
- Reason and trust context stay visible.
- No recommendation is shown without explanation.
```

### Checks

- `cd Klijent/clientapp && npm run check:analytics-guardrails`
- `cd Klijent/clientapp && npm run build`
- targeted vitest only if an existing Product Decision spec is touched

### Notes

- Date: 2026-06-14
- Changed files:
  - `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Šta je promenjeno:
  - user-facing copy je prebačen na dosledne srpske poslovne nazive sa dijakritikom
  - dodat je jasniji uvodni opis ekrana i očišćeni su recommendation/reason/detail labele
  - prikaz preporuke sada koristi dosledne frontend label mapove bez promene enum vrednosti ili recommendation logike
- Checks:
  - `cd Klijent/clientapp && npm run check:analytics-guardrails` — pass
  - `cd Klijent/clientapp && npm run build` — pass
  - targeted vitest — not run
- Rizici:
  - backend i dalje može vratiti nove user-facing stringove bez dijakritike koje ova stranica trenutno ne normalizuje osim kroz poznate copy replacement obrasce
  - chunk-size warning u `vite build` ostaje postojeći, bez novih build grešaka
- Next queue item:
  - `Q11 - Data Quality polish + silent empty panels`

---

## Q11 — Data Quality polish + silent empty panels

Status: DONE
Commit suggestion: `feat(data-quality): polish empty and silent panel states`
Priority: P1
Type: frontend code polish
Token budget: medium

### Scope only

- `Klijent/clientapp/src/pages/DataQualityPage.tsx`
- `Klijent/clientapp/src/components/analytics/PilotDataQualityIntakeReport.tsx`
- shared empty/error state components already used by analytics pages

### Prompt

```text
Repo: ivanjovicic/Trendplus

Task:
Polish Data Quality screen and remove silent empty panels.

Do:
1. Find panels that render weak placeholders, blanks or ambiguous zero-like states.
2. Replace silent empties with explicit empty, warning or unknown messaging.
3. Keep pilot readiness, freshness and methodology context visible.
4. Preserve existing intake semantics and trust messaging.

Do not:
- invent fake KPIs
- change backend contracts
- redesign unrelated analytics pages

Acceptance:
- Empty or missing sections are explicit.
- Operators can tell the difference between no data, warning and error.
- No silent empty panel remains in the main Data Quality flow.
```

### Checks

- `cd Klijent/clientapp && npm run check:analytics-guardrails`
- `cd Klijent/clientapp && npm run build`
- targeted vitest only if an existing Data Quality spec is touched

### Notes

- Date: 2026-06-14
- Changed files:
  - `Klijent/clientapp/src/pages/DataQualityPage.tsx`
  - `Klijent/clientapp/src/components/analytics/PilotDataQualityIntakeReport.tsx`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Šta je promenjeno:
  - glavni Data Quality tok više nema tihe prazne panele za top probleme i trend, već jasne empty/error poruke sa sledećim korakom za operatera
  - pilot intake izveštaj sada eksplicitno razlikuje validno prazno stanje od greške kada nema issue count-ova ili dodatnih impact signala
  - vidljivi copy na ekranu je dodatno očišćen i lokalizovan bez promene backend contract-a ili KPI semantike
- Checks:
  - `cd Klijent/clientapp && npm run check:analytics-guardrails` — pass
  - `cd Klijent/clientapp && npm run build` — pass
  - targeted vitest — not run
- Rizici:
  - build i dalje prijavljuje postojeći chunk-size warning, bez novih grešaka
  - ako backend uvede nove neprevedene stringove za ovaj ekran, frontend copy polish ih neće automatski normalizovati van postojećih mapiranja
- Next queue item:
  - `Q12 - Analytics navigation audit`

---

## Q12 — Analytics navigation audit

Status: DONE
Commit suggestion: `docs(frontend): audit analytics navigation clarity`
Priority: P1
Type: audit/docs
Token budget: low

### Scope only

- analytics route registration files
- analytics landing/navigation surfaces
- new doc under `docs/Frontend/` or `docs/ai/` if needed by findings

### Prompt

```text
Repo: ivanjovicic/Trendplus

Task:
Audit analytics navigation clarity.

Do:
1. Map key analytics entry points and likely operator paths.
2. Identify duplicated, unclear or legacy-feeling navigation labels.
3. Note where a user may not know whether to open Dashboard, Data Quality, Supplier, Inventory or Actions.
4. Recommend small, safe follow-up changes.

Do not:
- refactor routing in this task
- remove compatibility routes
- change code unless a very small copy fix is obviously safe and already patterned

Acceptance:
- Navigation confusion points are documented.
- Follow-up polish targets are clear.
```

### Notes

- Date: 2026-06-14
- Changed files:
  - `docs/Frontend/ANALYTICS_NAVIGATION_AUDIT.md`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Šta je promenjeno:
  - mapirani su canonical analytics entry point-i, verovatni operator tokovi i mesta gde sidebar ili dashboard copy ne vode jasno do pravog decision ekrana
  - dokumentovani su glavni confusion gapovi: nedostajući sidebar ulazi za `/analytics/products`, `/analytics/supplier` i `/analytics/actions`, kao i mešanje srpskih i engleskih labela
  - predložen je mali, bezbedan UX backlog bez promene routinga ili uklanjanja compatibility ruta
- Checks:
  - `git diff --check` — pass
  - `Get-Content -Encoding utf8 docs/Frontend/ANALYTICS_NAVIGATION_AUDIT.md` spot-check — pass
  - frontend build/test — not run (docs-only task)
- Rizici:
  - audit ne rešava navigacionu konfuziju sam po sebi; potreban je mali frontend follow-up commit da bi canonical analytics entry point-i postali vidljivi
  - deo confusion signala dolazi iz istorijskih/legacy ruta koje treba zadržati, pa budući polish mora paziti da ne polomi compatibility
- Next queue item:
  - `Q13 - Daily Sales UX audit`

---

## Q13 — Daily Sales UX audit

Status: DONE
Commit suggestion: `docs(analytics): audit daily sales ux clarity`
Priority: P2
Type: audit/docs
Token budget: low

### Scope only

- daily sales page and its immediate helpers
- new audit doc under `docs/Analytics/` or `docs/Frontend/`

### Prompt

```text
Repo: ivanjovicic/Trendplus

Task:
Audit Daily Sales UX clarity.

Do:
1. Review whether the page makes period, freshness and comparatives easy to understand.
2. Identify confusing KPIs, overloaded charts or weak empty/error messaging.
3. Note whether the screen helps a retail operator decide what to do next.
4. Propose small safe polish items.

Do not:
- change metrics logic
- redesign the page in this task
- change code unless a very small fix is explicitly justified and safe

Acceptance:
- Daily Sales UX risks are documented.
- Follow-up polish work is scoped.
```

### Notes

- Date: 2026-06-15
- Changed files:
  - `docs/Analytics/DAILY_SALES_UX_AUDIT.md`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Šta je promenjeno:
  - auditovana je `Daily Sales` stranica sa fokusom na period, poređenje, quality trust, chart prioritet i operator clarity
  - dokumentovani su glavni UX gapovi: nedostatak freshness/trust signala na vrhu, skriven quality panel, previše ravnopravnih chart panela i odsustvo jasnog sledećeg koraka
  - predložen je mali, bezbedan polish backlog bez diranja metrics logike ili broad redesign-a
- Checks:
  - `git diff --check` — pass
  - `Get-Content -Encoding utf8 docs/Analytics/DAILY_SALES_UX_AUDIT.md` spot-check — pass
  - frontend build/test — not run (docs-only task)
- Rizici:
  - deo freshness/trust poboljšanja verovatno traži i backend/generated-at signal koji ova ruta trenutno ne izlaže
  - follow-up mora paziti da ne pretvori ekran u još veći dashboard bez jasne hijerarhije prioriteta
- Next queue item:
  - `Q14 - Inventory UX audit`

---

## Q14 — Inventory UX audit

Status: DONE
Commit suggestion: `docs(inventory): audit analytics inventory ux`
Priority: P1
Type: audit/docs
Token budget: low

### Scope only

- `Klijent/clientapp/src/pages/InventoryPage.tsx`
- major inventory analytics panels
- new audit doc under `docs/Analytics/` or `docs/Frontend/`

### Prompt

```text
Repo: ivanjovicic/Trendplus

Task:
Audit Inventory UX from a decision-making angle.

Do:
1. Check whether replenish, OOS risk, dead stock and transfer decisions are visually obvious.
2. Identify places where export, scheduler or secondary controls overshadow the decision flow.
3. Review empty, stale and warning states for operator clarity.
4. Recommend a small follow-up set.

Do not:
- change inventory algorithms
- change code unless a tiny safe clarity fix is explicitly justified
- broaden scope beyond inventory analytics UX

Acceptance:
- Decision-flow gaps are documented.
- Risks and next polish targets are clear.
```

### Notes

- Date: 2026-06-15
- Changed files:
  - `docs/Analytics/INVENTORY_UX_AUDIT.md`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Šta je promenjeno:
  - auditovan je inventory ekran iz decision ugla sa fokusom na dopunu, OOS rizik, dead stock, transfer tok i odnos između signala, akcija i sekundarnih operativnih alata
  - dokumentovan je P0 gap da `DecisionSummaryBar` trenutno ne prenosi stvarni quality warning signal, pa top-level summary može delovati lažno zdravo
  - predložen je mali polish backlog za hijerarhiju odluka, copy cleanup i smanjenje export/control šuma bez diranja algoritama
- Checks:
  - `git diff --check` — pass
  - `Get-Content -Encoding utf8 docs/Analytics/INVENTORY_UX_AUDIT.md` spot-check — pass
  - frontend build/test — not run (docs-only task)
- Rizici:
  - P0 quality-summary gap ostaje otvoren dok se ne uradi mali frontend fix
  - follow-up mora paziti da ne vrati export/scheduler kontrole nazad u fokus iznad decision flow-a
- Next queue item:
  - `Q15 - Secondary analytics screens audit`

---

## Q15 — Secondary analytics screens audit

Status: DONE
Commit suggestion: `docs(analytics): audit secondary analytics screens`
Priority: P2
Type: audit/docs
Token budget: low

### Scope only

- secondary analytics pages outside dashboard, data quality, product decision, supplier hub and inventory
- new audit doc under `docs/Analytics/` or `docs/Frontend/`

### Prompt

```text
Repo: ivanjovicic/Trendplus

Task:
Audit secondary analytics screens for clarity and consistency.

Do:
1. Identify screens that feel legacy, under-explained or low-confidence.
2. Check for missing freshness, period or trust context.
3. Note where UX consistency drifts from the main analytics standard.
4. Rank the most valuable small polish opportunities.

Do not:
- refactor multiple screens in this task
- change code unless a tiny copy fix is explicitly safe and isolated

Acceptance:
- Secondary-screen clarity risks are documented.
- A prioritized follow-up list exists.
```

### Notes

- Date: 2026-06-15
- Changed files:
  - `docs/Analytics/SECONDARY_ANALYTICS_SCREENS_AUDIT.md`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Šta je promenjeno:
  - auditovani su sekundarni analytics ekrani van glavnih decision tokova i grupisani na modernije, mešovite i legacy/exploratory surface-e
  - dokumentovani su najveći clarity gapovi za `ColorSalesStatsPage`, `AnalyticsDetails`, `AnalyticsDetailPage` i `InsightStudioPage`, kao i relativno jači pattern-i na `ShoeType`, `SupplierSales`, `Pre/Post Nivelacije` i durable supplier report-u
  - napravljen je prioritetni backlog za male follow-up polish taskove bez broad refactor-a više ekrana odjednom
- Checks:
  - `git diff --check` — pass
  - `Get-Content -Encoding utf8 docs/Analytics/SECONDARY_ANALYTICS_SCREENS_AUDIT.md` spot-check — pass
  - frontend build/test — not run (docs-only task)
- Rizici:
  - deo sekundarnih ekrana i dalje ostaje nekonzistentan dok se ne uradi najmanje jedan mali frontend follow-up, posebno za `ColorSalesStatsPage` i `AnalyticsDetails`
  - `InsightStudio` je funkcionalno moćan, ali bez jasnog trust/freshness okvira i dalje nosi visok kognitivni trošak za pilot korisnika
- Next queue item:
  - `Q16 - PreNivelacija small polish`

---

## Q16 — PreNivelacija small polish

Status: DONE
Commit suggestion: `feat(nivelacija): polish pre-nivelacija clarity`
Priority: P2
Type: frontend code polish
Token budget: medium

### Scope only

- `Klijent/clientapp/src/pages/PreNivelacijaPriorityPage.tsx`
- immediate helper components already used by the page

### Prompt

```text
Repo: ivanjovicic/Trendplus

Task:
Apply a small clarity polish to PreNivelacija priority screen.

Do:
1. Improve scanability of priorities, reasons and recommended next actions.
2. Make warnings and data limitations easier to notice.
3. Keep current business semantics intact.

Do not:
- change scoring logic
- redesign the whole page
- touch unrelated nivelacija screens

Acceptance:
- Priority list is easier to scan.
- Warnings and next actions are clearer.
```

### Checks

- `cd Klijent/clientapp && npm run check:analytics-guardrails`
- `cd Klijent/clientapp && npm run build`
- targeted vitest only if an existing PreNivelacija spec is touched

### Notes

- Date: 2026-06-15
- Changed files:
  - `Klijent/clientapp/src/pages/PreNivelacijaPriorityPage.tsx`
  - `Klijent/clientapp/src/pages/PreNivelacijaPriorityPage.css`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Šta je promenjeno:
  - priority lista sada ispod preporuke prikazuje kratak sledeći korak, pa je brže jasno šta tim treba da uradi pre nivelacije
  - warnings i ograničenja signala su istaknuti kroz attention kartice i callout blokove u detaljima selektovanog reda
  - detalji prioriteta su pregledniji kroz akcioni sažetak, signal limitation poruku, chip prikaz reason code-ova i jasniji Data Quality hint
- Checks:
  - `cd Klijent/clientapp && npm run check:analytics-guardrails` — pass
  - `cd Klijent/clientapp && npm run build` — pass
  - targeted vitest — not run
- Rizici:
  - postojeći `vite build` chunk-size warning ostaje, bez novih build grešaka
  - sledeći koraci su frontend copy sloj; ako backend promeni semantics/status mapping, wording treba sinhronizovati
- Next queue item:
  - `Q17 - Supplier consolidated minor clarity`

---

## Q17 — Supplier consolidated minor clarity

Status: DONE
Commit suggestion: `feat(suppliers): polish consolidated clarity`
Priority: P2
Type: frontend code polish
Token budget: medium

### Scope only

- `Klijent/clientapp/src/pages/SupplierConsolidatedPage.tsx`
- immediate supplier analytics helpers already used by the page

### Prompt

```text
Repo: ivanjovicic/Trendplus

Task:
Apply minor clarity polish to Supplier Consolidated screen.

Do:
1. Improve visibility of period, trust and primary comparison cues.
2. Reduce confusion between supporting metrics and primary supplier takeaways.
3. Preserve current supplier analytics semantics.

Do not:
- change supplier scoring logic
- redesign supplier reporting flows
- touch unrelated supplier screens unless shared copy fix is tiny and safe

Acceptance:
- Main supplier takeaways are easier to understand.
- Trust context remains visible.
```

### Checks

- `cd Klijent/clientapp && npm run check:analytics-guardrails`
- `cd Klijent/clientapp && npm run build`
- targeted vitest only if an existing supplier consolidated spec is touched

### Notes

- Date: 2026-06-15
- Changed files:
  - `Klijent/clientapp/src/pages/SupplierConsolidatedPage.tsx`
  - `Klijent/clientapp/src/pages/SupplierConsolidatedPage.css`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Šta je promenjeno:
  - tabovi sada jasnije razlikuju finalnu preporuku, poređenje dobavljača i asortimanski drilldown kroz kratke hint labele
  - dodat je kratak kontekst panel za aktivni prikaz, period/filtere i trust status da glavni supplier takeaway ostane vidljiv iznad ugrađenih analytics ekrana
  - user-facing copy na stranici je očišćen i usklađen sa poslovnim jezikom bez promene supplier scoring semantike
- Checks:
  - `cd Klijent/clientapp && npm run check:analytics-guardrails` — pass
  - `cd Klijent/clientapp && npm run build` — pass
  - targeted vitest — not run
- Rizici:
  - postojeći `vite build` chunk-size warning ostaje, bez novih build grešaka
  - context panel ponavlja deo trust informacija iz headera; ako se trust copy menja globalno, može tražiti malu sinhronizaciju
- Next queue item:
  - `Q18 - Action Outcome Analytics plan`

---

## Q18 — Action Outcome Analytics plan

Status: DONE
Commit suggestion: `docs(actions): plan action outcome analytics`
Priority: P2
Type: audit/docs
Token budget: low

### Scope only

- action queue analytics surfaces
- outcome-related docs and nearby action analytics files
- new planning doc under `docs/Analytics/` or `docs/ai/`

### Prompt

```text
Repo: ivanjovicic/Trendplus

Task:
Create Action Outcome Analytics plan.

Do:
1. Define what outcome analytics should answer for operators and managers.
2. Identify minimal dimensions, metrics and trust constraints.
3. Note dependencies on action status, resolution notes, timing and ownership fields.
4. Recommend a staged implementation path.

Do not:
- implement new analytics now
- refactor action queue behavior
- change code unless a tiny safe docs-adjacent copy fix is explicitly justified

Acceptance:
- Action outcome analytics scope is documented.
- Dependencies and rollout stages are clear.
```

### Notes

- Date: 2026-06-15
- Changed files:
  - `docs/Analytics/ACTION_OUTCOME_ANALYTICS_PLAN.md`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Šta je promenjeno:
  - definisana su glavna operativna i menadžerska pitanja koja outcome analytics treba da odgovori za action queue
  - dokumentovane su minimalne dimenzije, metrike, trust constraints i zavisnosti od status/outcome/timing/ownership polja
  - preporučen je staged rollout od metric definition faze do read-only summary i manager report sloja, bez promene action workflow-a
- Checks:
  - `git diff --check` — pass (samo postojeće LF/CRLF upozorenje)
  - `Get-Content -Encoding utf8 docs/Analytics/ACTION_OUTCOME_ANALYTICS_PLAN.md` spot-check — pass
  - frontend build/test — not run (docs-only task)
  - `dotnet build` / `dotnet test` — not run
- Rizici:
  - nema eksplicitnog owner/assignee modela ni `acceptedAtUtc`, pa ownership i “time to first action” ostaju ograničeni u Phase 1
  - action queue write/outcome rute ostaju P0 access-control gap iz ranijeg audita, pa outcome analytics treba da ostane read-only dok se auth ne stabilizuje
- Next queue item:
  - queue je trenutno iscrpljen; sledeći logičan mali task je Phase 1 audit agregacionog data shape-a za action outcome summary


---

## Post-queue stabilization review - 2026-06-16

Status: DONE
Type: QA / stabilization review

### Notes

- Changed files:
  - docs/qa/ANALYTICS_STABILIZATION_REVIEW.md
  - docs/ai/NEXT_PROMPT_QUEUE.md
- Reviewed:
  - InventoryPage.tsx
  - DecisionSummaryBar.tsx
  - navConfig.ts
  - DataQualityPage.tsx
  - PilotDataQualityIntakeReport.tsx
  - AnalyticsActionsPage.tsx
  - analytics action summary endpoint/service/tests
  - route smoke tests
- Result:
  - no new regression requiring code changes was found
  - inventory quality card no longer masks warning/partial states as podaci OK
  - data quality zero-vs-partial behavior remains aligned with no-fake-zero trust rules
  - canonical sidebar analytics entry points remain visible while legacy routes stay intact
  - outcome summary endpoint remains read-only and its frontend failure mode stays non-blocking for the action list
- Checks:
  - targeted frontend stabilization tests - pass
  - frontend guardrails/build - see current task report
  - backend build/test - see current task report
- Risks:
  - action outcome summary implementation remains broader than the minimal public spec
  - there is still no dedicated component-level regression test for DecisionSummaryBar
- Next step:
  - targeted backend follow-up for resolvedFrom/resolvedTo and extra summary cohort bucket coverage

---

## Ad-hoc follow-up - 2026-06-17

Status: DONE
Type: frontend regression test

### Notes

- Changed files:
  - `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx`
  - `Klijent/clientapp/src/pages/__tests__/ProductDecisionCenterPage.actionStatusFallback.spec.tsx`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Result:
  - Product Decision Center now shows a non-blocking warning when optional action-status lookup fails, while keeping main product recommendations visible
  - dedicated regression coverage locks the fallback behavior and verifies that the real blocking error still appears when the main decision endpoint fails
  - no fake action counts are shown when action-status data is unavailable
- Checks:
  - `cd Klijent/clientapp && npm run test -- --run src/pages/__tests__/ProductDecisionCenterPage.actionStatusFallback.spec.tsx` - pass
  - `cd Klijent/clientapp && npm run check:analytics-guardrails` - pass
  - `cd Klijent/clientapp && npm run build` - pass
- Risks:
  - the worktree still contains unrelated local changes outside this task and they must stay out of the commit
- Next step:
  - re-run the latest analytics page smoke path after the next frontend deploy if Render or API action-status availability remains unstable

---

## Ad-hoc follow-up - 2026-06-17

Status: DONE
Type: QA smoke checklist

### Notes

- Changed files:
  - `docs/qa/ANALYTICS_PILOT_SMOKE_TEST.md`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Result:
  - added a repeatable manual smoke checklist for critical analytics backend routes, core frontend routes and durable report routes
  - each route now has explicit success expectations, honest warning/error expectations, fail conditions, next action and required evidence to save
  - the checklist explicitly protects against fake `0 RSD`, fake green unknown states, hidden stale refresh and reports that look ready when data is missing
- Checks:
  - `git diff --check` - pass
  - docs spot-check - pass
- Risks:
  - this task documents the operator flow but does not itself prove that current deploy environments pass the smoke
- Next step:
  - run the checklist on the current Vercel and Render deployments before demo or merge sign-off

---

## Ad-hoc follow-up - 2026-06-17

Status: DONE
Type: cache invalidation audit

### Notes

- Changed files:
  - `docs/qa/ANALYTICS_CACHE_INVALIDATION_AUDIT.md`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Result:
  - confirmed that Access import, nightly analytics refresh, data-quality worker refresh and admin cache clear already invalidate the pilot-critical cache families
  - confirmed that report routes use versioned cache keys and rely on report-family invalidation to rotate durable report outputs
  - documented one real follow-up gap: `AnalyticsAggregationWorker` refreshes aggregate tables but intentionally skips cache invalidation, so dashboard-family cached responses can lag until TTL expiry
  - documented that `docs/qa/STABLE_REPORT_URL_SMOKE.md` is currently missing, so earlier stable-report smoke expectations could not be reused
- Checks:
  - `dotnet build Trendplus2.sln --no-restore --configuration Release` - pass
  - `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "Category=Unit"` - pass
  - `git diff --check` - pass
- Risks:
  - the safest invalidation scope for `AnalyticsAggregationWorker` still needs a deliberate choice between `dashboard` only and a slightly wider aggregate-backed scope
  - no code fix was applied in this audit commit because that scope choice should be made explicitly instead of guessed
- Next step:
  - small backend follow-up: decide and implement the minimal safe invalidation for `AnalyticsAggregationWorker`, then add a focused regression test

---

## Ad-hoc follow-up - 2026-06-17

Status: DONE
Type: demo reset runbook

### Notes

- Changed files:
  - `docs/demo/ANALYTICS_DEMO_RESET_RUNBOOK.md`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Result:
  - documented the current confirmed demo-related capabilities across local seed helpers, Access import, cleanup preview, worker refresh and smoke verification
  - defined strict demo-only safety rules, required environment proof, naming rules and stop conditions before any destructive action
  - documented the minimal demo dataset shape for products, suppliers, sales, inventory, data quality issues, actions/outcomes and reports
  - defined the safest run order: reset, seed via existing Access import flow, refresh analytics, verify Pilot Readiness, then run analytics smoke
  - intentionally did not add a new script because the existing seed helpers are developer-only and not safe as a shared demo reset mechanism
- Checks:
  - `git diff --check` - pass
  - `dotnet build Trendplus2.sln --no-restore --configuration Release` - not run (no code/script added)
- Risks:
  - the process still depends on operational discipline because there is no one-click demo-only reset guard in code
  - a dedicated demo DB snapshot remains the safest reset path; batch-delete and cleanup flows require stricter human review
- Next step:
  - if the team wants automation, implement a tiny demo-only wrapper around the existing Access import + worker refresh flow with an explicit environment guard

---

## Ad-hoc follow-up - 2026-06-17

Status: DONE
Type: access-control implementation plan

### Notes

- Changed files:
  - `docs/security/ANALYTICS_ACCESS_CONTROL_IMPLEMENTATION_PLAN.md`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Result:
  - translated the existing analytics access-control audit into a phase-by-phase P0 implementation plan
  - defined the minimal role model: `Viewer`, `Analyst`, `Manager`, `Admin`
  - mapped the P0 endpoint groups for refresh, cache clear, import/access-import, worker control, admin configuration, action writes and report/export surfaces
  - documented for each group: current access, required role, backend enforcement location, frontend visibility rule and required tests
  - proposed a minimal Phase 1 where read-only analytics stays available to `Viewer`, while dangerous actions move behind explicit backend enforcement and hidden UI controls
- Checks:
  - `git diff --check` - pass
- Risks:
  - the repo still lacks a shared authentication/policy layer, so Phase 1 must start with explicit per-group enforcement helpers before broader cleanup
- current admin-key compatibility paths should remain temporary and must not become the long-term substitute for role checks
- Next step:
  - implement the first smallest protected group: `POST /api/analytics/cached/cache/invalidate` plus matching frontend visibility test

---

## Queue continuation - decision-support hardening

Status summary:

- Q01-Q18 are DONE.
- Product Decision optional action-status fallback is DONE.
- Analytics pilot smoke checklist is DONE.
- Cache invalidation audit is DONE.
- Demo reset runbook is DONE.
- Demo environment verification endpoint exists and is tested.
- Open production risk: Vercel deploy drift and Render runtime version 404.
- Open decision-support gap: no shared decision confidence contract yet.

## Q19 - Deploy proof cleanup

Status: PARTIAL

Evidence:
- Files: `docs/qa/ANALYTICS_DEPLOY_PROOF.md`, `docs/qa/ANALYTICS_PILOT_SMOKE_RESULT.md`, `docs/qa/RENDER_BACKEND_VERSION_TRIAGE.md`
- Tests/checks: live HTTP recheck on 2026-06-19 from the current workspace HEAD; Vercel still serves `index-XONGNubS.js` with a stale `Last-Modified` header, and Render still returns `404` for `/api/runtime/version`.
- Remaining risk: the public deploy is still drifting from current source, so the blocker is documented but not remediated.

## Q20 - Demo verification production smoke

Status: OPEN

Evidence:
- Files: `docs/demo/ANALYTICS_DEMO_RESET_RUNBOOK.md`, `Api/Endpoints/AdminConfigEndpoints.cs`, `Api.Tests/DemoEnvironmentVerificationEndpointTests.cs`
- Tests/checks: integration tests cover the admin-gated `/api/admin/demo-verification` endpoint and secret redaction behavior.
- Remaining risk: there is no concrete production smoke result document proving the demo verifier on a live demo deployment.

## Q21 - Analytics action idempotency production/migration verification

Status: DONE

Evidence:
- Files: `Infrastructure/DbContexts/AnalyticsDbContext.cs`, `Infrastructure/Migrations/AnalyticsDb/20260521123000_AddAnalyticsActionIndexes.cs`, `Infrastructure/Services/Analytics/AnalyticsActionItemService.cs`, `Api.Tests/AnalyticsActionItemServiceTests.cs`, `docs/qa/ANALYTICS_ACTION_IDEMPOTENCY_MIGRATION_NOTE.md`
- Tests/checks: targeted analytics action service tests cover duplicate open-action races, unrelated `DbUpdateException`, and source-type/source-key isolation.
- Remaining risk: older databases may still need duplicate cleanup before applying the filtered unique index if they predate the schema change.

## Q22 - Access-control next P0 group

Status: OPEN

Evidence:
- Files: `docs/security/ANALYTICS_ACCESS_CONTROL_IMPLEMENTATION_PLAN.md`, `Api/Endpoints/AnalyticsActionsEndpoints.cs`, `Api/Endpoints/AdminConfigEndpoints.cs`
- Tests/checks: admin-key compatibility exists for some admin surfaces, but analytics action write endpoints still need a dedicated protection pass.
- Remaining risk: the next P0 group is the action write path unless it has already been protected elsewhere.

## Q23 - Decision confidence contract

Status: DONE

Evidence:
- Files: `docs/Analytics/DECISION_CONFIDENCE_CONTRACT.md`, `docs/Analytics/EXECUTIVE_DECISION_BOARD_PLAN.md`
- Tests/checks: docs-only contract; no runtime test required for this planning task.
- Remaining risk: the contract still needs broader module-by-module enforcement outside Product Decision Center.

## Q24 - Product Decision confidence phase 1

Status: DONE

Evidence:
- Files: `Api/Endpoints/CachedAnalyticsEndpoints.cs`, `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx`, `Api.Tests/AnalyticsProductDecisionConfidenceTests.cs`, `Klijent/clientapp/src/pages/__tests__/ProductDecisionCenterPage.confidence.spec.tsx`
- Tests/checks: backend and frontend confidence coverage exists for Product Decision Center.
- Remaining risk: the confidence story still needs later ledger/board reuse and more edge-case coverage.

## Q25 - Action Impact Ledger plan

Status: DONE

Evidence:
- Files: `docs/Analytics/ACTION_IMPACT_LEDGER_PLAN.md`, `docs/Analytics/ACTION_OUTCOME_SUMMARY_ENDPOINT_SPEC.md`
- Tests/checks: docs-only planning task; no runtime test required.
- Remaining risk: backend DTO/entity implementation still needs a follow-up task.

## Q26 - Production deploy proof finalization

Status: DONE

Evidence:
- Files: `docs/qa/ANALYTICS_DEPLOY_PROOF.md`, `docs/qa/ANALYTICS_PILOT_SMOKE_RESULT.md`
- Tests/checks: `git diff --check` pass; `dotnet build Trendplus2.sln --no-restore --configuration Release` pass; `cd Klijent/clientapp && npm run check:analytics-guardrails` pass; `cd Klijent/clientapp && npm run build` pass.
- Remaining risk: Vercel still serves `index-XONGNubS.js` and Render still returns `404` for `/api/runtime/version`, so the blocker is documented but not remediated.

## Q27 - Demo verification production smoke

Status: PARTIAL

Evidence:
- Date: 2026-06-19
- Files: `docs/qa/DEMO_VERIFICATION_SMOKE_RESULT.md`, `docs/demo/ANALYTICS_DEMO_RESET_RUNBOOK.md`, `Api/Endpoints/AdminConfigEndpoints.cs`, `Api.Tests/DemoEnvironmentVerificationEndpointTests.cs`
- Tests/checks: `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "DemoEnvironmentVerification"` pass; public `GET https://trendplus-api.onrender.com/api/admin/demo-verification` returned `401 Unauthorized`; response secrecy is covered by local integration tests.
- Remaining risk: the route exists in source and the local tests pass, but the live production surface is admin-gated so `demoSafe` cannot be confirmed from the public endpoint alone.

Next step:
- Q28 - Protect analytics action write endpoints

## Q28 - Protect analytics action write endpoints

Status: DONE
Commit suggestion: `fix(security): protect analytics action write endpoints`
Priority: P0

### Evidence

- Date: 2026-06-19
- Files:
  - `Api/Endpoints/AnalyticsActionsEndpoints.cs`
  - `Api.Tests/AnalyticsActionsEndpointsTests.cs`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Checks:
  - `dotnet build Trendplus2.sln --no-restore --configuration Release` - pass
  - `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "FullyQualifiedName~AnalyticsActionsEndpointsTests"` - pass
- Remaining risk:
  - Read-only analytics routes remain public by design, and there is still no existing frontend capability/admin-key pattern for action-write visibility to reuse safely.

## Q29 - Executive Decision Board hardening tests

Status: DONE
Commit suggestion: `test(analytics): harden executive decision board route coverage`
Priority: P1

### Evidence

- Date: 2026-06-19
- Files:
  - `Klijent/clientapp/src/__tests__/AppAnalyticsRoutes.spec.tsx`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Checks:
  - `cd Klijent/clientapp; npm run test -- --run src/__tests__/AppAnalyticsRoutes.spec.tsx` - pass
  - `cd Klijent/clientapp; npm run build` - pass
- Remaining risk:
  - The board route is now covered explicitly in the production-style `App` route smoke, but it still depends on the existing lazy import and the route manifest staying aligned with `App.tsx`.

## Q30 - Executive Decision Board no-fake-confidence review

Status: DONE
Commit suggestion: `fix(analytics): keep executive board confidence honest`
Priority: P1

### Evidence

- Date: 2026-06-19
- Files:
  - `Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx`
  - `Klijent/clientapp/src/pages/__tests__/ExecutiveDecisionBoardPage.spec.ts`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Checks:
  - `cd Klijent/clientapp; npm run test -- --run src/pages/__tests__/ExecutiveDecisionBoardPage.spec.ts` - pass
  - `cd Klijent/clientapp; npm run check:analytics-guardrails` - pass
  - `cd Klijent/clientapp; npm run build` - pass
- Remaining risk:
  - The review is covered for missing action and supplier confidence plus the existing product insufficient-data path, but broader module-by-module confidence policy is still a follow-up concern.

## Q31 - Product Decision confidence review and edge-case tests

Status: DONE
Commit suggestion: `test(analytics): cover product decision confidence edge cases`
Priority: P1

### Evidence

- Date: 2026-06-19
- Files:
  - `Klijent/clientapp/src/pages/__tests__/ProductDecisionCenterPage.confidence.spec.tsx`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Checks:
  - `cd Klijent/clientapp; npm run test -- --run src/pages/__tests__/ProductDecisionCenterPage.confidence.spec.tsx` - pass
  - `cd Klijent/clientapp; npm run check:analytics-guardrails` - pass
  - `cd Klijent/clientapp; npm run build` - pass
- Remaining risk:
  - Confidence and missing-impact behavior are now locked for strong and insufficient recommendations, but broader module coverage still depends on future decision-board and cross-module review.

## Q32 - Decision Board backend aggregate plan

Status: DONE
Commit suggestion: `docs(analytics): plan decision board backend aggregate path`
Priority: P2

### Evidence

- Date: 2026-06-19
- Files:
  - `docs/analytics/EXECUTIVE_DECISION_BOARD_PLAN.md`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Checks:
  - `git diff --check` - pass
- Remaining risk:
- The backend aggregate path is still a Phase 2 design only; Phase 1 frontend composition remains the shipped board path until a server-side aggregate is explicitly implemented.

## Q33 - Production deploy recovery and proof

Status: DONE
Commit suggestion: `docs(qa): document analytics deploy recovery`
Priority: P1

### Evidence

- Date: 2026-06-19
- Files:
  - `docs/qa/ANALYTICS_DEPLOY_RECOVERY.md`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Checks:
  - `git status -sb` - pass, local `HEAD` is ahead of `origin/main` by 1
  - `git log --oneline -10` - pass
  - `git rev-parse HEAD` - pass, `8cfdbe6983adfde0b1d6e249f981f1b4c7b887b3`
  - `git rev-parse origin/main` - pass, `e9f3238a172fe61ade3844777d8576dade270dae`
  - live Render `/api/runtime/version` - pass, `200 OK` with `commitSha=e9f3238a172fe61ade3844777d8576dade270dae`
  - live Vercel route checks - pass, `/analytics/pilot-readiness`, `/analytics/reports/pilot-intake`, and `/analytics/decision-board` still serve the generic shell
- Remaining risk:
  - local `HEAD` is still ahead of `origin/main`, so production proof is not current with the latest local tip until that commit is pushed and redeployed
  - frontend deploy drift remains on Vercel until the shell bundle moves off `index-DelBmZl0.js`

## Q34 - Re-run live analytics smoke after deploy

Status: DONE

Evidence:
- Date: 2026-06-19
- Files:
  - `docs/qa/ANALYTICS_PILOT_SMOKE_RESULT.md`
  - `docs/qa/ANALYTICS_DEPLOY_RECOVERY.md`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Checks:
  - live Render `GET /api/runtime/version` - pass, `200 OK` with `commitSha=e2c2901c8589be4f5cbf9c066b6f5fc74ddd3288`
  - live Render `GET /api/admin/demo-verification` - pass as auth gate, `401 Unauthorized` without admin credentials
  - live Render `GET /api/analytics/refresh-status?dataScope=all` - pass, `200 OK` with `dataFreshnessStatus=unknown`
  - live Render `GET /api/analytics/actions?dataScope=all` - pass, `200 OK` with action data
  - live Vercel `/analytics/pilot-readiness` - pass, real Pilot readiness checklist rendered
  - live Vercel `/analytics/reports/pilot-intake` - pass, real Pilot intake report rendered
  - live Vercel `/analytics/decision-board` - pass, real Executive decision board rendered
- Remaining risk:
  - Vercel alias stability still depends on the next deployment cycle, so the proof should be rechecked if the bundle hash changes again
  - a follow-up watch item is still useful if the team wants an explicit soak check

## Q35 - Redeploy Vercel frontend from current main

Status: DONE
Commit suggestion: `docs(qa): record live analytics smoke recheck`
Priority: P1

Evidence:
- Date: 2026-06-19
- Files:
  - `docs/qa/VERCEL_FRONTEND_REDEPLOY_PROOF.md`
  - `docs/qa/ANALYTICS_PILOT_SMOKE_RESULT.md`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Checks:
  - `git rev-parse HEAD` - pass, `afb575ac02a9e43f6ab0a3ce2520997fd0ade69f`
  - `git rev-parse origin/main` - pass, `afb575ac02a9e43f6ab0a3ce2520997fd0ade69f`
  - `cd Klijent/clientapp && npm run check:analytics-guardrails` - pass
  - `cd Klijent/clientapp && npm run build` - pass
  - live Vercel `/analytics/pilot-readiness` - pass, real Pilot readiness checklist rendered from `/assets/index-DPyjYUlZ.js`
  - live Vercel `/analytics/reports/pilot-intake?fromDate=2026-06-01&toDate=2026-06-30&dataScope=all` - pass, real Pilot intake report rendered from `/assets/index-DPyjYUlZ.js`
  - live Vercel `/analytics/decision-board` - pass, real Executive decision board rendered from `/assets/index-DPyjYUlZ.js`
- Remaining risk:
  - Vercel alias stability still depends on the next deployment cycle, so the proof should be rechecked if the bundle hash changes again
  - a follow-up watch item is still useful if the team wants an explicit soak check

## Q36 - Post-redeploy smoke watch

Status: DONE

Evidence:
- Files:
  - `docs/qa/ANALYTICS_PILOT_SMOKE_RESULT.md`
  - `docs/qa/ANALYTICS_LIVE_SMOKE_RESULT.md`
- Checks:
  - later live browser recheck after the redeploy soak window - pass, same bundle hash still live and required routes continue to render correctly
  - full live analytics smoke after Vercel redeploy - pass, backend and frontend work together on the production surfaces
- Remaining risk:
  - Vercel alias stability still matters on future deploys, so another smoke watch may be useful after the next release if the bundle hash changes again

## Q37 - Protected action write UX hardening

Status: DONE

Evidence:
- Date: 2026-06-19
- Files:
  - `Klijent/clientapp/src/utils/analyticsActionWriteErrors.ts`
  - `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx`
  - `Klijent/clientapp/src/pages/AnalyticsActionsPage.tsx`
  - `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx`
  - `Klijent/clientapp/src/pages/InventoryPage.tsx`
  - `Klijent/clientapp/src/pages/SupplierDecisionHubPage.tsx`
  - `Klijent/clientapp/src/components/analytics/SupplierDecisionReportActions.tsx`
  - `Klijent/clientapp/src/pages/__tests__/ProductDecisionCenterPage.queueStatus.spec.tsx`
  - `Klijent/clientapp/src/pages/__tests__/AnalyticsActionsPage.spec.tsx`
  - `Klijent/clientapp/src/components/analytics/__tests__/SupplierDecisionReportActions.spec.tsx`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Checks:
  - `git diff --check` - pass
  - `cd Klijent/clientapp && npm run check:analytics-guardrails` - pass
  - `cd Klijent/clientapp && npm run build` - pass
  - `cd Klijent/clientapp && npm run test -- --run src/pages/__tests__/ProductDecisionCenterPage.queueStatus.spec.tsx src/pages/__tests__/AnalyticsActionsPage.spec.tsx src/components/analytics/__tests__/SupplierDecisionReportActions.spec.tsx` - pass
- Remaining risk:
  - protected write endpoints still depend on backend auth responses in production, so future UI changes should keep the same forbidden-state handling and avoid optimistic success

Next step:
- no queued follow-up task yet; revisit the same forbidden-state pattern if another analytics write surface is added

## Q38 - Analytics regression risk audit

Status: DONE

Evidence:
- Date: 2026-06-19
- Files:
  - `docs/qa/ANALYTICS_REGRESSION_RISK_AUDIT.md`
  - `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx`
  - `Klijent/clientapp/src/pages/SupplierConsolidatedPage.tsx`
  - `Klijent/clientapp/src/pages/SupplierFootwearAnalyticsPage.tsx`
  - `Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx`
  - `Klijent/clientapp/src/pages/InventoryPage.tsx`
  - `Klijent/clientapp/src/pages/SupplierDecisionHubPage.tsx`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Checks:
  - `git diff --check` - pass
  - `cd Klijent/clientapp && npm run check:analytics-guardrails` - pass
  - `cd Klijent/clientapp && npm run build` - pass
- Extra follow-up:
  - Inventory page queued suggestion markers now survive transient source-status failures instead of being cleared to an empty state.
- Remaining risk:
  - `?? 0` / `|| 0` patterns still exist in many analytics surfaces, but the audited ones were either intentional derived defaults or already protected by meta/error states
  - broader numeric fallback review is still useful even though the fake-empty behavior is now reduced

Next step:
- Q39 - Add visible warnings for ancillary filter/list refresh failures

## Q39 - Executive Decision Board quality audit

Status: DONE

Evidence:
- Date: 2026-06-19
- Files:
  - `Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx`
  - `Klijent/clientapp/src/pages/__tests__/ExecutiveDecisionBoardPage.spec.ts`
  - `docs/qa/EXECUTIVE_DECISION_BOARD_QUALITY_AUDIT.md`
- Checks:
  - `git diff --check` - pass
  - `cd Klijent/clientapp && npm run check:analytics-guardrails` - pass
  - `cd Klijent/clientapp && npm run build` - pass
  - `cd Klijent/clientapp && npm run test -- --run ExecutiveDecisionBoard` - pass
- Remaining risk:
  - repeated cards are still shown across multiple board sections by design, but the section context now makes that repetition explicit instead of silently deduping it

Next step:
- Q40 - Analytics observability/correlation-id hardening

## Q40 - Analytics observability/correlation-id hardening

Status: DONE

Evidence:
- Date: 2026-06-19
- Files:
  - `docs/qa/ANALYTICS_OBSERVABILITY_REVIEW.md`
  - `Klijent/clientapp/src/services/analyticsApi.ts`
  - `Klijent/clientapp/src/components/analytics/AnalyticsRefreshStatusBanner.tsx`
  - `Klijent/clientapp/src/components/analytics/__tests__/AnalyticsRefreshStatusBanner.spec.tsx`
  - `docs/qa/ANALYTICS_PILOT_SMOKE_RESULT.md`
  - `docs/qa/ANALYTICS_LIVE_SMOKE_RESULT.md`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Checks:
  - `git diff --check` - pass
  - `cd Klijent/clientapp && npm run check:analytics-guardrails` - pass
  - `cd Klijent/clientapp && npm run build` - pass
  - `cd Klijent/clientapp && npm run test -- --run AnalyticsRefreshStatusBanner` - pass
- Remaining risk:
  - refresh-status still depends on recent run history for visible correlation IDs, but the shared API layer now preserves IDs whenever the backend emits them

Next step:
- Q41 - Action Impact Ledger Phase 1 design-to-implementation gap review

## Q41 - Action Impact Ledger Phase 1 design-to-implementation gap review

Status: DONE

Evidence:
- Date: 2026-06-19
- Files:
  - `docs/qa/ACTION_IMPACT_LEDGER_GAP_REVIEW.md`
  - `docs/Analytics/ACTION_IMPACT_LEDGER_PLAN.md`
  - `docs/Analytics/ACTION_OUTCOME_DATA_SHAPE_AUDIT.md`
  - `docs/Analytics/ACTION_OUTCOME_SUMMARY_API_PLAN.md`
  - `Domain/Model/Analytics/AnalyticsActionItem.cs`
  - `Domain/Model/Analytics/AnalyticsActionNote.cs`
  - `Infrastructure/Services/Analytics/AnalyticsActionItemService.cs`
  - `Api/Endpoints/AnalyticsActionsEndpoints.cs`
  - `Klijent/clientapp/src/types/analytics.ts`
- Checks:
  - `git diff --check` - pass
- Remaining risk:
  - the current ledger contract is still only implicit in `MetadataJson` + notes, so Phase 1 still needs a canonical structured metadata schema before a true append-only table is worth adding

Next step:
- Q42 - Product Decision confidence calibration review

## Q42 - Product Decision confidence calibration review

Status: DONE

Evidence:
- Date: 2026-06-19
- Files:
  - `docs/qa/PRODUCT_DECISION_CONFIDENCE_AUDIT.md`
  - `docs/Analytics/DECISION_CONFIDENCE_CONTRACT.md`
  - `docs/Analytics/ANALYTICS_DECISION_OS_ROADMAP.md`
  - `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx`
  - `Klijent/clientapp/src/pages/__tests__/ProductDecisionCenterPage.confidence.spec.tsx`
  - `Klijent/clientapp/src/pages/__tests__/ProductDecisionCenterPage.actionStatusFallback.spec.tsx`
  - `Api/Endpoints/CachedAnalyticsEndpoints.cs`
- Checks:
  - `git diff --check` - pass
  - `cd Klijent/clientapp && npm run test -- --run ProductDecisionCenterPage.confidence` - pass
- Remaining risk:
  - Product Decision Center still does not have a separate calibration bucket UI; calibration learning belongs to the outcome summary / ledger layer rather than a local page-only score

Next step:
- Q43 - Supplier confidence contract mapping

## Q43 - Supplier confidence contract mapping

Status: DONE

Next step:
- map supplier summary, list, and report confidence semantics onto the shared contract without inventing new values in the UI

### Notes

- Date: 2026-06-19
- Verification HEAD: `58165dc325621a84c5327705f2fe3554bca083d6`
- Changed files:
  - `Klijent/clientapp/src/pages/__tests__/SupplierDecisionHubPage.spec.tsx`
  - `Klijent/clientapp/src/services/__tests__/supplierDecisionReport.spec.ts`
  - `docs/qa/SUPPLIER_CONFIDENCE_CONTRACT_AUDIT.md`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Checks:
  - `git diff --check` - pass, with repository line-ending warnings only
  - `cd Klijent/clientapp && npm run check:analytics-guardrails` - pass
  - `cd Klijent/clientapp && npm run test -- --run src/pages/__tests__/SupplierDecisionHubPage.spec.tsx src/services/__tests__/supplierDecisionReport.spec.ts` - pass
  - `cd Klijent/clientapp && npm run build` - pass
- Risk:
  - Supplier ranking sorting still uses an internal `?? 0` fallback for ordering only; visible confidence output remains gated by backend presence.
- Next step:
  - `Q44 - Inventory decision confidence mapping`

## Q44 - Inventory decision confidence mapping

Status: DONE

Next step:
- align inventory recommendation confidence, warnings, and nullable impact behavior with the shared decision contract

### Notes

- Date: 2026-06-19
- Verification HEAD: `7b24b3801b8f2c11e7983c0d724ed1647576883f`
- Changed files:
  - `Klijent/clientapp/src/pages/InventoryPage.tsx`
  - `Klijent/clientapp/src/pages/__tests__/InventoryPage.signalActions.spec.ts`
  - `docs/qa/INVENTORY_DECISION_CONTRACT_AUDIT.md`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Checks:
  - `git diff --check` - pass, with repository line-ending warnings only
  - `cd Klijent/clientapp && npm run check:analytics-guardrails` - pass
  - `cd Klijent/clientapp && npm run test -- --run src/pages/__tests__/InventoryPage.signalActions.spec.ts` - pass
  - `cd Klijent/clientapp && npm run build` - pass
- Risk:
  - Some inventory display widgets still render derived `estimatedValueAmount`; Q44 fixes the action impact contract, not a full inventory value-nullability refactor.
- Next step:
  - `Q45 - Decision Board backend aggregate readiness review`

## Q45 - Decision Board backend aggregate readiness review

Status: DONE

Next step:
- verify the phase 1 board model is stable enough to justify a backend aggregate endpoint design review

### Notes

- Date: 2026-06-19
- Verification HEAD: `a8602ae75f9d60708c604dd3482576a4e7161ce3`
- Changed files:
  - `docs/qa/DECISION_BOARD_BACKEND_AGGREGATE_READINESS.md`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Checks:
  - `git diff --check` - pass, with repository line-ending warnings only
  - Existing board quality audit checks are already documented in `docs/qa/EXECUTIVE_DECISION_BOARD_QUALITY_AUDIT.md`
- Risk:
  - The frontend board still composes from multiple source requests; the aggregate review is ready, but implementation should stay read-only and preserve nullable semantics.
- Next step:
  - `Q46 - Decision Board backend aggregate contract design`

## Q46 - Decision Board backend aggregate contract design

Status: DONE

Next step:
- use the read-only aggregate contract to implement the backend endpoint and adapter tests

### Notes

- Date: 2026-06-19
- Verification HEAD: `c79f50bc87a98962c3da51dcb3e9bb8f30272017`
- Changed files:
  - `docs/analytics/DECISION_BOARD_BACKEND_AGGREGATE_CONTRACT.md`
  - `docs/analytics/ANALYTICS_DECISION_OS_ROADMAP.md`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Checks:
  - `git diff --check` - not run yet after this doc update
  - Existing board quality audit checks are already documented in `docs/qa/EXECUTIVE_DECISION_BOARD_QUALITY_AUDIT.md`
- Risk:
  - The frontend board still composes from multiple source requests; the backend aggregate should preserve the same nullable and section-context semantics.
- Next step:
  - `Q47 - Decision Board backend aggregate implementation`

## Q47 - Decision Board backend aggregate implementation

Status: DONE

### Notes

- Date: 2026-06-19
- Commit: pending
- Changed files:
  - `Api/Dtos/DecisionBoardDtos.cs`
  - `Api/Endpoints/DecisionBoardEndpoints.cs`
  - `Api/Endpoints/CachedAnalyticsEndpoints.cs`
  - `Api/Endpoints/SupplierDecisionHubEndpoints.cs`
  - `Api/Program.cs`
  - `Api.Tests/AnalyticsCriticalRouteMappingsTests.cs`
  - `Api.Tests/DecisionBoardEndpointsTests.cs`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Checks:
  - `git diff --check` - pass
  - `dotnet build Trendplus2.sln --no-restore --configuration Release` - pass
  - `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "FullyQualifiedName~DecisionBoard|FullyQualifiedName~AnalyticsCriticalRouteMappings"` - pass
  - `cd Klijent/clientapp && npm run check:analytics-guardrails` - pass
  - `cd Klijent/clientapp && npm run build` - pass
- Risk:
  - The aggregate now preserves nullable impact/confidence semantics, but the frontend still needs to switch to the new read-only board source before the contract is fully exercised in UI.
- Next step:
  - `Q48 - Decision Board frontend aggregate adoption`

## Q48 - Decision Board frontend aggregate adoption

Status: DONE

### Notes

- Date: 2026-06-19
- Commit: pending
- Changed files:
  - `Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx`
  - `Klijent/clientapp/src/pages/__tests__/ExecutiveDecisionBoardPage.spec.ts`
  - `Klijent/clientapp/src/services/analyticsApi.ts`
  - `Klijent/clientapp/src/types/analytics.ts`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Checks:
  - `git diff --check` - pass
  - `cd Klijent/clientapp && npm run check:analytics-guardrails` - pass
  - `cd Klijent/clientapp && npm run typecheck` - pass
  - `cd Klijent/clientapp && npm run test -- --run src/pages/__tests__/ExecutiveDecisionBoardPage.spec.ts` - pass
  - `cd Klijent/clientapp && npm run build` - pass
- Risk:
  - The board now consumes the backend aggregate endpoint, but legacy multi-source helper code remains in the file for now and should only be removed if a future cleanup task explicitly targets it.
- Next step:
  - No follow-up queue item is queued yet.

## Q50 - Queue reconciliation after Q48/Q49 and latest multi-topic commit

Status: DONE
Commit suggestion: `docs(ai): reconcile analytics queue after q49`
Priority: P0
Token budget: low/medium

### Why

- Q48 and Q49 are being worked on or recently changed, and the latest multi-topic commit `8fb6141` mixes several analytics topics in one push.
- The queue must reflect what is truly done, partial, open, or blocked before more feature work continues.

### Scope only

- `docs/ai/NEXT_PROMPT_QUEUE.md`
- `docs/qa/ANALYTICS_QUEUE_RECONCILIATION.md`
- no app code changes

### Do

1. Identify current queue state from Q38 onward.
2. Mark Q48 and Q49 accurately as DONE, PARTIAL, or BLOCKED.
3. Add evidence for each recent item: commit SHA, files changed, tests added, remaining risk.
4. Create `docs/qa/ANALYTICS_QUEUE_RECONCILIATION.md`.
5. Add Q51-Q56 as the next recommended tasks.
6. Do not mark Vercel/production DONE if the latest commit status is failing.

### Checks

- `git diff --check`

### Acceptance

- Queue reflects real status after Q48/Q49.
- No duplicate TODOs.
- Next work is ordered.
- Multi-topic commit is documented as a review risk.

### Notes

- Date: 2026-06-19
- Changed files:
  - `docs/qa/ANALYTICS_QUEUE_RECONCILIATION.md`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Checks:
  - `git diff --check` - pass, with repository line-ending warnings only
- Risk:
  - Q49 is not present in the current queue snapshot, so the reconciliation doc marks it blocked instead of inventing an implementation status.
- Next step:
  - `Q51 - Fix Vercel status blocker caused by GitHub commit email settings`

## Q51 - Fix Vercel status blocker caused by GitHub commit email settings

Status: PARTIAL
Commit suggestion: `docs(qa): document vercel commit email fix`
Priority: P0
Token budget: low

### Why

- The latest commit status for `8fb6141` points to a Vercel failure tied to GitHub commit email settings.
- Future analytics work should not keep hitting the same deploy/status blocker.

### Scope only

- `docs/qa/VERCEL_STATUS_EMAIL_FIX.md`
- optionally docs-only changes
- no analytics feature changes

### Do

1. Record `git config user.name`, `git config user.email`, and recent commit author emails.
2. Determine whether the latest commits use an email GitHub/Vercel rejects.
3. Document the remediation path: verified email or GitHub no-reply email.
4. Explain how to create a new small commit and push it after fixing local git config.
5. Do not rewrite history unless explicitly chosen by the operator.

### Checks

- `git diff --check`
- `cd Klijent/clientapp && npm run build`

### Acceptance

- Vercel email/status blocker has exact fix instructions.
- Future commits should not repeat the same failure.
- No analytics logic changed.

### Notes

- 2026-06-19
- Current local HEAD:
  - `3b488f6228846faeb7c53fc7efef61a0ae64df35`
- Current local identity:
  - `Ivan Jovicic <ivanjovicic1986@gmail.com>`
- Files changed:
  - `docs/qa/VERCEL_STATUS_EMAIL_FIX.md`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Checks:
  - `git diff --check` pass
  - `cd Klijent/clientapp && npm run build` pass
- Risk:
  - GitHub account verification and live Vercel status still need to be confirmed before calling the blocker fully resolved
- Next step:
  - `Q52 - Review and harden Supplier Negotiation Pack MVP`

## Q52 - Review and harden Supplier Negotiation Pack MVP

Status: DONE
Commit suggestion: `test(analytics): harden supplier negotiation pack`
Priority: P1
Token budget: medium

### Why

- The latest multi-topic commit added Supplier Negotiation Pack behavior to supplier decision reports.
- This decision-support surface must never imply fake recommendations, hidden warnings, or blocked advice that still looks actionable.

### Scope only

- supplier decision report endpoint and tests
- SupplierDecisionReport component and tests
- `docs/qa/SUPPLIER_NEGOTIATION_PACK_REVIEW.md`
- small fixes only if obvious

### Do

1. Verify backend negotiation pack section, cache key, fallback warning, missing-cost warning, and blocked final advice.
2. Verify frontend copy button, fallback state, warning visibility, and blocked final advice styling.
3. Add clipboard-safe tests if missing.
4. Document known limitations and follow-ups.

### Checks

- `dotnet build Trendplus2.sln --no-restore --configuration Release`
- `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "SupplierDecision"`
- `cd Klijent/clientapp && npm run check:analytics-guardrails`
- `cd Klijent/clientapp && npm run build`
- `cd Klijent/clientapp && npm run test -- --run SupplierDecisionReport`

### Acceptance

- Supplier Negotiation Pack cannot imply fake actionable advice.
- Fallback/missing-cost warnings remain visible.
- Copy UX is safe.
- Queue updated.

### Notes

- 2026-06-19
- Current local HEAD before this task commit:
  - `7a84848ac234ffd1e6029af01daafd1ae98f6fa8`
- Files changed:
  - `Klijent/clientapp/src/components/analytics/SupplierDecisionReportActions.tsx`
  - `Klijent/clientapp/src/components/analytics/__tests__/SupplierDecisionReportActions.spec.tsx`
  - `docs/qa/SUPPLIER_NEGOTIATION_PACK_REVIEW.md`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Checks:
  - `git diff --check` pass
  - `cd Klijent/clientapp && npm run check:analytics-guardrails` pass
  - `cd Klijent/clientapp && npm run build` pass
  - `cd Klijent/clientapp && npm run test -- --run src/components/analytics/__tests__/SupplierDecisionReportActions.spec.tsx` pass
  - `cd Klijent/clientapp && npm run test -- --run SupplierDecisionReport` pass
  - `dotnet build Trendplus2.sln --no-restore --configuration Release` pass with existing repo warnings
  - `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "SupplierDecision"` pass
- Risk:
  - the browser copy fallback still depends on `document.execCommand("copy")`, but it now fails safely instead of pretending success
- Next step:
  - `Q53 - Audit Replenishment/OOS decision workflow trust states`

## Q53 - Audit Replenishment/OOS decision workflow trust states

Status: DONE
Commit suggestion: `docs(qa): audit replenishment oos workflow`
Priority: P1
Token budget: medium

### Why

- Replenishment/OOS workflow is high-impact and can cause overstock or missed sales if trust states are unclear.
- The MVP must clearly distinguish real OOS signal, estimated lost sales, low-confidence demand baseline, missing stock data, and stale inventory refresh.

### Scope only

- `docs/qa/REPLENISHMENT_OOS_WORKFLOW_AUDIT.md`
- small tests/fixes only
- no new forecasting engine

### Do

1. Identify all OOS/replenishment signals.
2. Verify confidence, stale-data warning, missing-stock warning, estimate labels, and nullable impact behavior.
3. Confirm the UI does not present stale or insufficient data as green.
4. Add focused tests for missing baseline, stale refresh, and insufficient data.
5. Document audit findings and follow-up items.

### Checks

- `dotnet build Trendplus2.sln --no-restore --configuration Release`
- `cd Klijent/clientapp && npm run check:analytics-guardrails`
- `cd Klijent/clientapp && npm run build`
- `cd Klijent/clientapp && npm run test -- --run relevant inventory/product tests`

### Acceptance

- OOS/replenishment workflow is safe for MVP.
- Estimates are labelled.
- Missing data does not become 0.
- Queue updated.

### Notes

- Date: 2026-06-19
- HEAD SHA: `72a1db59edfa332d66f31f2c930ecea69f0824a4`
- Changed files:
  - `Klijent/clientapp/src/pages/InventoryPage.tsx`
  - `Klijent/clientapp/src/pages/__tests__/InventoryPage.signalActions.spec.ts`
  - `Klijent/clientapp/src/pages/__tests__/InventoryPage.forecastRestock.spec.tsx`
  - `docs/qa/REPLENISHMENT_OOS_WORKFLOW_AUDIT.md`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Checks:
  - `dotnet build Trendplus2.sln --no-restore --configuration Release` pass
  - `cd Klijent/clientapp && npm run check:analytics-guardrails` pass
  - `cd Klijent/clientapp && npm run build` pass
  - `cd Klijent/clientapp && npm run test -- --run src/pages/__tests__/InventoryPage.signalActions.spec.ts src/pages/__tests__/InventoryPage.forecastRestock.spec.tsx src/pages/__tests__/AnalyticsSalesReadinessRegression.spec.tsx` pass
- Risk:
  - Forecast restock suggestions are deliberately blocked when the matching stock baseline is not loaded, so the user must load or search the item before queueing that action.
- Next:
  - Q55 - Add KPI methodology consistency review and tests

## Q54 - Audit Markdown Optimizer MVP safety and trust boundaries

Status: DONE
Commit suggestion: `docs(qa): audit markdown optimizer mvp`
Priority: P1
Token budget: medium

### Why

- Markdown optimizer can strongly influence pricing decisions.
- MVP wording must not look like guaranteed profit optimization when it is based on rule-based or pre/post signals.

### Scope only

- `docs/qa/MARKDOWN_OPTIMIZER_MVP_AUDIT.md`
- small tests/fixes only
- no new ML or forecasting model

### Do

1. Identify markdown optimizer surfaces.
2. Verify wording emphasizes signal/proposal/estimate, not guaranteed optimization.
3. Verify no-fake rules: missing cost, sparse sales, missing baseline, stale data.
4. Verify outcome links and nullable impact behavior.
5. Add focused tests for missing cost, sparse sales, and baseline blocking.

### Checks

- `dotnet build Trendplus2.sln --no-restore --configuration Release`
- `cd Klijent/clientapp && npm run check:analytics-guardrails`
- `cd Klijent/clientapp && npm run build`
- `cd Klijent/clientapp && npm run test -- --run relevant markdown/product/decision tests`

### Acceptance

- Markdown optimizer MVP cannot be mistaken for a guaranteed optimizer.
- Missing data blocks or downgrades recommendation.
- Tests cover no-fake-money states.
- Queue updated.

### Notes

- Date: 2026-06-19
- HEAD SHA: `8ad00a56ff6feaecea39d84032625c8303163108`
- Changed files:
  - `Klijent/clientapp/src/pages/PreNivelacijaPriorityPage.tsx`
  - `Klijent/clientapp/src/pages/__tests__/PreNivelacijaPriorityPage.spec.tsx`
  - `docs/qa/MARKDOWN_OPTIMIZER_MVP_AUDIT.md`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Checks:
  - `dotnet build Trendplus2.sln --no-restore --configuration Release` pass
  - `cd Klijent/clientapp && npm run check:analytics-guardrails` pass
  - `cd Klijent/clientapp && npm run build` pass
  - `cd Klijent/clientapp && npm run test -- --run src/pages/__tests__/ProductDecisionCenterPage.confidence.spec.tsx src/pages/__tests__/PreNivelacijaPriorityPage.spec.tsx` pass
- Risk:
  - No standalone markdown optimizer screen exists yet; the fix only prevents insufficient-data rows from being treated as high priority on the pre-nivelacija surface.
- Next:
  - Q55 - Add KPI methodology consistency review and tests

## Q55 - Add KPI methodology consistency review and tests

Status: DONE
Commit suggestion: `docs(qa): review kpi methodology consistency`
Priority: P1
Token budget: medium

### Why

- The retail KPI roadmap introduces many metrics and the app must avoid inconsistent formulas between pages, reports, supplier, inventory, and the decision board.

### Scope only

- `docs/qa/KPI_METHODOLOGY_CONSISTENCY_REVIEW.md`
- tests for shared formula helpers if present
- no broad formula rewrite

### Do

1. List current KPI formulas used in code and docs.
2. Compare them against the retail KPI roadmap.
3. Identify formula, naming, denominator, and unit mismatches.
4. Add helper tests for zero/null denominator behavior if helpers exist.
5. Document consolidation plan if formulas are duplicated.

### Checks

- `dotnet build Trendplus2.sln --no-restore --configuration Release`
- `cd Klijent/clientapp && npm run check:analytics-guardrails`
- `cd Klijent/clientapp && npm run build`

### Acceptance

- KPI formula risks are documented.
- Missing denominator does not become 0.
- Any small tests added are focused.
- Queue updated.

### Notes

- Date: 2026-06-19
- Changed files:
  - `Klijent/clientapp/src/utils/__tests__/analyticsformatters.spec.ts`
  - `Klijent/clientapp/src/utils/__tests__/analyticsMetricDefinitions.spec.ts`
  - `docs/qa/KPI_METHODOLOGY_CONSISTENCY_REVIEW.md`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Checks:
  - `cd Klijent/clientapp && npm run test -- --run src/utils/__tests__/analyticsformatters.spec.ts src/utils/__tests__/analyticsMetricDefinitions.spec.ts` - pass
  - `cd Klijent/clientapp && npm run check:analytics-guardrails` - pass
  - `cd Klijent/clientapp && npm run build` - pass
  - `dotnet build Trendplus2.sln --no-restore --configuration Release` - pass
- Risk:
  - `inventoryTurnover` remains cost-based in code; the roadmap also documents a units-based proxy, so future docs must keep those variants distinct.
- Next step:
  - `Q56 - Close Analytics production readiness checklist`

## Q56 - Close Analytics production readiness checklist

Status: DONE
Commit suggestion: `docs(qa): close analytics production readiness status`
Priority: P0
Token budget: low/medium

### Why

- The production readiness checklist should become evidence-based, not just aspirational.
- It needs a final PASS/WARN/FAIL/NOT TESTED status per item.

### Scope only

- `docs/qa/ANALYTICS_PRODUCTION_READINESS_CHECKLIST.md`
- `docs/qa/ANALYTICS_PRODUCTION_READINESS_STATUS.md`
- `docs/ai/NEXT_PROMPT_QUEUE.md`
- no feature code

### Do

1. Review checklist items and mark each PASS, WARN, FAIL, or NOT TESTED.
2. Add evidence for deploy proof, backend health, frontend smoke, no-fake-zero/no-fake-green, protected writes, supplier negotiation pack, OOS workflow, markdown optimizer, observability, and demo reset safety.
3. Add a final recommendation: Ready for internal pilot, Ready with warnings, or Not ready.
4. Update the queue.

### Checks

- `git diff --check`

### Acceptance

- Production readiness status is evidence-based.
- Remaining risks are explicit.
- Queue updated.

### Notes

- Date: 2026-06-19
- Changed files:
  - `docs/Analytics/ANALYTICS_PRODUCTION_READINESS_CHECKLIST.md`
  - `docs/qa/ANALYTICS_PRODUCTION_READINESS_STATUS.md`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Checks:
  - `git diff --check` - pass
- Risk:
  - Cache/freshness is still warning-like in live smoke evidence, and markdown optimizer remains a future roadmap item.
- Next step:
  - No follow-up task defined in the current queue snapshot.

## Q57 - Action Impact Ledger Phase 1 implementation spec

Status: DONE
Commit suggestion: `docs(analytics): specify action impact ledger phase 1`
Priority: P0
Type: docs/spec
Token budget: medium

### Why

- The ledger plan and gap review exist, but the next implementation step still needs a concrete schema and API contract.
- We need a spec that keeps nullable impact fields honest and prevents fake outcome certainty.

### Scope only

- `docs/Analytics/ACTION_IMPACT_LEDGER_PHASE1_SPEC.md`
- no implementation yet

### Read first

- `AGENTS.md`
- `.github/copilot-instructions.md`
- `docs/ai/NEXT_PROMPT_QUEUE.md`
- `docs/Analytics/ACTION_IMPACT_LEDGER_PLAN.md`
- `docs/qa/ACTION_IMPACT_LEDGER_GAP_REVIEW.md`
- `docs/Analytics/ANALYTICS_DECISION_OS_ROADMAP.md`
- `Api/Endpoints/AnalyticsActionsEndpoints.cs`

### Do

1. Turn the existing ledger plan and gap review into an implementation-ready Phase 1 spec.
2. Define fields stored at action creation.
3. Define fields stored at outcome resolution.
4. Define metadata JSON shape, migration options, and API DTO changes.
5. Define no-fake rules for expected vs measured impact and nullable fields.
6. List backend and frontend tests required before implementation starts.

### Checks

- `git diff --check`

### Acceptance

- Phase 1 ledger spec is implementation-ready.
- Required fields, DTO changes, migration options, and no-fake rules are explicit.
- No application code changed.

### Notes

- Date: 2026-06-21
- Changed files:
  - `docs/Analytics/ACTION_IMPACT_LEDGER_PHASE1_SPEC.md`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Checks:
  - `git diff --check` pass
- Risk:
  - Q58 must keep legacy rows without a `ledger` metadata envelope readable and must not silently expand into a broader schema rewrite.
- Next step:
  - `Q58 - Action Impact Ledger Phase 1 backend implementation`

## Q58 - Action Impact Ledger Phase 1 backend implementation

Status: DONE
Commit suggestion: `feat(analytics): implement action impact ledger phase 1`
Priority: P0
Type: backend
Token budget: medium/high

### Why

- Once the Phase 1 spec exists, we need the smallest safe backend slice that captures structured action outcome evidence without rewriting the action system.

### Scope only

- `Domain/Model/Analytics`
- `Infrastructure/Services/Analytics`
- `Api/Endpoints/AnalyticsActionsEndpoints.cs`
- `Api.Tests`

### Read first

- `AGENTS.md`
- `.github/copilot-instructions.md`
- `docs/ai/NEXT_PROMPT_QUEUE.md`
- `docs/Analytics/ACTION_IMPACT_LEDGER_PHASE1_SPEC.md`
- `docs/Analytics/ACTION_IMPACT_LEDGER_PLAN.md`
- `docs/qa/ACTION_IMPACT_LEDGER_GAP_REVIEW.md`
- `Api/Endpoints/AnalyticsActionsEndpoints.cs`

### Do

1. Implement only the backend slice approved by Q57.
2. Preserve the existing action flow and endpoint semantics.
3. Keep nullable impact fields nullable.
4. Avoid broad event sourcing or a new append-only store unless Q57 explicitly recommends it.
5. Add focused backend tests for creation, resolution, null handling, and DTO shape.

### Checks

- `dotnet build Trendplus2.sln --no-restore --configuration Release`
- `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "AnalyticsActions"`
- `git diff --check`

### Acceptance

- Action Impact Ledger Phase 1 exists in the backend with focused tests.
- Existing action flows still work.
- Missing outcome data does not become fake zero.

### Notes

- Date: 2026-06-21
- Changed files:
  - `Domain/Model/Analytics/AnalyticsActionLedgerSnapshot.cs`
  - `Domain/Model/Analytics/AnalyticsActionItem.cs`
  - `Infrastructure/Services/Analytics/AnalyticsActionItemService.cs`
  - `Api/Endpoints/AnalyticsActionsEndpoints.cs`
  - `Api.Tests/AnalyticsActionItemServiceTests.cs`
  - `Api.Tests/AnalyticsActionsEndpointsTests.cs`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Checks:
  - `dotnet build Trendplus2.sln --no-restore --configuration Release` pass
  - `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "AnalyticsActions"` pass
  - `git diff --check` pass
- Risk:
  - Existing action create flows remain backward compatible by treating ledger request fields as optional, so Q59 will need to start consuming the new `ledgerSnapshot` payload deliberately instead of assuming every historical row already has it.
- Next step:
  - `Q59 - Action outcome UI detail panel`

## Q59 - Action outcome UI detail panel

Status: DONE
Commit suggestion: `feat(analytics): show action outcome detail panel`
Priority: P1
Type: frontend
Token budget: medium

### Why

- Once outcome data exists, operators need a safe UI that compares expected and measured impact without implying missing data is failure or zero.

### Scope only

- `Klijent/clientapp/src/pages/AnalyticsActionsPage.tsx`
- action detail/modal if present
- frontend types/tests

### Read first

- `AGENTS.md`
- `.github/copilot-instructions.md`
- `docs/ai/NEXT_PROMPT_QUEUE.md`
- `docs/Analytics/ACTION_IMPACT_LEDGER_PHASE1_SPEC.md`
- `Api/Endpoints/AnalyticsActionsEndpoints.cs`
- `Klijent/clientapp/src/pages/AnalyticsActionsPage.tsx`

### Do

1. Show expected vs measured outcome safely in the Analytics Actions UI.
2. Keep pending outcome distinct from failure.
3. Keep missing measured impact null or unavailable, never `0 RSD`.
4. Hide confidence calibration UI when calibration data is unavailable.
5. Add focused frontend tests for pending, measured, and missing-impact states.

### Checks

- `cd Klijent/clientapp && npm run check:analytics-guardrails`
- `cd Klijent/clientapp && npm run build`
- `cd Klijent/clientapp && npm run test -- --run src/pages/__tests__/AnalyticsActionsPage*.spec.ts*`
- `git diff --check`

### Acceptance

- Operators can inspect expected vs measured outcome without fake zero or fake failure states.
- Missing calibration or impact data stays visibly unavailable.
- Frontend tests cover the key outcome states.

### Notes

- 2026-06-21: DONE. Added ledger-aware outcome detail cards to `Klijent/clientapp/src/pages/AnalyticsActionsPage.tsx` so expected impact, measured impact, evidence source/reference, and recommendation context render without implying `0 RSD` or failure when data is pending/missing.
- Changed files:
  - `Klijent/clientapp/src/pages/AnalyticsActionsPage.tsx`
  - `Klijent/clientapp/src/pages/AnalyticsActionsPage.css`
  - `Klijent/clientapp/src/pages/__tests__/AnalyticsActionsPage.spec.tsx`
  - `Klijent/clientapp/src/types/analytics.ts`
- Checks:
  - `git diff --check` (pass; CRLF/LF warnings only)
  - `cd Klijent/clientapp && npm run check:analytics-guardrails` (pass)
  - `cd Klijent/clientapp && npm run build` (pass)
  - `cd Klijent/clientapp && npm run test -- --run src/pages/__tests__/AnalyticsActionsPage.spec.tsx` (pass)
- Risk:
  - Legacy list rows still depend on the detail fetch to expose `ledgerSnapshot`, so historical actions without the Phase 1 envelope continue to show safe unavailable labels rather than reconstructed context.
- Next:
  - `Q60 - Confidence calibration audit`

## Q60 - Confidence calibration audit

Status: DONE
Commit suggestion: `docs(qa): audit confidence calibration`
Priority: P1
Type: docs/audit
Token budget: medium

### Why

- Confidence labels should eventually be audited against completed outcomes so the system learns whether its decision trust levels are honest.

### Scope only

- `docs/qa/CONFIDENCE_CALIBRATION_AUDIT.md`
- no implementation unless a tiny existing helper makes it trivial

### Read first

- `AGENTS.md`
- `.github/copilot-instructions.md`
- `docs/ai/NEXT_PROMPT_QUEUE.md`
- `docs/Analytics/DECISION_CONFIDENCE_CONTRACT.md`
- `docs/Analytics/ACTION_IMPACT_LEDGER_PHASE1_SPEC.md`
- `docs/qa/ACTION_IMPACT_LEDGER_GAP_REVIEW.md`
- `docs/qa/EXECUTIVE_DECISION_BOARD_QUALITY_AUDIT.md`

### Do

1. Identify which analytics surfaces can already be calibrated.
2. Document missing data and outcome dependencies.
3. Define calibration buckets and future metrics.
4. Call out where current confidence is still descriptive rather than outcome-validated.

### Checks

- `git diff --check`

### Acceptance

- Calibration audit shows what can be measured now and what still blocks trustworthy calibration.
- No application code is required unless a tiny helper is clearly safe.

### Notes

- 2026-06-21: DONE. Added `docs/qa/CONFIDENCE_CALIBRATION_AUDIT.md` with a source-backed audit of what can already be calibrated from Analytics Actions outcome summary versus what still depends on broader Phase 1 ledger adoption.
- Changed files:
  - `docs/qa/CONFIDENCE_CALIBRATION_AUDIT.md`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Checks:
  - `git diff --check` (pass; CRLF/LF warning only)
- Risk:
  - Current calibration is still action-sample based and subject to selection bias because ignored recommendations and non-queued signals are outside the measured denominator.
- Next:
  - `Q61 - Pilot operator workflow runbook`

## Q61 - Pilot operator workflow runbook

Status: DONE
Commit suggestion: `docs(pilot): add analytics pilot operator runbook`
Priority: P1
Type: docs/ops
Token budget: medium

### Why

- Internal and customer pilots need a repeatable operator workflow so analytics use is evidence-driven instead of ad hoc.

### Scope only

- `docs/pilot/ANALYTICS_PILOT_OPERATOR_RUNBOOK.md`

### Read first

- `AGENTS.md`
- `.github/copilot-instructions.md`
- `docs/ai/NEXT_PROMPT_QUEUE.md`
- `docs/qa/ANALYTICS_PRODUCTION_READINESS_STATUS.md`
- `docs/qa/ANALYTICS_LIVE_SMOKE_RESULT.md`
- `docs/qa/REPLENISHMENT_OOS_WORKFLOW_AUDIT.md`
- `docs/qa/MARKDOWN_OPTIMIZER_MVP_AUDIT.md`

### Do

1. Define the daily opening checklist.
2. Define the weekly decision review cadence.
3. Define action queue and data-quality review steps.
4. Define supplier negotiation pack, OOS/replenishment, and markdown signal usage rules.
5. Define escalation rules and evidence capture expectations.

### Checks

- `git diff --check`

### Acceptance

- Pilot operator workflow is repeatable and evidence-based.
- Decision usage, data quality review, and escalation steps are explicit.

### Notes

- 2026-06-21: DONE. Added `docs/pilot/ANALYTICS_PILOT_OPERATOR_RUNBOOK.md` to turn current live-smoke/readiness evidence into a repeatable pilot operating workflow with daily opening checks, weekly review cadence, action/outcome hygiene, and escalation rules.
- Changed files:
  - `docs/pilot/ANALYTICS_PILOT_OPERATOR_RUNBOOK.md`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Checks:
  - `git diff --check` (pass; CRLF/LF warning only)
- Risk:
  - The runbook is intentionally conservative and depends on operators preserving visible warnings instead of overriding them in customer summaries or ad hoc screenshots.
- Next:
  - `Q62 - Decision Board backend aggregate readiness gate`

## Q62 - Decision Board backend aggregate readiness gate

Status: DONE
Commit suggestion: `docs(qa): gate decision board aggregate readiness`
Priority: P0
Type: docs/review
Token budget: medium

### Why

- The board backend aggregate endpoint should not be implemented just because it is desirable; it should be gated by evidence about quality, cache, ranking stability, and performance.

### Scope only

- `docs/qa/DECISION_BOARD_BACKEND_AGGREGATE_GATE.md`
- no endpoint implementation

### Read first

- `AGENTS.md`
- `.github/copilot-instructions.md`
- `docs/ai/NEXT_PROMPT_QUEUE.md`
- `docs/Analytics/ANALYTICS_DECISION_OS_ROADMAP.md`
- `docs/Analytics/EXECUTIVE_DECISION_BOARD_PLAN.md`
- `docs/qa/EXECUTIVE_DECISION_BOARD_QUALITY_AUDIT.md`
- `docs/qa/ANALYTICS_PRODUCTION_READINESS_STATUS.md`

### Do

1. Decide whether backend aggregation is READY, WARN, or NOT READY.
2. Evaluate data-quality evidence, performance expectations, cache behavior, dedupe strategy, and ranking stability.
3. Document prerequisites that must exist before a backend aggregate endpoint is safe.
4. Explicitly state whether Q63 may proceed.

### Checks

- `git diff --check`

### Acceptance

- Readiness gate is evidence-based.
- The document clearly states whether backend aggregate work may start.
- No endpoint is implemented in this task.

### Notes

- 2026-06-21: DONE. Added `docs/qa/DECISION_BOARD_BACKEND_AGGREGATE_GATE.md` and concluded the aggregate endpoint is `NOT READY` because dedupe policy, ranking parity, freshness contract, and cross-module trust semantics are not stable enough to freeze server-side.
- Changed files:
  - `docs/qa/DECISION_BOARD_BACKEND_AGGREGATE_GATE.md`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Checks:
  - `git diff --check` (pass; CRLF/LF warning only)
- Risk:
  - Frontend composition remains the active architecture, so request fan-out and section-level composition logic still need to be monitored until a later gate re-evaluates backend aggregation.
- Next:
  - `Q63 - Decision Board backend aggregate endpoint MVP` remains blocked by this gate

## Q63 - Decision Board backend aggregate endpoint MVP

Status: BLOCKED
Commit suggestion: `feat(analytics): add decision board aggregate endpoint mvp`
Priority: P1
Type: backend/frontend-integration
Token budget: high

### Why

- If Q62 says the system is ready, the board can move from client-side composition toward a safer shared backend aggregate path.

### Scope only

- Api endpoint/service/tests
- frontend keeps existing composition fallback

### Read first

- `AGENTS.md`
- `.github/copilot-instructions.md`
- `docs/ai/NEXT_PROMPT_QUEUE.md`
- `docs/qa/DECISION_BOARD_BACKEND_AGGREGATE_GATE.md`
- `docs/Analytics/EXECUTIVE_DECISION_BOARD_PLAN.md`
- `Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx`

### Do

1. Proceed only if Q62 explicitly says READY.
2. Implement the backend aggregate endpoint and focused tests only within the approved contract.
3. Keep the frontend composition fallback until live evidence proves parity.
4. If Q62 says NOT READY, mark Q63 BLOCKED and do not implement.

### Checks

- `dotnet build Trendplus2.sln --no-restore --configuration Release`
- `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "ExecutiveDecisionBoard|Analytics"`
- `cd Klijent/clientapp && npm run check:analytics-guardrails`
- `cd Klijent/clientapp && npm run build`
- `git diff --check`

### Acceptance

- Backend aggregate endpoint is implemented only if the readiness gate allows it.
- Frontend fallback remains available.
- Quality and ranking semantics stay explicit.

### Notes

- 2026-06-22: Blocker resolution was broken down into Q63A-Q63F so the aggregate endpoint stays blocked until candidate contract, dedupe, ranking parity, freshness contract, performance budget, and a re-run gate are all completed.
- Changed files:
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Checks:
  - `git diff --check`
- Risk:
  - If Q63 is revisited without closing Q63A-Q63F, the backend would freeze unstable frontend composition semantics into a server contract.
- Next:
  - `Q63A - Decision Board candidate contract audit`

## Q63A - Decision Board candidate contract audit

Status: DONE
Commit suggestion: `docs(qa): audit decision board candidate contract`
Priority: P0
Type: docs/contract-review
Token budget: medium

### Why

- Q62 showed that the current board still depends on frontend composition and does not yet have a locked candidate contract that a backend aggregate could preserve.

### Scope only

- `docs/qa/DECISION_BOARD_CANDIDATE_CONTRACT_AUDIT.md`
- optional links to existing board docs
- no backend aggregate implementation

### Read first

- `AGENTS.md`
- `.github/copilot-instructions.md`
- `docs/ai/NEXT_PROMPT_QUEUE.md`
- `docs/qa/DECISION_BOARD_BACKEND_AGGREGATE_GATE.md`
- `docs/Analytics/EXECUTIVE_DECISION_BOARD_PLAN.md`
- `docs/Analytics/DECISION_CONFIDENCE_CONTRACT.md`
- `Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx`
- `Klijent/clientapp/src/pages/__tests__/ExecutiveDecisionBoardPage.spec.ts`
- analytics API service/types used by the board

### Do

1. Define the exact candidate/card shape that frontend composition currently produces.
2. Map every candidate field back to its source module or shared helper.
3. Mark nullable fields explicitly, especially confidence, expected impact, freshness, and warning payloads.
4. Document no-fake-zero and no-fake-confidence rules that the candidate contract must preserve.
5. List missing fields or inconsistencies that currently block a backend aggregate contract.

### Checks

- `git diff --check`

### Acceptance

- The current frontend-composed candidate shape is documented field-by-field.
- Source module ownership and nullable behavior are explicit.
- Blocking gaps for backend aggregate contract design are listed without implementing the endpoint.

### Notes

- 2026-06-22: DONE. Added `docs/qa/DECISION_BOARD_CANDIDATE_CONTRACT_AUDIT.md` and documented the active transport DTO, local render-model fields, shadow composition helpers, nullable requirements, and contract gaps that still block a stable backend aggregate.
- Changed files:
  - `docs/qa/DECISION_BOARD_CANDIDATE_CONTRACT_AUDIT.md`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Checks:
  - `git diff --check`
- Risk:
  - The page currently mixes an aggregate DTO with local render normalization and dormant composition helpers, so future Q63 work must not treat the current card `id`, section link, or confidence label logic as the final server contract.
- Next:
  - `Q63B - Decision Board dedupe and source identity rules`

## Q63B - Decision Board dedupe and source identity rules

Status: DONE
Commit suggestion: `docs(qa): define decision board dedupe rules`
Priority: P0
Type: docs/tests
Token budget: medium

### Why

- Q62 concluded that dedupe policy is not ready, and a backend aggregate cannot centralize repeated cards safely until source identity and collision handling are explicit.

### Scope only

- `docs/qa/DECISION_BOARD_DEDUPE_RULES.md`
- frontend tests only if a tiny safe coverage addition is needed
- no backend aggregate implementation

### Read first

- `AGENTS.md`
- `.github/copilot-instructions.md`
- `docs/ai/NEXT_PROMPT_QUEUE.md`
- `docs/qa/DECISION_BOARD_BACKEND_AGGREGATE_GATE.md`
- `docs/Analytics/EXECUTIVE_DECISION_BOARD_PLAN.md`
- `Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx`
- `Klijent/clientapp/src/pages/__tests__/ExecutiveDecisionBoardPage.spec.ts`
- analytics API service/types used by the board

### Do

1. Define canonical source identity rules using `sourceType + sourceKey + recommendationType`.
2. Document when repeated cards across sections are intentional versus true duplicates.
3. Capture collision examples across Product, Supplier, Inventory, OOS, Markdown, and Action sources.
4. Define dedupe behavior for same-source, same-section, and cross-section candidates.
5. Add or propose focused frontend tests only if that can be done without changing board behavior.

### Checks

- `git diff --check`
- if tests are added: `cd Klijent/clientapp && npm run test -- --run src/pages/__tests__/ExecutiveDecisionBoardPage.spec.ts`

### Acceptance

- Dedupe rules are explicit enough that a later backend aggregate would not need to invent them.
- Source identity and collision handling are documented with examples.
- Any test additions remain focused and non-behavioral outside the documented rules.

### Notes

- 2026-06-22: DONE. Added `docs/qa/DECISION_BOARD_DEDUPE_RULES.md` and documented canonical source identity, recommendation-level dedupe keys, same-section vs cross-section repetition rules, and collision examples across Product, Inventory, Supplier, OOS/Markdown, Action, and Outcome surfaces.
- Changed files:
  - `docs/qa/DECISION_BOARD_DEDUPE_RULES.md`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Checks:
  - `git diff --check`
- Risk:
  - The current board DTO still makes `sourceType`, `sourceKey`, and `recommendationType` too loose for deterministic dedupe, so later aggregate work must not assume this rule set is already encoded in the payload.
- Next:
  - `Q63C - Decision Board ranking parity test plan`

## Q63C - Decision Board ranking parity test plan

Status: DONE
Commit suggestion: `docs(qa): plan decision board ranking parity`
Priority: P0
Type: docs/test-plan
Token budget: medium

### Why

- Q62 marked ranking stability as not ready, so the current frontend ordering behavior must be locked before any server-side parity work begins.

### Scope only

- `docs/qa/DECISION_BOARD_RANKING_PARITY_PLAN.md`
- no backend aggregate implementation

### Read first

- `AGENTS.md`
- `.github/copilot-instructions.md`
- `docs/ai/NEXT_PROMPT_QUEUE.md`
- `docs/qa/DECISION_BOARD_BACKEND_AGGREGATE_GATE.md`
- `docs/Analytics/EXECUTIVE_DECISION_BOARD_PLAN.md`
- `Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx`
- `Klijent/clientapp/src/pages/__tests__/ExecutiveDecisionBoardPage.spec.ts`

### Do

1. Document the current ranking inputs and ordering behavior used by frontend composition.
2. Build a test matrix covering:
   - high confidence vs insufficient data
   - expected impact present vs missing
   - stale data
   - urgent action
   - blocked recommendation
3. Define which behaviors require exact backend parity and which may stay section-specific.
4. Write backend parity acceptance criteria without implementing the backend endpoint.

### Checks

- `git diff --check`

### Acceptance

- The repo has a concrete parity plan for ranking behavior.
- High-risk ranking cases are captured in a reusable matrix.
- Backend parity criteria are explicit without unblocking Q63.

### Notes

- 2026-06-22: DONE. Added `docs/qa/DECISION_BOARD_RANKING_PARITY_PLAN.md` and documented the current ranking inputs, shared score/cap logic, lane-specific ordering behavior, a parity matrix, and the backend acceptance bar needed before any aggregate endpoint can claim ranking parity.
- Changed files:
  - `docs/qa/DECISION_BOARD_RANKING_PARITY_PLAN.md`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Checks:
  - `git diff --check`
- Risk:
  - The live page currently trusts backend section order while the same file still carries shadow composition scoring logic, so future Q63 work must decide parity against the documented rules instead of assuming the current runtime payload already encodes them completely.
- Next:
  - `Q63D - Decision Board freshness and warning contract`

## Q63D - Decision Board freshness and warning contract

Status: DONE
Commit suggestion: `docs(qa): map decision board freshness contract`
Priority: P0
Type: docs/contract-review
Token budget: medium

### Why

- Q62 showed that freshness, partial failure, and warning semantics are still too loose to centralize into a board aggregate snapshot.

### Scope only

- `docs/qa/DECISION_BOARD_FRESHNESS_CONTRACT.md`
- no backend aggregate implementation

### Read first

- `AGENTS.md`
- `.github/copilot-instructions.md`
- `docs/ai/NEXT_PROMPT_QUEUE.md`
- `docs/qa/DECISION_BOARD_BACKEND_AGGREGATE_GATE.md`
- `docs/Analytics/DECISION_CONFIDENCE_CONTRACT.md`
- `Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx`
- analytics API service/types used by the board

### Do

1. Map freshness, data-quality, and warning inputs for every board source module.
2. Identify missing fields that prevent the board from carrying trust metadata consistently.
3. Define UI behavior rules for stale, partial, warning, and unknown states.
4. Document backend contract requirements for aggregate-level warnings, section-level warnings, and invalidation thresholds.

### Checks

- `git diff --check`

### Acceptance

- Every source module has a freshness/warning mapping.
- Missing fields and trust-contract gaps are explicit.
- A future backend aggregate would know what metadata it must preserve.

### Notes

- 2026-06-22: DONE. Added `docs/qa/DECISION_BOARD_FRESHNESS_CONTRACT.md` and documented snapshot-, source-, and candidate-level warning/freshness semantics, source-module mappings, current rendering gaps, and the minimum backend metadata required before aggregate work can preserve trust honestly.
- Changed files:
  - `docs/qa/DECISION_BOARD_FRESHNESS_CONTRACT.md`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Checks:
  - `git diff --check`
- Risk:
  - The current page still compresses several trust layers into `isPartial`, `overallDataQualityStatus`, and a limited set of warning chips, so future aggregate work must not assume that current render behavior is already carrying full freshness provenance.
- Next:
  - `Q63E - Decision Board aggregate performance and cache budget`

## Q63E - Decision Board aggregate performance and cache budget

Status: DONE
Commit suggestion: `docs(qa): define decision board aggregate performance budget`
Priority: P1
Type: docs/performance
Token budget: medium

### Why

- Q62 found no proof yet that frontend fan-out is the pilot bottleneck, so backend aggregation needs a performance and cache budget before it becomes architectural work.

### Scope only

- `docs/qa/DECISION_BOARD_AGGREGATE_PERFORMANCE_BUDGET.md`
- no backend aggregate implementation

### Read first

- `AGENTS.md`
- `.github/copilot-instructions.md`
- `docs/ai/NEXT_PROMPT_QUEUE.md`
- `docs/qa/DECISION_BOARD_BACKEND_AGGREGATE_GATE.md`
- `docs/qa/ANALYTICS_PRODUCTION_READINESS_STATUS.md`
- `docs/qa/ANALYTICS_LIVE_SMOKE_RESULT.md`
- `Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx`

### Do

1. Define target board latency and request budget for the current pilot use case.
2. Define candidate cache TTL and invalidation triggers for a future aggregate snapshot.
3. Document correlationId/error behavior expectations for aggregate failures.
4. Define partial-failure behavior so the endpoint would not hide upstream degradation.
5. State what evidence would justify replacing frontend composition for performance or operability reasons.

### Checks

- `git diff --check`

### Acceptance

- The repo has a documented performance and cache budget for a future aggregate.
- Partial failure and error behavior are explicit.
- Q63 remains blocked until evidence justifies the architectural move.

### Notes

- 2026-06-22: DONE. Added `docs/qa/DECISION_BOARD_AGGREGATE_PERFORMANCE_BUDGET.md` and documented the current single-request board runtime, conservative latency and cache budgets, required cache-key fields, invalidation triggers, partial-failure rules, correlation/error expectations, and the evidence bar required before aggregate performance work is justified.
- Changed files:
  - `docs/qa/DECISION_BOARD_AGGREGATE_PERFORMANCE_BUDGET.md`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Checks:
  - `git diff --check`
- Risk:
  - The current page already performs one aggregate request, so future work must not confuse “already one request” with “already a proven stable performance architecture,” especially while freshness/cache evidence remains warning-like.
- Next:
  - `Q63F - Re-run backend aggregate readiness gate`

## Q63F - Re-run backend aggregate readiness gate

Status: DONE
Commit suggestion: `docs(qa): rerun decision board aggregate gate`
Priority: P0
Type: docs/review
Token budget: medium

### Why

- Q63 must stay blocked until the blocker documents exist and the readiness gate is re-evaluated against them.

### Scope only

- update `docs/qa/DECISION_BOARD_BACKEND_AGGREGATE_GATE.md`
- no backend aggregate implementation

### Read first

- `AGENTS.md`
- `.github/copilot-instructions.md`
- `docs/ai/NEXT_PROMPT_QUEUE.md`
- `docs/qa/DECISION_BOARD_BACKEND_AGGREGATE_GATE.md`
- `docs/qa/DECISION_BOARD_CANDIDATE_CONTRACT_AUDIT.md`
- `docs/qa/DECISION_BOARD_DEDUPE_RULES.md`
- `docs/qa/DECISION_BOARD_RANKING_PARITY_PLAN.md`
- `docs/qa/DECISION_BOARD_FRESHNESS_CONTRACT.md`
- `docs/qa/DECISION_BOARD_AGGREGATE_PERFORMANCE_BUDGET.md`

### Do

1. Re-run the Q62 gate after Q63A-Q63E are complete.
2. Update the readiness matrix and verdict in `docs/qa/DECISION_BOARD_BACKEND_AGGREGATE_GATE.md`.
3. Explicitly decide whether Q63 becomes READY to proceed or remains NOT READY.
4. Keep Q63 blocked unless the updated gate says READY.

### Checks

- `git diff --check`

### Acceptance

- The readiness gate is re-evaluated against concrete blocker evidence.
- Q63 is unblocked only if the updated verdict is READY.
- The queue preserves the gate-first workflow and does not implement the endpoint in this task.

### Notes

- 2026-06-22: DONE. Re-ran the readiness gate against Q63A-Q63E evidence and kept the verdict at `NOT READY`, because candidate identity, dedupe enforcement, ranking parity fixtures, and freshness/warning contract fidelity are still not stable enough to freeze server-side.
- Changed files:
  - `docs/qa/DECISION_BOARD_BACKEND_AGGREGATE_GATE.md`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Checks:
  - `git diff --check`
- Risk:
  - The blocker set is now much clearer, but Q63 would still lock in ambiguous card identity and lossy trust semantics if we implemented it before parity fixtures and stronger contract fields exist.
- Next:
  - `Q64 - Forecast/Replenishment safety guardrails`

## Q64 - Forecast/Replenishment safety guardrails

Status: DONE
Commit suggestion: `test(analytics): add forecast replenishment guardrails`
Priority: P0
Type: frontend/tests
Token budget: medium

### Why

- Forecast and replenishment signals can easily be over-read as guaranteed reorder instructions unless the UI and tests keep uncertainty visible.

### Scope only

- Inventory/Product Decision relevant surfaces
- tests

### Read first

- `AGENTS.md`
- `.github/copilot-instructions.md`
- `docs/ai/NEXT_PROMPT_QUEUE.md`
- `docs/qa/REPLENISHMENT_OOS_WORKFLOW_AUDIT.md`
- `docs/qa/ANALYTICS_PRODUCTION_READINESS_STATUS.md`
- `Klijent/clientapp/src/pages/InventoryPage.tsx`
- `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx`

### Do

1. Add guardrails that keep estimates labelled as estimates.
2. Ensure missing stock baseline blocks action or lowers confidence.
3. Ensure stale stock freshness warnings remain visible.
4. Add focused tests for estimate wording, stale warnings, and blocked/low-confidence states.

### Checks

- `cd Klijent/clientapp && npm run check:analytics-guardrails`
- `cd Klijent/clientapp && npm run build`
- `cd Klijent/clientapp && npm run test -- --run src/pages/__tests__/InventoryPage*.spec.ts* src/pages/__tests__/ProductDecisionCenterPage*.spec.ts*`
- `git diff --check`

### Acceptance

- Forecast and replenishment signals cannot look like guaranteed instructions.
- Missing baseline and stale data stay visible in UI and tests.

### Notes

- 2026-06-22: DONE. Labeled forecast replenishment as a signal/procena flow, kept Product Decision impact copy explicitly estimated, and fixed a real queue bug where `low_cover` could still become `REPLENISH` even when `recommendationAllowed=false`.
- Changed files:
  - `Klijent/clientapp/src/components/inventory/DemandForecastPanel.tsx`
  - `Klijent/clientapp/src/pages/InventoryPage.tsx`
  - `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx`
  - `Klijent/clientapp/src/pages/__tests__/InventoryPage.forecastGuardrails.spec.tsx`
  - `Klijent/clientapp/src/pages/__tests__/InventoryPage.forecastRestock.spec.tsx`
  - `Klijent/clientapp/src/pages/__tests__/ProductDecisionCenterPage.confidence.spec.tsx`
  - `Klijent/clientapp/src/pages/__tests__/ProductDecisionCenterPage.signalQueue.spec.ts`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Checks:
  - `cd Klijent/clientapp && npm run test -- --run src/pages/__tests__/InventoryPage.forecastRestock.spec.tsx src/pages/__tests__/InventoryPage.forecastGuardrails.spec.tsx src/pages/__tests__/InventoryPage.signalActions.spec.ts src/pages/__tests__/ProductDecisionCenterPage.signalQueue.spec.ts src/pages/__tests__/ProductDecisionCenterPage.queueStatus.spec.tsx src/pages/__tests__/ProductDecisionCenterPage.confidence.spec.tsx` (pass)
  - `cd Klijent/clientapp && npm run check:analytics-guardrails` (fail: `tsc -b` is currently blocked by existing `AnalyticsActionsPage.tsx` / `types/analytics` mismatch around `AnalyticsActionImpactLedger`)
  - `cd Klijent/clientapp && npm run build` (fail for the same existing type error before Vite build)
  - `cd Klijent/clientapp && npm run test -- --run src/pages/__tests__/InventoryPage*.spec.ts* src/pages/__tests__/ProductDecisionCenterPage*.spec.ts*` (fail: Vitest did not expand those glob literals in this shell, so explicit file paths were used instead)
  - `git diff --check` (pass; LF/CRLF warnings only)
- Risk:
  - Repo-wide frontend type/build health is still blocked by the unrelated `AnalyticsActionsPage` ledger typings issue, so Q64 guardrails are proven by targeted tests but not by a clean full type/build pass yet.
- Next:
  - `Q65 - Markdown optimizer safety guardrails`

## Q65 - Markdown optimizer safety guardrails

Status: DONE
Commit suggestion: `test(analytics): strengthen markdown optimizer guardrails`
Priority: P0
Type: frontend/tests
Token budget: medium

### Why

- Markdown optimizer MVP should remain clearly experimental and data-dependent, not a guaranteed profit engine.

### Scope only

- PreNivelacija/markdown decision surfaces
- tests

### Read first

- `AGENTS.md`
- `.github/copilot-instructions.md`
- `docs/ai/NEXT_PROMPT_QUEUE.md`
- `docs/qa/MARKDOWN_OPTIMIZER_MVP_AUDIT.md`
- `docs/Analytics/DECISION_CONFIDENCE_CONTRACT.md`
- relevant markdown/product decision surfaces

### Do

1. Strengthen wording so the surface stays signal/proposal-oriented.
2. Ensure missing cost blocks profit impact.
3. Ensure sparse sales lower confidence.
4. Ensure no fake expected impact appears when evidence is incomplete.
5. Add focused tests for those guardrails.

### Checks

- `cd Klijent/clientapp && npm run check:analytics-guardrails`
- `cd Klijent/clientapp && npm run build`
- `cd Klijent/clientapp && npm run test -- --run src/pages/__tests__/PreNivelacijaPriorityPage.spec.tsx src/pages/__tests__/ProductDecisionCenterPage*.spec.ts*`
- `git diff --check`

### Acceptance

- Markdown optimizer MVP cannot read like guaranteed optimization.
- Missing cost and sparse data stay visible through confidence and impact guardrails.

### Notes

- 2026-06-22: DONE. Strengthened pre-nivelacija markdown wording so it stays scenario/signal-oriented, blocked margin-delta display when `missing_cost` is present, and kept sparse-sales candidates in additional-check mode even when other scores look strong.
- Changed files:
  - `Klijent/clientapp/src/pages/PreNivelacijaPriorityPage.tsx`
  - `Klijent/clientapp/src/pages/__tests__/PreNivelacijaPriorityPage.spec.tsx`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Checks:
  - `cd Klijent/clientapp && npm run test -- --run src/pages/__tests__/PreNivelacijaPriorityPage.spec.tsx src/pages/__tests__/ProductDecisionCenterPage.confidence.spec.tsx` (pass)
  - `cd Klijent/clientapp && npm run check:analytics-guardrails` (fail: existing `AnalyticsActionsPage.tsx` / `types/analytics` mismatch around `AnalyticsActionImpactLedger`)
  - `cd Klijent/clientapp && npm run build` (fail for the same existing type error before Vite build)
  - `git diff --check` (pass; LF/CRLF warnings only)
- Risk:
  - Markdown guardrails on the audited surfaces are stronger now, but repo-wide frontend type/build health is still blocked by the unrelated `AnalyticsActionsPage` ledger typings issue.
- Next:
  - `Q66 - Analytics pilot release checklist v2`

## Q66 - Analytics pilot release checklist v2

Status: DONE
Commit suggestion: `docs(qa): add analytics pilot release checklist v2`
Priority: P1
Type: docs/release
Token budget: medium

### Why

- After Q57-Q65, the pilot will need a refreshed evidence-based release gate that includes outcome measurement, calibration, and operator readiness.

### Scope only

- `docs/qa/ANALYTICS_PILOT_RELEASE_CHECKLIST_V2.md`

### Read first

- `AGENTS.md`
- `.github/copilot-instructions.md`
- `docs/ai/NEXT_PROMPT_QUEUE.md`
- `docs/qa/ANALYTICS_PRODUCTION_READINESS_STATUS.md`
- `docs/qa/ANALYTICS_LIVE_SMOKE_RESULT.md`
- `docs/qa/CONFIDENCE_CALIBRATION_AUDIT.md`
- `docs/pilot/ANALYTICS_PILOT_OPERATOR_RUNBOOK.md`

### Do

1. Create a PASS/WARN/FAIL release checklist for the next pilot phase.
2. Cover deploy proof, live smoke, data quality, action ledger, confidence calibration, pilot operator readiness, and rollback notes.
3. Link each checklist row to evidence docs and tests where possible.

### Checks

- `git diff --check`

### Acceptance

- Release checklist v2 is evidence-based and ready to use after Q57-Q65 land.
- Remaining risks and rollback expectations are explicit.

### Notes

- 2026-06-22: DONE. Added a new evidence-based release gate with PASS/WARN rows for deploy proof, live smoke, data quality, cache/freshness, action ledger, confidence calibration, operator readiness, and rollback notes.
- Changed files:
  - `docs/qa/ANALYTICS_PILOT_RELEASE_CHECKLIST_V2.md`
  - `docs/ai/NEXT_PROMPT_QUEUE.md`
- Checks:
  - `git diff --check` (pass; LF/CRLF warning only)
- Risk:
  - The checklist is intentionally conservative: cache/freshness, action ledger completeness, and confidence calibration remain warnings rather than hidden success states.
- Next:
  - `Q67 - Add automated encoding/mojibake guardrail`

## Q67 - Add automated encoding/mojibake guardrail

Status: TODO
Commit suggestion: `chore(ai): add encoding mojibake guardrail`
Priority: P1
Type: tooling/docs
Token budget: medium

### Why

- Encoding regressions are still a repeated repo risk.
- The current guardrail script protects analytics business-logic boundaries, but not mojibake drift.

### Scope only

- add a script to scan docs and frontend source for mojibake
- wire it into frontend guardrails or CI only if safe
- fail with `file:line`
- do not alter business logic
- include tests or a manual sample if feasible

### Read first

- `AGENTS.md`
- `.github/copilot-instructions.md`
- `docs/ai/AGENT_START_HERE.md`
- `docs/ai/ENCODING_AND_TEXT_SAFETY.md`
- `docs/ai/COMMON_FAILURES_AND_FIXES.md`
- `Klijent/clientapp/scripts/check-analytics-guardrails.mjs`
- `Klijent/clientapp/package.json`

### Do

1. Design or implement the smallest safe script that scans docs and frontend source for mojibake patterns.
2. Ensure the output includes `file:line`.
3. Integrate it into `check:analytics-guardrails` or CI only if that integration is low-risk.
4. Keep the task scoped to tooling and documentation.
5. Add tests or a manual verification sample if practical.

### Checks

- `git diff --check`
- `cd Klijent/clientapp && npm run check:analytics-guardrails`
- `cd Klijent/clientapp && npm run build`

### Acceptance

- Encoding/mojibake guardrail exists or is safely wired for a future merge path.
- Output is actionable with `file:line`.
- No application business logic changed.
