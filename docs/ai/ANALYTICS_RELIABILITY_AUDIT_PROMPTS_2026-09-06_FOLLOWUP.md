# Analytics Reliability Audit Follow-up Prompts

Date: 2026-09-06
Repo: `ivanjovicic/Trendplus`
Queue state: new prompts are `WAITING`; canonical queue was not edited because `RQ164` is currently `IN_PROGRESS` with an active local lock.

## Audit basis

This follow-up rechecked the previous audit in `ANALYTICS_RELIABILITY_AUDIT_PROMPTS_2026-09-06.md`, the current queue through `RQ228`, the relevant frontend tests and recent history for supplier analytics. The four previous findings (`RQ229`-`RQ232`) were not duplicated.

The audit also inspected the following already-covered or non-promoted areas:

- Dashboard aggregate numeric and period state remains covered by `RQ229` and `RQ230`.
- Supplier period validation remains covered by `RQ231`.
- Supplier footwear top-eight denominator remains covered by `RQ232`.
- Color sales uses `null` for missing article-level coverage. Shoe-type sales has an internal `coveragePct` fallback to `0`, but the field has no rendered consumer in the current page; it is recorded as a latent contract inconsistency, not promoted as a separate user-visible bug.
- Data Quality, Actions, Decision Board, inventory and pre/post surfaces were checked against existing queue owners (`RQ141`, `RQ143`, `RQ145`, `RQ146`, `RQ180`, `RQ181`, `RQ182`, `RQ198`-`RQ208`) and no independent duplicate prompt was created.

## RQ233 - Preserve supplier concentration denominator scope

Status: WAITING
Priority: P1
Type: frontend/contract/tests
Feature family: supplier-concentration-scope-parity
Parallel-safe: no
Owner: Supplier Analytics
Commit suggestion: `fix(analytics): preserve supplier concentration scope`

### Problem

`SupplierSalesStatsPage` filters the visible supplier rows by `supplierId` and `includeUnknown`, but the `Udeo top 5 dobavljača` KPI keeps `data.totals.ukupanPromet` from the unfiltered response as its denominator. With a focused supplier deep-link, the numerator can be one supplier while the label and denominator still describe all suppliers. The concentration chart, table and report can consequently describe different populations.

### Evidence

- `Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx:820-826` derives `visibleSuppliers` from `activeSupplierId` and `includeUnknown`.
- `Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx:873-887` derives `knownSuppliers` from those visible rows but calculates `top5SharePct` against `data.totals.ukupanPromet`.
- `Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx:1574-1577` labels the value as the share of total revenue from the top five suppliers, without exposing that the numerator may have been locally narrowed.
- `Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx:899-917` builds concentration data from the filtered `knownSuppliers` population.
- Existing `SupplierSalesStatsPage.premium.spec.tsx` covers trust, empty and error states, but has no focused-supplier concentration denominator regression.

### Scope

- Supplier overview page and its embedded `/analytics/supplier` overview tab.
- Supplier concentration KPI, concentration chart, table/detail context and report/export adapters that consume the same metric.
- Existing supplier analytics tests and the backend response contract if a backend-owned aggregate is required.

Do not redo the general supplier period validation from `RQ231`, the general parity program from `RQ145`, or the supplier-footwear top-eight issue from `RQ232`.

### Read first

