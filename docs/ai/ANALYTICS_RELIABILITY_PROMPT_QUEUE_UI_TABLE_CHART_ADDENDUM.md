# Analytics Reliability Prompt Queue - UI/Table/Chart Addendum

Date: 2026-06-28
Repo: `ivanjovicic/Trendplus`
Current READY prompt: none in this addendum (next global: RQ51)
Main queue READY prompt: none (RQ01–RQ13 DONE)

Use this queue with `docs/ai/PROMPT_QUEUE_PROTOCOL.md`.

Purpose: add reliability prompts for analytics UI tables, charts, detail snapshots, reports and exports. These prompts remain WAITING until explicitly reprioritized.

## Status summary

| Task | Status | Feature family | Purpose |
|---|---|---|---|
| RQ39 | DONE | category-percent-units | Fix derived category ratio vs percent unit mismatch |
| RQ40 | DONE | supplier-decision-percent-export | Fix Supplier Decision percent display/export mismatch |
| RQ41 | DONE | xlsx-typed-cells | Write XLSX numeric/currency/percent/date cells as typed cells |
| RQ42 | DONE | detail-snapshot-formatting | Format detail snapshot values consistently with table values |
| RQ43 | DONE | stale-report-preview | Harden stale browser report preview fallback |
| RQ44 | DONE | change-badge-baseline | Stop showing zero/no-baseline changes as positive up signal |
| RQ45 | DONE | kpi-margin-coverage-ui | Show margin coverage on KPI margin card |
| RQ46 | WAITING | export-trust-metadata | Include trust metadata in exported analytics tables |
| RQ47 | WAITING | action-source-key-lineage | Include relevant filters in supplier action source keys |
| RQ48 | WAITING | action-duplicate-pagination | Avoid first-page-only duplicate guard for action queue |
| RQ49 | WAITING | reorder-value-field-drift | Fix legacy/derived totalReorderValue semantic drift |
| RQ50 | WAITING | chart-topn-semantics | Label top-N charts and rest/tail behavior explicitly |

---

## RQ39 - Derived category ratio vs percent units

Status: DONE
Ready after: RQ35/RQ36 or explicit reprioritization
Priority: P0
Type: frontend-contract/tests
Feature family: category-percent-units
Parallel-safe: no
Owner: Cursor-Composer
Local lock: `.ai/task-locks/RQ39-cursor.lock.md`
Commit suggestion: `fix(analytics): normalize category share percent units`

### Why

Derived category intelligence returns `revShare` as a ratio, while legacy category intelligence and UI formatters expect percent units. This can produce a 100x display/export error.

### Scope only

- `Klijent/clientapp/src/services/analyticsIntelligenceDerived.ts`
- `Klijent/clientapp/src/pages/InsightStudioPage.tsx`
- relevant frontend tests/types

### Do not touch

- backend SQL formulas
- unrelated category scoring logic
- chart styling except labels if needed

### Do

1. Add tests for derived category share where category A has 25% share.
2. Decide one canonical contract for `CategoryStat.revShare`:
   - percent units, or
   - ratio.
3. Normalize both legacy and derived paths to the same contract.
4. Verify table, chart, detail and export all show the same percent.

### Checks

- `git diff --check`
- frontend unit tests/typecheck if configured

### Acceptance

- Same data displays the same share regardless of legacy or derived source.
- No 100x percent mismatch remains.

### Notes

