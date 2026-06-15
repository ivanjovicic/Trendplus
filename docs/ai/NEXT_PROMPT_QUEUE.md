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

Status: TODO
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