- `AGENTS.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `RQ141`, `RQ143`, `RQ145`, `RQ198`, `RQ199`, `RQ231`, `RQ232` and their evidence
- `Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx`
- `Klijent/clientapp/src/services/supplierSalesStatsApi.ts`
- `Klijent/clientapp/src/pages/__tests__/SupplierSalesStatsPage.premium.spec.tsx`

### Do

1. Define one explicit population for concentration: either the backend-returned aggregate scope or the currently selected visible scope. Do not mix a filtered numerator with an unfiltered denominator.
2. Prefer a backend-owned aggregate and scope metadata when the metric is a decision KPI; if a frontend projection remains necessary, make the scope explicit and use the same normalized rows everywhere.
3. Keep unknown suppliers excluded or included consistently, and distinguish a valid zero denominator from unavailable or insufficient evidence.
4. Align the KPI label, tooltip, chart, table/detail context, export and report with the selected scope. A focused supplier must not be described as an aggregate top-five ranking.
5. Preserve backend recommendation ownership; this task must not recreate recommendation status, score or confidence.

### Tests

- all suppliers with five or more known suppliers;
- one focused supplier with an unfiltered totals payload;
- `includeUnknown=false`, unknown-only and empty visible populations;
- valid zero revenue versus missing/null/non-finite denominator;
- exact KPI/chart/table/detail/export/report parity and scope labels;
- regression proving a focused supplier cannot produce a misleading “top 5” aggregate value.

### Acceptance

- The numerator, denominator, label, tooltip and scope metadata always describe the same supplier population.
- A valid zero remains `0`; missing, unknown, insufficient, `NaN` and `Infinity` remain unavailable and never become a valid percentage.
- The same value and scope state are used by card, chart, table, detail, export and report.
- No recommendation or confidence is displayed or enabled merely because a narrowed denominator exists.
- Focused and unfiltered supplier tests fail before the fix and pass after it.

### Dependencies

- Consumes the existing supplier response and recommendation contract.
- Must coordinate with `RQ145` for the broader parity fixture, but is independently reproducible in the supplier overview.
- Do not claim live refresh, deployed runtime or full route parity from local component tests alone.

## RQ234 - Preserve all supplier decision filters in report deep-links

Status: WAITING
Priority: P1
Type: frontend/backend/contract/tests
Feature family: supplier-report-filter-fidelity
Parallel-safe: no
Owner: Supplier Analytics Reports
Commit suggestion: `fix(analytics): preserve supplier report filter context`

### Problem

The supplier Decision Hub report link is built from a reduced filter set. The page has active season, minimum-revenue, high-confidence and other decision filters, but the durable report URL preserves only period, data scope, supplier and store. The backend stable report URL builder has the same omission. Opening or exporting the report can therefore show a different dataset from the Decision Hub that generated the link while appearing to be the same report context.

### Evidence

- `Klijent/clientapp/src/pages/SupplierDecisionHubPage.tsx:688-703` serializes only `fromDate`, `toDate`, `scope`, `supplierId` and `storeId`.
- `Klijent/clientapp/src/pages/SupplierDecisionHubPage.tsx:611-621` exposes active `seasonId`, `minRevenue` and `onlyHighConfidence` filters that are absent from the durable report URL.
- `Api/Endpoints/SupplierDecisionHubEndpoints.cs:967-986` builds the backend stable report URL with only period, scope, supplier and store.
- `Api/Endpoints/SupplierDecisionHubEndpoints.cs:44-54` and `:449-460` show that the report-capable endpoints accept additional decision filters, so omission changes the requested scope rather than merely presentation.
- `SupplierDecisionReportPage.tsx` forwards the filters it receives, but cannot recover values that were never serialized into the link.

### Scope

- Supplier Decision Hub report/deep-link generation.
- Supplier durable report endpoint/query DTO and report page filter parsing.
- Printable/exported report metadata and focused regression tests.

Do not redesign report presentation or redo the full card/chart/detail parity program from `RQ145`; this prompt is specifically about preserving the active decision filter contract.

### Read first

- `AGENTS.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `RQ141`, `RQ145`, `RQ146`, `RQ198`, `RQ199` and their evidence
- `Klijent/clientapp/src/pages/SupplierDecisionHubPage.tsx`
- `Klijent/clientapp/src/pages/SupplierDecisionReportPage.tsx`
- `Api/Endpoints/SupplierDecisionHubEndpoints.cs`
- existing supplier report tests

### Do

1. Enumerate every active filter that affects the Decision Hub query and classify whether it must be serialized, intentionally excluded, or represented by a canonical scope token.
2. Make the Hub link, backend stable URL, report API request, printable report and export use one canonical filter contract.
3. Preserve nullable/empty filters distinctly from defaults; never silently broaden a filtered report.
4. Show the effective report period, scope and all material filters in report metadata so a user can verify context.
5. Return a clear validation/error state for malformed filter values rather than defaulting to a broader report.

### Tests

- season, minimum revenue, high-confidence, OOS, category, gender, supplier, store and data-scope filters individually and in combination;
- absent, empty, zero and malformed filter values;
- generated Hub URL round-trip into report page and API request;
- report/table/export parity for the same filter fixture;
- reversed period, unknown scope and endpoint 404/error responses;
- no raw backend codes or silent fallback to unfiltered data.

### Acceptance

- A report opened from the Decision Hub queries exactly the same period, scope and material filters as the originating view.
- The report and export visibly disclose effective filters and do not present broadened data as equivalent.
- Invalid filters fail closed with safe user-facing copy; valid zero and absent values retain their intended semantics.
- Existing compatibility links remain supported or redirect with an explicit, verified filter-preserving contract.
- Focused URL round-trip and report parity tests fail before the fix and pass after it.

### Dependencies

- Coordinates with `RQ141` and `RQ145`; it is a narrow report-link reproduction, not a replacement for their complete matrix/parity work.
- Requires the current supplier report endpoint contract and should not introduce a second filter owner.
- Live browser, refresh and deployed endpoint proof remain separate evidence requirements.