- 2026-08-05: DONE. Contract = percent units (`25` = `25%`) per hardening addendum. Derived path now multiplies ratio by 100 before assigning `CategoryStat.revShare`.
- Changed files:
  - `Klijent/clientapp/src/services/analyticsIntelligenceDerived.ts`
  - `Klijent/clientapp/src/services/insightStudioApi.ts` (JSDoc on revShare)
  - `Klijent/clientapp/src/services/__tests__/analyticsIntelligenceDerived.spec.ts`
  - `docs/qa/ANALYTICS_UI_TABLE_CHART_RELIABILITY_AUDIT.md`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_UI_TABLE_CHART_ADDENDUM.md`
- Checks:
  - `npm run test -- --run src/services/__tests__/analyticsIntelligenceDerived.spec.ts` - pass (4)
  - `git diff --check` - pass (scoped)
- Risk:
  - InsightStudioPage still formats with `fmtPct`; no page change needed once DTO units match.
- Next:
  - `RQ40 - Supplier Decision percent export/detail mismatch`

---

## RQ40 - Supplier Decision percent export/detail mismatch

Status: DONE
Ready after: RQ01 or explicit reprioritization
Priority: P0
Type: frontend/report/tests
Feature family: supplier-decision-percent-export
Parallel-safe: no
Owner: Cursor-Composer
Local lock: `.ai/task-locks/RQ40-cursor.lock.md`
Commit suggestion: `fix(analytics): align supplier decision percent export values`

### Why

Supplier Decision UI multiplies some raw ratio fields for display, but export/detail payloads use the raw row value while marking the column as `percent`.

### Scope only

- `Klijent/clientapp/src/pages/SupplierDecisionHubPage.tsx`
- `Klijent/clientapp/src/services/analyticsTableState.ts` only if shared formatting helper is needed
- report/export tests if available

### Do not touch

- supplier decision backend formula
- materialized views
- action queue logic

### Do

1. Add fixture row with `preMarkdownMarginPct = 0.35`.
2. Verify visual table, detail snapshot and export payload all represent `35%` consistently.
3. Use `getValue` or normalized export view model for percent columns.
4. Document whether percent table values are raw ratios or percent units.

### Checks

- `git diff --check`
- frontend tests/typecheck if configured

### Acceptance

- Visual UI and exported/report values do not disagree on percent units.

### Notes

- 2026-08-05: DONE. API `preMarkdownMarginPct` stays 0–1 ratio; export/detail/table percent columns use percent units via `getValue` / `toSupplierDecisionMarginPercentUnits`. Detail snapshot formats percent with `fmtPct`.
- Changed files:
  - `Klijent/clientapp/src/pages/SupplierDecisionHubPage.tsx`
  - `Klijent/clientapp/src/services/analyticsTableState.ts`
  - `Klijent/clientapp/src/pages/__tests__/SupplierDecisionHubPage.percentExport.spec.ts`
  - `Klijent/clientapp/src/services/__tests__/analyticsTableState.spec.ts`
  - `docs/qa/ANALYTICS_UI_TABLE_CHART_RELIABILITY_AUDIT.md`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_UI_TABLE_CHART_ADDENDUM.md`
- Checks:
  - `npm run test -- --run ...percentExport.spec.ts ...analyticsTableState.spec.ts` - pass (9)
  - `git diff --check` - pass (scoped)
- Risk:
  - XLSX still stores values as strings; numeric typing is RQ41. Export number is `35` not `0.35`.
- Next:
  - `RQ41 - Typed XLSX cells for analytics exports`

---

## RQ41 - Typed XLSX cells for analytics exports

Status: DONE
Ready after: RQ40 or explicit reprioritization
Priority: P1
Type: backend-export/tests
Feature family: xlsx-typed-cells
Parallel-safe: no
Owner: Cursor-Composer
Local lock: `.ai/task-locks/RQ41-cursor.lock.md`
Commit suggestion: `fix(exports): write typed analytics xlsx cells`

### Why

The XLSX renderer currently writes every cell as an inline string. Exported spreadsheets are not reliable for numeric sorting, formulas, pivots or totals.

### Scope only

- `Infrastructure/Services/Documents/DocumentRenderer.cs`
- document/export tests
- optional document model helper

### Do not touch

- PDF renderer
- analytics formulas
- frontend report layout

### Do

