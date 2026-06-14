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

Status: TODO
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

---

## Q07 — Performance budgets

Status: TODO
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

---

## Q08 — Demo mode and demo dataset plan

Status: TODO
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

---

## Q09 — Tenant-safety checklist

Status: TODO
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
