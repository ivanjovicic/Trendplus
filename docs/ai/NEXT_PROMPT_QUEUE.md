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
- Remaining risk:
  - `?? 0` / `|| 0` patterns still exist in many analytics surfaces, but the audited ones were either intentional derived defaults or already protected by meta/error states
  - follow-up visibility for ancillary filter refresh failures is still useful even though the fake-empty behavior is now reduced

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

Status: TODO

Next step:
- make correlation IDs visible across backend logs, live smoke docs, and frontend error states so partial failures are easier to trace

## Q41 - Action Impact Ledger Phase 1 design-to-implementation gap review

Status: TODO

Next step:
- compare the current action model with the ledger plan and document the smallest safe Phase 1 implementation gap

## Q42 - Product Decision confidence calibration review

Status: TODO

Next step:
- review Product Decision confidence mapping, calibration buckets, and missing-impact behavior against the contract

## Q43 - Supplier confidence contract mapping

Status: TODO

Next step:
- map supplier summary, list, and report confidence semantics onto the shared contract without inventing new values in the UI

## Q44 - Inventory decision confidence mapping

Status: TODO

Next step:
- align inventory recommendation confidence, warnings, and nullable impact behavior with the shared decision contract

## Q45 - Decision Board backend aggregate readiness review

Status: TODO

Next step:
- verify the phase 1 board model is stable enough to justify a backend aggregate endpoint design review