1. Add tests for numeric, currency, percent and date columns.
2. Write numeric cells as numeric cells, not inline strings.
3. Apply styles/numFmtId for currency/percent/date where feasible.
4. Preserve CSV behavior unless separately scoped.

### Checks

- `git diff --check`
- targeted document renderer tests

### Acceptance

- Excel recognizes exported numeric columns as numbers.

### Notes

- 2026-08-05: DONE. Typed XLSX cells for number/currency/percent/date via `DocumentColumnDefinition.DataType`; percent format is percent-units (`0.00"%"`). Text/unparseable stay `inlineStr`. CSV unchanged.
- Changed files:
  - `Infrastructure/Services/Documents/DocumentRenderer.cs`
  - `Api.Tests/DocumentRendererTests.cs`
  - `docs/qa/ANALYTICS_UI_TABLE_CHART_RELIABILITY_AUDIT.md`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_UI_TABLE_CHART_ADDENDUM.md`
- Checks:
  - `dotnet test ... --filter DocumentRendererTests` - pass (12)
  - `git diff --check` - pass (scoped)
- Risk:
  - Callers that still export percent as ratio (0.35) will get typed 0.35 with literal-% format (shows 0.35%); RQ40 paths send percent units.
- Next:
  - `RQ42 - Detail snapshot formatting parity`

---

## RQ42 - Detail snapshot formatting parity

Status: DONE
Ready after: RQ39/RQ40 or explicit unblocking
Priority: P1
Type: frontend/tests
Feature family: detail-snapshot-formatting
Parallel-safe: no
Owner: Cursor-Composer
Local lock: `.ai/task-locks/RQ42-cursor.lock.md`
Commit suggestion: `fix(analytics): format detail snapshot values consistently`

### Why

Detail snapshot currently stringifies raw values. It can show different units/format from the visible table.

### Scope only

- `Klijent/clientapp/src/services/analyticsTableState.ts`
- formatter utilities if needed
- detail snapshot tests

### Do not touch

- backend report generator
- chart components

### Do

1. Add tests for currency, percent, number, date and boolean detail fields.
2. Reuse shared formatter behavior or store both raw and display values.
3. Ensure percent unit contract is not guessed incorrectly.

### Checks

- `git diff --check`
- frontend tests/typecheck if configured

### Acceptance

- Detail view matches table display for the same row/column.

### Notes

- 2026-08-05: DONE. Shared `formatDetailFieldValue` uses `fmtRsd`/`fmtPct`/`fmtNumber`/`formatDate`/`formatDateTime`; percent stays percent-units (no silent `*100`).
- Changed files:
  - `Klijent/clientapp/src/services/analyticsTableState.ts`
  - `Klijent/clientapp/src/services/__tests__/analyticsTableState.spec.ts`
  - `Klijent/clientapp/src/pages/ColorSalesStatsPage.spec.tsx` (expect formatted RSD)
  - `docs/qa/ANALYTICS_UI_TABLE_CHART_RELIABILITY_AUDIT.md`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_UI_TABLE_CHART_ADDENDUM.md`
- Checks:
  - `npm run test -- --run src/services/__tests__/analyticsTableState.spec.ts` - pass (7)
  - `npm run test -- --run ...ColorSalesStatsPage.spec.tsx -t "expands a color row"` - pass
  - `git diff --check` - pass (scoped)
- Risk:
  - Pages that previously asserted raw numeric strings in detail snapshots need updated expectations.
- Next:
  - `RQ43 - Stale browser report preview hardening`

---

## RQ43 - Stale browser report preview hardening

Status: DONE
Ready after: RQ40/RQ42 or explicit unblocking
Priority: P1
Type: frontend/report/tests
Feature family: stale-report-preview
Parallel-safe: no
Owner: Cursor-Composer
Local lock: `.ai/task-locks/RQ43-cursor.lock.md`
Commit suggestion: `fix(analytics): watermark stale report previews`

### Why

Supplier Decision report can fall back to a local browser preview when durable backend report fails. A warning exists, but the report can still be read/exported as if current.

### Scope only

- `Klijent/clientapp/src/pages/SupplierDecisionReportPage.tsx`
- report action component if disabling export/print is needed
- tests if available

### Do not touch

- backend durable report endpoint
- document storage

### Do

1. Add visible watermark/badge for local preview mode.
2. Include savedAt timestamp and TTL in the report header.
3. Decide whether export/print should be disabled or force-confirmed in stale preview mode.
4. Add tests for backend-fail + local-preview scenario.

### Checks

- `git diff --check`
- frontend tests/typecheck if configured

### Acceptance

- Stale/local report cannot be mistaken for backend-verified current data.

### Notes

- 2026-08-05: DONE. Local preview watermark + savedAt/TTL meta; export/print disabled for browser snapshot. Decision: disable (not confirm) unsafe export from stale preview.
- Changed files:
  - `Klijent/clientapp/src/pages/SupplierDecisionReportPage.tsx`
  - `Klijent/clientapp/src/pages/SupplierDecisionReportPage.css`
  - `Klijent/clientapp/src/services/analyticsTableState.ts` (`getPrintPayloadSnapshot`, `ANALYTICS_PRINT_TTL_MS`)
  - `Klijent/clientapp/src/pages/__tests__/SupplierDecisionReportPage.spec.tsx`
  - `Klijent/clientapp/src/services/__tests__/analyticsTableState.spec.ts`
  - `docs/qa/ANALYTICS_UI_TABLE_CHART_RELIABILITY_AUDIT.md`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_UI_TABLE_CHART_ADDENDUM.md`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_CROSS_SURFACE_ADDENDUM.md` (next READY RQ51)
- Checks:
  - `npm run test -- --run SupplierDecisionReportPage.spec.tsx analyticsTableState.spec.ts` - pass (12)
  - `git diff --check` - pass (scoped)
- Risk:
  - Users must open durable report for export; local preview is view-only by design.
- Next:
  - `RQ51 - Color insufficient_data status mapping` (priority review after RQ40/RQ43 lane)

---

## RQ44 - Change badge baseline semantics

Status: DONE
Ready after: RQ20/RQ31 or explicit unblocking
Priority: P1
Type: frontend/tests
Feature family: change-badge-baseline
Parallel-safe: no
Owner: Codex
Local lock: `.ai/task-locks/RQ44-codex.lock.md`
Commit suggestion: `fix(analytics): distinguish neutral and no-baseline changes`

### Why

`changeBadge` treats `change >= 0` as up/positive. Zero/no-baseline values should not always be positive.

### Scope only

- `Klijent/clientapp/src/pages/InsightStudioPage.tsx`
- shared formatter/helper if useful
- tests

### Do not touch

- backend zero-baseline formula unless doing RQ20/RQ31
- unrelated badges

### Do

1. Add tests for positive, negative, zero and no-baseline/null changes.
2. Render zero as neutral, not up.
3. Render no-baseline as `N/A`, `novo`, or explicit baseline badge.

### Checks

- `git diff --check`
- frontend tests/typecheck if configured

### Acceptance

- UI does not signal improvement when baseline is missing or change is neutral.

### Notes

- 2026-08-06: DONE. `changeBadge` now treats positive, negative, zero and null/unknown changes as distinct states; zero renders neutral, null/unknown renders `N/A`.
- Changed files:
  - `Klijent/clientapp/src/pages/InsightStudioPage.tsx`
  - `Klijent/clientapp/src/pages/__tests__/InsightStudioPage.spec.tsx`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_UI_TABLE_CHART_ADDENDUM.md`
- Checks:
  - `git diff --check` - pass
  - `npm run test -- --run src/pages/__tests__/InsightStudioPage.spec.tsx` - pass
  - `npm run check:analytics-guardrails` - pass
  - `npm run build` - pass
- Risk:
  - Current changelog payload still collapses missing baseline into numeric `0`, so the frontend cannot prove a true no-baseline signal until backend emits an explicit nullable/flagged field.
- Next:
  - `RQ45 - KPI margin coverage UI`

---

## RQ45 - KPI margin coverage UI

Status: DONE
Ready after: RQ34/RQ36 or explicit unblocking
Priority: P1
Type: frontend-contract/tests
Feature family: kpi-margin-coverage-ui
Parallel-safe: no
Owner: Codex
Local lock: `.ai/task-locks/RQ45-codex.lock.md`
Commit suggestion: `fix(analytics): show kpi margin coverage`

### Why

KPI Snapshot backend supplies margin coverage, but frontend type/card hides it. Margin can look fully trusted even with low cost coverage.

### Scope only

- `Klijent/clientapp/src/services/insightStudioApi.ts`
- `Klijent/clientapp/src/pages/InsightStudioPage.tsx`
- UI tests if available

### Do not touch

- backend margin formula
- Supplier Decision Hub

### Do

1. Expose `marginDataCoveragePct` and `revenueWithCost` in frontend type.
2. Show coverage in KPI tooltip/subtext or trust badge.
3. If coverage is low, show warning or mark estimated.

### Checks

- `git diff --check`
- frontend tests/typecheck if configured

### Acceptance

- Users can see when displayed margin is low-coverage/estimated.

### Notes

- 2026-08-06: DONE. KPI margin card now shows margin coverage directly in the subtext and tooltip, and low coverage is marked as estimated.
- Changed files:
  - `Klijent/clientapp/src/services/insightStudioApi.ts`
  - `Klijent/clientapp/src/pages/InsightStudioPage.tsx`
  - `Klijent/clientapp/src/pages/__tests__/InsightStudioPage.spec.tsx`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_UI_TABLE_CHART_ADDENDUM.md`
  - `docs/qa/ANALYTICS_UI_TABLE_CHART_RELIABILITY_AUDIT.md`
- Checks:
  - `git diff --check` - pass
  - `npm run test -- --run src/pages/__tests__/InsightStudioPage.spec.tsx` - pass
  - `npm run check:analytics-guardrails` - pass
  - `npm run build` - pass
- Risk:
  - Coverage is still a frontend trust signal layered on top of backend numbers; no backend formula was changed.
- Next:
  - `RQ46 - export trust metadata`

---

## RQ46 - Export trust metadata preservation

Status: WAITING
Ready after: RQ34/RQ45 or explicit unblocking
Priority: P1
Type: frontend-report/tests
Feature family: export-trust-metadata
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ46-<agent>.lock.md`
Commit suggestion: `fix(analytics): preserve trust metadata in exports`

### Why

Analytics exports are built from visible column definitions. Hidden trust fields can be dropped from exported reports.

### Scope only

- `Klijent/clientapp/src/pages/InsightStudioPage.tsx`
- `Klijent/clientapp/src/components/analytics/AnalyticsTableToolbar.tsx` only if hidden/export-only columns are supported
- export tests if available

### Do not touch

- backend formulas
- document renderer formatting unless doing RQ41

### Do

1. Identify tables where trust fields exist but are omitted from columns.
2. Add visible or export-only trust columns for coverage/source/status.
3. Ensure exported report includes enough metadata to assess reliability.

### Checks

- `git diff --check`
- frontend tests/typecheck if configured

### Acceptance

- Exported analytics tables retain source/coverage/quality context.

---

## RQ47 - Supplier action source key filter lineage

Status: WAITING
Ready after: RQ01 or explicit reprioritization
Priority: P1
Type: frontend/backend-contract/tests
Feature family: action-source-key-lineage
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ47-<agent>.lock.md`
Commit suggestion: `fix(analytics): include scorecard filters in action source keys`

### Why

Supplier action source keys omit season/minRevenue/onlyHighConfidence even though those filters affect the visible scorecard context.

### Scope only

- `Klijent/clientapp/src/pages/SupplierDecisionHubPage.tsx`
- action source key tests if available
- backend action idempotency docs only if needed

### Do not touch

- analytics action schema
- recommendation formulas

### Do

1. Add test/fixture for same supplier/date/store/dataScope but different season/minRevenue/high-confidence filter.
2. Decide which filters are identity-defining vs display-only.
3. Include identity-defining filters in source key or store them in metadata while avoiding unwanted duplicates.

### Checks

- `git diff --check`
- frontend tests/typecheck if configured

### Acceptance

- Action lineage accurately identifies the scorecard context that created it.

---

## RQ48 - Supplier action duplicate guard pagination

Status: WAITING
Ready after: RQ47 or explicit unblocking
Priority: P2
Type: frontend/API-contract/tests
Feature family: action-duplicate-pagination
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ48-<agent>.lock.md`
Commit suggestion: `fix(analytics): avoid paginated action duplicate blind spot`

### Why

Supplier Decision UI duplicate guard fetches only page 1 with pageSize 200 per open status. More actions can exist beyond that page.

### Scope only

- `Klijent/clientapp/src/pages/SupplierDecisionHubPage.tsx`
- analytics action API usage
- tests if available

### Do not touch

- backend action upsert unless necessary
- action outcome metrics

### Do

1. Add helper that pages until all open action keys are loaded, or add a backend endpoint for source-key lookup.
2. Preserve performance guardrails.
3. Keep backend idempotency as final protection.

### Checks

- `git diff --check`
- frontend tests/typecheck if configured

### Acceptance

- Existing open actions beyond page 1 are not missed by the UI guard.

---

## RQ49 - Reorder value field semantic drift

Status: WAITING
Ready after: RQ33/RQ38 or explicit unblocking
Priority: P1
Type: frontend-contract/tests
Feature family: reorder-value-field-drift
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ49-<agent>.lock.md`
Commit suggestion: `fix(analytics): separate reorder cost and revenue fields`

### Why

Legacy reorder `totalReorderValue` historically means selling-price potential value, while derived smart reorder fallback maps `totalReorderCost` into the same field name.

### Scope only

- `Klijent/clientapp/src/services/analyticsIntelligenceDerived.ts`
- `Klijent/clientapp/src/services/insightStudioApi.ts`
- `Klijent/clientapp/src/pages/InsightStudioPage.tsx`

### Do not touch

- backend V2 smart reorder unless doing RQ17
- inventory signal producers

### Do

1. Add tests for legacy backend reorder vs derived smart reorder fallback.
2. Split fields into cost and revenue names.
3. Update UI labels and exports.

### Checks

- `git diff --check`
- frontend tests/typecheck if configured

### Acceptance

- Same field name no longer means cost in one path and revenue in another.

---

## RQ50 - Chart top-N semantics

Status: WAITING
Ready after: higher-priority data fixes
Priority: P2
Type: frontend-ux/tests
Feature family: chart-topn-semantics
Parallel-safe: yes
Owner: unassigned
Local lock: `.ai/task-locks/RQ50-<agent>.lock.md`
Commit suggestion: `fix(analytics): label chart top n semantics`

### Why

Several charts show top 8/top N rows while adjacent tables may contain all rows. Users can misread chart totals as whole-dataset totals.

### Scope only

- chart labels/subtitles in `InsightStudioPage.tsx` and `SupplierDecisionHubPage.tsx`
- no formula changes

### Do not touch

- backend endpoints
- table/export columns

### Do

1. Inventory every `.slice(0, N)` chart/table snippet.
2. Add subtitle like “Top 8 po prihodu; tabela ispod sadrži sve redove”.
3. Add `Ostali` bucket when the chart represents shares and a rest bucket is appropriate.

### Checks

- `git diff --check`
- frontend tests/typecheck if configured

### Acceptance

- Top-N charts cannot be mistaken for full totals.
