# Analytics Reliability Prompt Queue - Cross-Surface Addendum

Date: 2026-06-28
Repo: `ivanjovicic/Trendplus`
Current READY prompt: none
Main queue READY prompt: none (RQ01–RQ13 DONE; owner pack RQ100-RQ105 DONE)

Use this queue with `docs/ai/PROMPT_QUEUE_PROTOCOL.md`.

Purpose: add reliability prompts for cross-surface analytics inconsistencies: supplier/shoe/color stats, vendor pre/post nivelacija, inventory sorting/export/action lineage and freshness semantics. These prompts remain WAITING until explicitly reprioritized.

## Status summary

| Task | Status | Feature family | Purpose |
|---|---|---|---|
| RQ51 | DONE | color-insufficient-data-status | Stop mapping color insufficient_data to Zadrzi |
| RQ52 | DONE | color-local-recommendation-fallback | Remove or label Color frontend recommendation fallback |
| RQ53 | DONE | color-shoetype-datascope-lineage | Pass/verify dataScope in Color and ShoeType pages |
| RQ54 | DONE | vendor-nivelacija-scope-lineage | Add/verify dataScope/store lineage on Vendor pre/post page |
| RQ55 | WAITING | supplier-hidden-unknown-denominators | Clarify denominators when unknown suppliers are hidden |
| RQ56 | WAITING | total-cost-fallback-guardrail | Do not clamp inconsistent implied cost to fake zero |
| RQ57 | DONE | inventory-risk-global-sort | Make inventory OOS/overstock sort global or clearly page-local |
| RQ58 | DONE | inventory-screen-csv-order | Make CSV ekran match displayed risk-sorted rows |
| RQ59 | DONE | inventory-signal-review-impact | Do not attach confirmed impact to weak signal-check actions |
| RQ60 | DONE | inventory-fake-zero-value | Preserve unknown inventory value when cost is missing |
| RQ61 | DONE | inventory-freshness-lineage | Separate inventory panel freshness timestamps |
| RQ62 | DONE | vendor-previous-comparison-failure | Warn when previous-period request fails |
| RQ63 | WAITING | vendor-change-share-naming | Rename/clarify top5 share of absolute change |
| RQ105 | DONE | analytics-operational-fallback-honesty | Daily sales and dashboard inventory operational fallback must stay visible |
| RQ125 | DONE | stats-trust-meta-freshness | Add backend-owned trust/freshness metadata to supplier/shoe/color stats pages |
| RQ126 | DONE | daily-sales-trust-meta-contract | Add authoritative trust metadata to Daily Sales instead of placeholder trust header values |
| RQ127 | DONE | stats-margin-baseline-unavailable | Stop supplier/shoe/color recommendation inputs from treating missing known-margin baseline as `0` |

---

## RQ51 - Color insufficient_data must not become Zadrzi

Status: DONE
Ready after: RQ01 or explicit reprioritization
Priority: P0
Type: frontend-contract/tests
Feature family: color-insufficient-data-status
Parallel-safe: no
Owner: Cursor-Composer
Local lock: `.ai/task-locks/RQ51-cursor.lock.md` (removed after DONE)
Commit suggestion: `fix(analytics): preserve color insufficient data status`

### Why

Color analytics maps backend `insufficient_data` to local `Zadrzi`. A lack of evidence must not become a valid hold/maintain decision.

### Scope only

- `Klijent/clientapp/src/pages/ColorSalesStatsPage.tsx`
- `Klijent/clientapp/src/services/colorSalesStatsApi.ts` if type changes are needed
- frontend tests if available

### Do not touch

- backend recommendation formulas
- ShoeType/Supplier pages
- styling beyond status label/tone if needed

### Do

1. Add fixture with backend recommendation status `insufficient_data`.
2. Preserve a distinct UI status/label: `Nedovoljno podataka`.
3. Ensure sorting, counts, badge color and export/detail preserve that status.

### Checks

- `git diff --check`
- frontend tests/typecheck if configured

### Acceptance

- Backend `insufficient_data` is never displayed as `Zadrzi`.

### Completion note

- Date: 2026-08-05
- Agent: Cursor-Composer
- Changed: `ColorSalesStatsPage.tsx`, `ColorSalesStatsPage.spec.tsx`, this queue, cross-surface audit R51
- Fix: `mapRecommendationStatus("insufficient_data")` → `NedovoljnoPodataka` / label `Nedovoljno podataka` (tone `status-na`); counts/export/detail/InfoTip updated
- Checks: `npm run test -- --run src/pages/ColorSalesStatsPage.spec.tsx` pass (7); `git diff --check` pass
- Risk: local heuristic fallback when backend recommendation is missing remains (RQ52)
- Next: RQ52 READY

---

## RQ52 - Color frontend recommendation fallback

Status: DONE
Ready after: RQ51 DONE or explicit unblocking
Priority: P1
Type: frontend-contract/tests
Feature family: color-local-recommendation-fallback
Parallel-safe: no
Owner: Cursor-Composer
Local lock: `.ai/task-locks/RQ52-cursor.lock.md` (removed after DONE)
Commit suggestion: `fix(analytics): block color local recommendation fallback`

### Why

When backend recommendation is missing, Color page computes a local decision score and maps it to recommendation statuses. Missing backend recommendations should remain informational unless explicitly approved.

### Scope only

- `Klijent/clientapp/src/pages/ColorSalesStatsPage.tsx`
- frontend tests if available

### Do not touch

- backend recommendations
- color API formulas

### Do

1. Add test/fixture where `recommendation` is null.
2. Display row as informational/insufficient-data, or clearly label local heuristic as non-authoritative.
3. Do not include local fallback as a final recommendation in counts/export.

### Checks

- `git diff --check`
- frontend tests/typecheck if configured

### Acceptance

- Missing backend recommendation cannot silently become a frontend business recommendation.

### Completion note

- Date: 2026-08-05
- Agent: Cursor-Composer
- Changed: `ColorSalesStatsPage.tsx`, `ColorSalesStatsPage.spec.tsx`, this queue, cross-surface audit R52
- Fix: removed local decisionScore→Pojacaj/Zadrzi/Smanji heuristic; missing/unmapped backend recommendation → `NedovoljnoPodataka` with explicit reason; dead clamp/UNKNOWN_COLORS/buildStatusReason removed
- Checks: `npm run test -- --run src/pages/ColorSalesStatsPage.spec.tsx` pass (8); `git diff --check` pass
- Risk: rows without backend recommendation no longer appear in Pojačaj/Zadrži/Smanji counts (by design)
- Next: RQ53 READY

---

## RQ53 - Color/ShoeType dataScope lineage

Status: DONE
Ready after: RQ39/RQ51 or explicit reprioritization
Priority: P1
Type: frontend-contract/tests
Feature family: color-shoetype-datascope-lineage
Parallel-safe: no
Owner: Cursor-Composer
Local lock: `.ai/task-locks/RQ53-cursor.lock.md` (removed after DONE)
Commit suggestion: `fix(analytics): pass data scope to color and shoe type pages`

### Why

Color/ShoeType services support `dataScope`, and detail links include it, but the list page calls can omit it. List/detail/report surfaces can disagree.

### Scope only

- `Klijent/clientapp/src/pages/ColorSalesStatsPage.tsx`
- `Klijent/clientapp/src/pages/ShoeTypeSalesStatsPage.tsx`
- service tests if available

### Do not touch

- Supplier page dataScope logic
- backend endpoints unless a bug is found there

### Do

1. Add tests proving `dataScope` is included in list API calls.
2. Use the same `dataScope` in list, detail navigation, export metadata and trust header.
3. Verify changing global dataScope reloads the page data.

### Checks

- `git diff --check`
- frontend tests/typecheck if configured

### Acceptance

- Color/ShoeType list and detail always refer to the same data scope.

### Completion note

- Date: 2026-08-05
- Agent: Cursor-Composer
- Changed: `ColorSalesStatsPage.tsx`, `ShoeTypeSalesStatsPage.tsx`, specs, this queue, audit R53
- Fix: list APIs receive `dataScope`; same value in toolbar filters/metadata, detail URL/snapshot, ShoeType trust header; reload on `trendplus:data-scope-changed`
- Checks: Color (9) + ShoeType dataScope (1) tests pass; `git diff --check` pass
- Risk: backend must honor `dataScope` query (already supported by services)
- Next: RQ54 READY

---

## RQ54 - Vendor pre/post scope lineage

Status: DONE
Ready after: RQ53 or explicit unblocking
Priority: P1
Type: frontend-contract/tests
Feature family: vendor-nivelacija-scope-lineage
Parallel-safe: no
Owner: Cursor-Composer
Local lock: `.ai/task-locks/RQ54-cursor.lock.md` (removed after DONE)
Commit suggestion: `fix(analytics): add vendor nivelacija scope lineage`

### Why

Vendor pre/post API supports `storeId` and `dataScope`, but the page does not expose/pass them. This can make the page disagree with scoped analytics surfaces.

### Scope only

- `Klijent/clientapp/src/pages/ProdajaPrePostNivelacijePage.tsx`
- `Klijent/clientapp/src/services/vendorSalesNivelacijaApi.ts` only if contract adjustment is needed

### Do not touch

- vendor pre/post backend formulas
- materialized views

### Do

1. Decide whether page should inherit global `dataScope` and/or expose store filter.
2. Pass `dataScope` and `storeId` consistently to current and previous period requests.
3. Show filters in export/detail metadata.

### Checks

- `git diff --check`
- frontend tests/typecheck if configured

### Acceptance

- Vendor pre/post page declares and preserves its data scope.

### Completion note

- Date: 2026-08-05
- Agent: Cursor-Composer
- Changed: `ProdajaPrePostNivelacijePage.tsx`, `ProdajaPrePostNivelacijePage.spec.tsx`, this queue, audit R54
- Decision: inherit global `dataScope` (reload on change); expose Objekat store filter
- Fix: current+previous API calls pass `storeId`/`dataScope`; toolbar + trust header declare scope/store
- Checks: `npm run test -- --run src/pages/ProdajaPrePostNivelacijePage.spec.tsx` pass (2); `git diff --check` pass
- Risk: previous-period failure still silent (RQ62)
- Next: RQ62 READY (unblocked by RQ54); RQ55 remains WAITING (RQ34/RQ46)

---

## RQ55 - Supplier hidden-unknown denominator semantics

Status: WAITING
Ready after: RQ34/RQ46 or explicit unblocking
Priority: P1
Type: frontend-ux/tests
Feature family: supplier-hidden-unknown-denominators
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ55-<agent>.lock.md`
Commit suggestion: `fix(analytics): clarify supplier unknown denominator semantics`

### Why

When unknown suppliers are hidden, visible rows are known suppliers only, but denominators can still use full dataset revenue. This is mathematically defensible but visually ambiguous.

### Scope only

- `Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx`
- chart/table labels and metadata

### Do not touch

- backend supplier aggregation
- unknown supplier data quality page

### Do

1. Add test/fixture with unknown revenue and `includeUnknown=false`.
2. Decide chart denominator contract:
   - full revenue including hidden unknowns, with explicit note; or
   - visible-known revenue only, with separate hidden unknown warning.
3. Ensure top5/concentration/export metadata makes hidden unknown denominator explicit.

### Checks

- `git diff --check`
- frontend tests/typecheck if configured

### Acceptance

- Users can understand why visible shares may not sum to 100%.

---

## RQ56 - Total cost fallback guardrail

Status: WAITING
Ready after: RQ41/RQ42 or explicit unblocking
Priority: P2
Type: frontend-contract/tests
Feature family: total-cost-fallback-guardrail
Parallel-safe: yes
Owner: unassigned
Local lock: `.ai/task-locks/RQ56-<agent>.lock.md`
Commit suggestion: `fix(analytics): flag inconsistent implied cost fallback`

### Why

When backend `totalCost` is missing, UI uses `Math.max(0, revenueWithCost - marginContribution)`. If inputs are inconsistent, impossible negative implied cost becomes zero.

### Scope only

- `SupplierSalesStatsPage.tsx`
- `ShoeTypeSalesStatsPage.tsx`
- frontend tests if available

### Do not touch

- backend margin policy
- cost import pipeline

### Do

1. Add fixture where `revenueWithCost - marginContribution < 0` and backend `totalCost` is missing.
2. Return/display unknown or data-quality warning rather than zero.
3. Prefer backend `totalCost` and mark fallback as legacy compatibility only.

### Checks

- `git diff --check`
- frontend tests/typecheck if configured

### Acceptance

- Inconsistent implied cost is not hidden as zero.

---

## RQ57 - Inventory risk global sorting

Status: DONE
Ready after: RQ01 or explicit reprioritization
Priority: P0
Type: frontend/backend-contract/tests
Feature family: inventory-risk-global-sort
Parallel-safe: no
Owner: Cursor-Composer
Local lock: `.ai/task-locks/RQ57-cursor.lock.md` (removed after DONE)
Commit suggestion: `fix(inventory): make risk sort global or explicit`

### Why

Inventory OOS/overstock risk sorting is applied client-side only to the loaded page while server sorting falls back to quantity.

### Scope only

- `Klijent/clientapp/src/pages/InventoryPage.tsx`
- inventory API sort contract if server support is needed
- tests

### Do not touch

- forecast algorithm unless necessary
- unrelated inventory panels

### Do

1. Add fixture where a high-risk SKU is on page 2 under quantity sort.
2. Either implement server-side risk sort or label it as “sort current page only”.
3. Ensure filtered exports use the same sort semantics.

### Checks

- `git diff --check`
- frontend/backend targeted tests if available

### Acceptance

- Selecting OOS/overstock risk sort cannot hide higher-risk SKUs on later pages without warning.

### Completion note

- Date: 2026-08-05
- Agent: Cursor-Composer
- Decision: explicit page-local labeling (no server-side forecast risk sort; avoids forecast algorithm / backend rewrite)
- Changed: `InventoryPage.tsx`, `inventoryUtils.ts`, `InventoryPage.riskSortScope.spec.ts`, this queue, audit R57
- Fix: option labels “(samo trenutna strana)” + warning under sort control when multipage risk exists
- Checks: riskSortScope specs pass (4); `git diff --check` pass
- Risk: CSV ekran may still export unsorted `rows` (RQ58)
- Next: RQ58 READY

---

## RQ58 - Inventory screen CSV order parity

Status: DONE
Ready after: RQ57 or explicit unblocking
Priority: P1
Type: frontend-export/tests
Feature family: inventory-screen-csv-order
Parallel-safe: no
Owner: Cursor-Composer
Local lock: `.ai/task-locks/RQ58-cursor.lock.md` (removed after DONE)
Commit suggestion: `fix(inventory): export displayed row order for screen csv`

### Why

`CSV ekran` exports `rows`, while risk-sorted UI displays `displayedRows`. The export can disagree with the visible table order.

### Scope only

- `Klijent/clientapp/src/pages/InventoryPage.tsx`
- frontend tests if available

### Do not touch

- server export
- document renderer

### Do

1. Use `displayedRows` for screen CSV export.
2. Add sort-mode metadata to the filename/header if useful.
3. Add test for OOS risk sort export order.

### Checks

- `git diff --check`
- frontend tests/typecheck if configured

### Acceptance

- Screen CSV matches the order currently shown on screen.

### Completion note

- Date: 2026-08-05
- Agent: Cursor-Composer
- Changed: `InventoryPage.tsx`, `inventoryUtils.ts` (`buildInventoryScreenCsvLines` / `buildInventoryScreenCsvFilename`), `InventoryPage.screenCsvOrder.spec.ts`, this queue, audit R58
- Fix: `exportVisibleCsv` uses `displayedRows`; filename includes risk sort token; status note for page-local risk sorts
- Checks: `npm run test -- --run src/pages/__tests__/InventoryPage.screenCsvOrder.spec.ts` pass (2); `git diff --check` pass
- Risk: filtered server CSV still uses server sort (intentional; out of scope)
- Next: RQ59 READY

---

## RQ59 - Inventory signal-review impact trust

Status: DONE
Ready after: RQ57 or explicit reprioritization
Priority: P1
Type: frontend/action-contract/tests
Feature family: inventory-signal-review-impact
Parallel-safe: no
Owner: Cursor-Composer
Local lock: `.ai/task-locks/RQ59-cursor.lock.md` (removed after DONE)
Commit suggestion: `fix(inventory): avoid confirmed impact on signal review actions`

### Why

Weak/insufficient inventory signals create `SIGNAL_REVIEW` actions but can still attach expected impact. A review action should not look like confirmed financial impact.

### Scope only

- `Klijent/clientapp/src/pages/InventoryPage.tsx`
- analytics action tests/contracts if available

### Do not touch

- backend action outcome metrics unless needed
- inventory formulas

### Do

1. Add fixture with `recommendationAllowed=false` and positive inventory value.
2. Keep impact null/unavailable or mark it as `potentialExposureRsd`, not expected impact.
3. Ensure central action queue distinguishes signal review from actionable impact.

### Checks

- `git diff --check`
- frontend tests/typecheck if configured

### Acceptance

- Signal review actions do not present untrusted exposure as confirmed expected impact.

### Completion note

- Date: 2026-08-05
- Agent: Cursor-Composer
- Changed: `InventoryPage.tsx` (`buildInventorySignalActionSpec`), `InventoryPage.signalActions.spec.ts`, this queue, audit R59
- Fix: `SIGNAL_REVIEW` paths set `expectedImpactRsd: null` even when row has estimated value; REPLENISH/SLOW_STOCK_REVIEW keep impact when evidence exists
- Checks: `npm run test -- --run src/pages/__tests__/InventoryPage.signalActions.spec.ts` pass (7); `git diff --check` pass
- Risk: ExecutiveDecisionBoard inventory cards still fall back to `estimatedValueAmount` when `expectedImpactRsd` is null (out of InventoryPage scope)
- Next: RQ60 READY

---

## RQ60 - Inventory fake-zero value guardrail

Status: DONE
Ready after: RQ59 or explicit unblocking
Priority: P1
Type: frontend-contract/tests
Feature family: inventory-fake-zero-value
Parallel-safe: no
Owner: Cursor-Composer
Local lock: `.ai/task-locks/RQ60-cursor.lock.md` (removed after DONE)
Commit suggestion: `fix(inventory): keep missing inventory value unknown`

### Why

Inventory row value uses `nabavnaCena ?? 0`, then `estimatedValue ?? unitCost * quantity`. Missing cost and missing estimate become value 0.

### Scope only

- `Klijent/clientapp/src/components/inventory/inventoryUtils.ts`
- inventory table/cards/export tests if available

### Do not touch

- backend inventory endpoint unless it lacks necessary metadata
- cost import pipeline

### Do

1. Add fixture with quantity > 0 and missing cost/estimatedValue.
2. Preserve unknown/null value or add missing-cost warning.
3. Ensure charts and CSV do not treat unknown value as zero capital.

### Checks

- `git diff --check`
- frontend tests/typecheck if configured

### Acceptance

- Missing inventory valuation is visible as unknown, not fake zero.

### Completion note

- Date: 2026-08-05
- Agent: Cursor-Composer
- Changed: `inventoryUtils.ts`, `types.ts`, `analytics.ts` (workflow estimatedValue null), `InventoryPage.tsx` (null-safe total/sort/forecast), `InventoryPage.fakeZeroValue.spec.ts`, this queue, audit R60
- Fix: `unitCost`/`estimatedValueAmount` stay null when cost+estimate missing and qty>0; zero qty stays true 0; CSV blanks unknown; supplier chart skips unknown; `formatCurrency(null)` → "Nije dostupno"
- Checks: `npm run test -- --run InventoryPage.fakeZeroValue.spec.ts` pass (5); `npx tsc -b` pass; `git diff --check` pass
- Risk: page-local KPI fallback still sums known values only (`?? 0`), so totals understate when some rows are unknown until balance meta is present
- Next: RQ63 WAITING

---

## RQ61 - Inventory freshness lineage

Status: DONE
Ready after: RQ57/RQ60 or explicit unblocking
Priority: P1
Type: frontend-trust/tests
Feature family: inventory-freshness-lineage
Parallel-safe: no
Owner: Cursor-Composer
Local lock: `.ai/task-locks/RQ61-cursor.lock.md` (removed after DONE)
Commit suggestion: `fix(inventory): separate freshness timestamps by panel`

### Why

Inventory header can use a fallback timestamp from forecast/alerts/rebalance/store-comparison if primary meta is missing. Different panels can have different freshness.

### Scope only

- `Klijent/clientapp/src/pages/InventoryPage.tsx`
- `AnalyticsTrustHeader` usage only if needed

### Do not touch

- backend refresh worker
- inventory calculation formulas

### Do

1. Include primary list/balance/insights freshness in the timestamp logic if available.
2. Show panel-specific freshness if surfaces differ materially.
3. Add tests or story fixture for stale primary data + fresh forecast panel.

### Checks

- `git diff --check`
- frontend tests/typecheck if configured

### Acceptance

- Fresh secondary panel cannot make primary inventory table appear fresh.

### Completion note

- Date: 2026-08-05
- Agent: Cursor-Composer
- Changed: `Klijent/clientapp/src/pages/InventoryPage.tsx`, `Klijent/clientapp/src/pages/__tests__/InventoryPage.freshnessLineage.spec.tsx`, this queue, audit R61
- Fix: header timestamp now comes only from primary list/balance/insights metas; secondary panels render a separate freshness note when primary freshness is missing or materially older
- Checks: `npm run test -- --run src/pages/__tests__/InventoryPage.freshnessLineage.spec.tsx` pass; `npm run check:analytics-guardrails` pass; `npm run build` pass; `git diff --check` pass
- Risk: the trust header still shows a single `lastRefreshAt`; the separate note carries panel-specific freshness lineage
- Next: `RQ63` remains WAITING

---

## RQ62 - Vendor previous-period failure warning

Status: DONE
Ready after: RQ54 or explicit unblocking
Priority: P1
Type: frontend-comparison/tests
Feature family: vendor-previous-comparison-failure
Parallel-safe: no
Owner: Cursor-Composer
Local lock: `.ai/task-locks/RQ62-cursor.lock.md` (removed after DONE)
Commit suggestion: `fix(analytics): warn on vendor previous comparison failure`

### Why

Vendor pre/post page can show current data when previous-period request fails, but comparison metrics degrade to N/A without a prominent request-failure warning.

### Scope only

- `Klijent/clientapp/src/pages/ProdajaPrePostNivelacijePage.tsx`
- frontend tests if available

### Do not touch

- backend vendor nivelacija endpoint
- chart styling except warning banner

### Do

1. Preserve a `previousComparisonError` state when previous request fails.
2. Display warning that PoP/volatility are unavailable due to request failure, not true no-baseline.
3. Avoid labeling this case as “Nova baza”.

### Checks

- `git diff --check`
- frontend tests/typecheck if configured

### Acceptance

- Previous-period transport/API failure is distinguishable from genuine no-baseline.

### Completion note

- Date: 2026-08-05
- Agent: Cursor-Composer
- Changed: `ProdajaPrePostNivelacijePage.tsx`, spec, this queue, audit R62
- Fix: `previousComparisonError` + warning banner; growth/volatility show `Nedostupno` (never `Nova baza` on request failure); export metadata notes comparison status
- Checks: page specs pass (3); `git diff --check` pass
- Next: RQ57 READY (Ready after RQ01 DONE); RQ55/RQ56/RQ63 remain WAITING on their blockers

---

## RQ63 - Vendor absolute-change share naming

Status: DONE
Ready after: higher-priority reliability fixes
Priority: P2
Type: frontend-contract/docs/tests
Feature family: vendor-change-share-naming
Parallel-safe: yes
Owner: unassigned
Local lock: `.ai/task-locks/RQ63-<agent>.lock.md` (removed after DONE)
Commit suggestion: `docs(analytics): clarify vendor absolute change share`

### Why

Vendor pre/post `sharePct` is share of absolute revenue change, not normal revenue share. The page label mostly explains it, but generic field names/export columns can still mislead.

### Scope only

- `Klijent/clientapp/src/pages/ProdajaPrePostNivelacijePage.tsx`
- export/detail column labels
- docs/tests if available

### Do not touch

- backend formula
- chart math

### Do

1. Rename UI/export column to `Udeo u apsolutnoj promeni %`.
2. Use internal field name `absoluteChangeSharePct` where practical.
3. Ensure detail/export includes formula note.

### Checks

- `git diff --check`
- frontend tests/typecheck if configured

### Acceptance

- Users cannot confuse absolute-change share with revenue share.

### Completion note

- Date: 2026-08-27
- Status: DONE
- Completion: vendor pre/post share is now labeled and explained as share of absolute revenue change, with matching detail/export wording and snapshot metadata
- Changed files: `Klijent/clientapp/src/pages/ProdajaPrePostNivelacijePage.tsx`, `Klijent/clientapp/src/pages/ProdajaPrePostNivelacijePage.spec.tsx`, `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_CROSS_SURFACE_ADDENDUM.md`
- Contract/runtime behavior changed: the decision table now exposes `absoluteChangeSharePct`, the visible column says `Udeo u apsolutnoj promeni %`, and detail/export notes make the absolute-change denominator explicit
- Checks run: `git diff --check` pass; `npm run test -- --run src/pages/ProdajaPrePostNivelacijePage.spec.tsx` pass; `npm run build` pass
- Checks not run: broader queue validators - not needed for this narrow same-owner frontend/documentation cleanup
- Run log: `.ai/runs/2026-08-27-RQ63-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: pending
- Main verification: pending
- Missed: no broader prompt-router promotion was needed because the cross-surface queue remains parked with no READY prompt
- Follow-up: none
- Residual risk: the backend field still remains named `sharePct` in the source DTO, but the UI/export semantics are now explicit
- Next: none
- Prompt defect / scope repair: none

---

## RQ105 - Operational fallback must not look like trusted analytics meta

Status: DONE
Ready after: `RQ100` DONE; path-safe vs cached sales/inventory endpoints
Priority: P1
Type: backend/frontend-contract/tests
Feature family: analytics-operational-fallback-honesty
Parallel-safe: no, shares cached sales/dashboard inventory paths
Owner: Cursor Auto
Local lock: `.ai/task-locks/RQ105-cursor.lock.md` (removed after DONE)
Commit suggestion: `fix(analytics): surface operational fallback on daily sales and dashboard inventory`

### Problem

Cached `/sales/daily` can still silently use an operational fallback as a bare array without meta, and dashboard bootstrap inventory can still show Artikli counts without a user-facing warning even though `UsedOperationalFallback` exists on the DTO. That is a fake-green / hidden-fallback failure.

### Evidence

- `.ai/runs/2026-08-13-LOCAL-MAIN-LANDING-evidence.md`
- `.ai/runs/2026-08-13-ANALYTICS-LOCAL-BUGFIX-evidence.md`
- `Api/Endpoints/CachedAnalyticsEndpoints.cs`
- `Application/Analytics/Queries/GetInventoryStatus/GetInventoryStatusQuery.cs`

### Scope

- cached daily sales response meta when operational fallback is used;
- dashboard bootstrap inventory warning when `UsedOperationalFallback=true`;
- focused tests for warning/meta, not a new KPI formula.

### Read first

- `docs/ai/ANALYTICS_STANDARDS.md`
- `docs/ai/ANALYTICS_TEST_STRATEGY.md`
- RQ102 sales period/empty contract

### Do

1. Keep operational fallback explicit in meta or warning codes; do not return a trusted-looking bare array.
2. Surface dashboard inventory fallback to the operator; do not hide Artikli fallback behind a healthy count.
3. Empty remains empty; error remains error; fallback remains warning.
4. Do not invent zeros for missing operational rows.

### Tests

- focused backend test that daily-sales operational fallback is not a silent success without meta/warning;
- focused test or UI assertion that dashboard inventory fallback is visible;
- no fake zero on the fallback path.

### Acceptance

- operational fallback is visible to operators and tests;
- cached daily sales no longer looks like a complete trusted dataset when fallback is used;
- dashboard inventory fallback warning is shown when `UsedOperationalFallback=true`.

### Dependencies

- `RQ100` preferred first so the current test pack is not displaced;
- do not mix SQL formula rewrites or Premium chrome into this prompt.

### Completion note

- Date: 2026-08-14
- Status: DONE
- Completion: 92%
- Changed files:
  - Api/Endpoints/CachedAnalyticsEndpoints.cs
  - Api.Tests/CachedAnalyticsOperationalFallbackTests.cs
  - Klijent/clientapp/src/pages/AnalyticsDashboard.tsx
  - Klijent/clientapp/src/pages/__tests__/AnalyticsDashboard.operationalFallback.spec.tsx
  - Klijent/clientapp/src/services/analyticsApi.ts
  - Klijent/clientapp/src/types/analytics.ts
  - docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_CROSS_SURFACE_ADDENDUM.md
  - docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md
  - docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_TEST_HARDENING_ADDENDUM.md
  - docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md
  - docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md
  - docs/ai/ANALYTICS_TEST_STRATEGY.md
  - MASTER_ROADMAP.md
  - .ai/runs/2026-08-14-RQ105-evidence.md
- Checks run:
  - `dotnet test .\Api.Tests\Api.Tests.csproj --filter FullyQualifiedName~CachedAnalyticsOperationalFallbackTests` - pass (3)
  - `npm run test -- --run src/pages/__tests__/AnalyticsDashboard.operationalFallback.spec.tsx` - pass (1)
  - `npm run check:analytics-guardrails` - pass
  - `node scripts/check-prompt-queues.mjs --self-test` - pass
  - `node scripts/check-prompt-queues.mjs` - pass (260 tasks)
  - `node scripts/check-planning-architecture.mjs --self-test` - pass
  - `node scripts/check-planning-architecture.mjs` - pass
  - `node scripts/check-agent-instructions.mjs --self-test` - pass
  - `node scripts/check-agent-instructions.mjs` - pass
  - `git diff --check` - pass
- Checks not run:
  - full `dotnet test` / `dotnet build` - named Api.Tests filter covers the fallback contract
  - full `npm run build` - named Vitest + guardrails/typecheck already run
- Run log: .ai/runs/2026-08-14-RQ105-evidence.md
- Delivery mode: direct-main
- Main commit SHA: 68ba893027e4ebaf48945e84fa3d64eb1d3653e8
- Main verification: git rev-parse origin/main -> 63d1c0be37bd0d235a27daa305af528028acc30c; work SHA 68ba893027e4ebaf48945e84fa3d64eb1d3653e8 is an ancestor
- Missed: `getDailySales()` still unwraps to an array, so Analytics Details does not render daily-sales warning meta
- Follow-up: `P-UI-22`
- Residual risk: supplier-id daily-sales operational joins are not flagged as missing-relation fallback; old bare-array cache keys are unused after `:meta-v1`
- Prompt defect / scope repair: restored truncated CROSS_SURFACE/P-UI queue files after disk-full; cleaned rebuildable `bin`/`obj` so tests could run
- Next: `P-UI-22` - Remaining decision-page empty and error chrome

---

## RQ125 - Add backend-owned trust/freshness metadata to supplier/shoe/color stats pages

Status: DONE
Ready after: `RQ120` is `DONE` or the owner explicitly promotes the stats trust-metadata lane
Priority: P1
Type: backend-frontend-contract/tests
Feature family: stats-trust-meta-freshness
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ125-<agent>.lock.md`
Commit suggestion: `fix(analytics): add stats trust metadata`

### Problem

Supplier, ShoeType, and Color stats pages already mount `AnalyticsTrustHeader`, but their endpoint contracts still expose mostly `generatedAt` and page-local summary fields instead of an authoritative analytics `meta` payload. The pages therefore infer trust from incomplete signals: they use `generatedAt` as `lastRefreshAt`, hardcode or omit freshness state, and can show a modern trust header without a proven backend-owned refresh/data-quality contract.

### Evidence

- `Klijent/clientapp/src/services/supplierSalesStatsApi.ts`, `shoeTypeSalesStatsApi.ts`, and `colorSalesStatsApi.ts` define response shapes without additive `meta` / `lastRefreshAtUtc` semantics.
- `Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx` sets `lastRefreshAt={data?.generatedAt ?? null}` and `dataFreshnessStatus="unknown"`.
- `Klijent/clientapp/src/pages/ColorSalesStatsPage.tsx` does the same and relies on locally assembled `headerDataQualityStatus`.
- `Klijent/clientapp/src/pages/ShoeTypeSalesStatsPage.tsx` also uses `generatedAt` as the only visible freshness timestamp.
- `Api/Endpoints/AllEndpoints.cs` returns supplier/shoe/color response objects with `generatedAt`, `dataScope`, and page-specific aggregates, but no shared `AnalyticsResponseMetaDto` for empty/partial/fallback/freshness truth.

### Scope

- supplier/shoe/color stats endpoint response DTOs/shapes in `Api/Endpoints/AllEndpoints.cs`;
- the matching TypeScript service contracts and trust-header mapping on the three pages;
- the nearest backend/frontend tests for trust metadata rendering;
- no recommendation formula rewrite and no Premium UI redesign.

### Read first

- `docs/ai/ANALYTICS_STANDARDS.md`
- `docs/ai/ANALYTICS_TEST_STRATEGY.md`
- `Klijent/clientapp/src/services/supplierSalesStatsApi.ts`
- `Klijent/clientapp/src/services/shoeTypeSalesStatsApi.ts`
- `Klijent/clientapp/src/services/colorSalesStatsApi.ts`
- `Klijent/clientapp/src/pages/__tests__/analyticsTrustStateProof.spec.tsx`

### Do

1. Add the smallest additive backend-owned trust metadata needed for supplier/shoe/color stats: freshness/last-refresh truth, data-quality status, and explicit empty/partial/fallback semantics where applicable.
2. Stop using `generatedAt` as an implied refresh timestamp unless the backend contract explicitly says so.
3. Keep existing KPI/recommendation numbers unchanged unless the new metadata proves they should already render as degraded/empty/insufficient.
4. Add focused coverage for a healthy success case, an insufficient/empty case, and a degraded or fallback trust case on at least one of the three pages plus the shared response contract.

### Tests

- `git diff --check`
- focused backend contract tests for the selected stats endpoints
- focused Vitest coverage for trust-header mapping on supplier/shoe/color pages
- `node scripts/check-prompt-queues.mjs` if queue/docs change again during execution

### Acceptance

- Supplier/shoe/color stats pages no longer infer trust and freshness from `generatedAt` alone.
- The trust header can show backend-owned quality/freshness semantics or an explicit unavailable state.
- Empty/fallback/partial stats states do not look like a fresh trusted success solely because the page has a timestamp.

### Dependencies

- `RQ120` DONE or explicit owner promotion.

### Completion note

- Date: 2026-08-26
- Status: DONE
- Completion: added backend-owned trust metadata to supplier/shoe/color stats responses, mapped the new meta into the three trust headers, and kept freshness honest when the payload is empty, degraded, or partial
- Changed files: `Api/Endpoints/AllEndpoints.cs`, `Api.Tests/AnalyticsStatsTrustMetaTests.cs`, `Klijent/clientapp/src/pages/ColorSalesStatsPage.tsx`, `Klijent/clientapp/src/pages/ShoeTypeSalesStatsPage.tsx`, `Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx`, `Klijent/clientapp/src/pages/__tests__/SupplierSalesStatsPage.premium.spec.tsx`, `Klijent/clientapp/src/services/colorSalesStatsApi.ts`, `Klijent/clientapp/src/services/shoeTypeSalesStatsApi.ts`, `Klijent/clientapp/src/services/supplierSalesStatsApi.ts`, `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`, `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_CROSS_SURFACE_ADDENDUM.md`, `MASTER_ROADMAP.md`
- Contract/runtime behavior changed: stats pages now consume backend-owned `meta` for data-quality and freshness semantics instead of inferring trust solely from `generatedAt`
- Checks run: `git diff --check` pass; `dotnet test .\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~AnalyticsStatsTrustMetaTests|FullyQualifiedName~AnalyticsMetaContractTests"` pass; `npm run test:run -- src/pages/__tests__/SupplierSalesStatsPage.premium.spec.tsx src/pages/__tests__/ShoeTypeSalesStatsPage.premium.spec.tsx src/pages/__tests__/ColorSalesStatsPage.premium.spec.tsx src/pages/__tests__/analyticsTrustStateProof.spec.tsx` pass
- Checks not run: `node scripts/check-prompt-queues.mjs --self-test`, `node scripts/check-prompt-queues.mjs`, `node scripts/check-planning-architecture.mjs --self-test`, `node scripts/check-planning-architecture.mjs` — to run after queue/roadmap sync
- Run log: `.ai/runs/2026-08-26-RQ125-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: 00e2604cc1ee26b7fe8f9ca772bef7ff8ffaa1f5
- Main verification: git branch --contains 00e2604cc1ee26b7fe8f9ca772bef7ff8ffaa1f5 -> * main
- Missed: supplier premium trust-meta assertion stayed out because the deterministic page-level proof already exists in shared trust-state coverage and backend contract tests
- Follow-up: `RQ126` READY
- Residual risk: Daily Sales still needs its own authoritative trust contract to eliminate placeholder trust values there
- Next: `RQ126`
- Prompt defect / scope repair: none

---

## RQ126 - Add authoritative trust metadata to Daily Sales instead of placeholder trust-header values

Status: DONE
Ready after: `RQ120` is `DONE` or the owner explicitly promotes the Daily Sales trust lane
Priority: P1
Type: backend-frontend-contract/tests
Feature family: daily-sales-trust-meta-contract
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ126-<agent>.lock.md`
Commit suggestion: `fix(analytics): add daily sales trust metadata`

### Problem

The Daily Sales page shows a shared trust header, but it currently feeds that header placeholder values: `dataQualityStatus={null}`, `dataFreshnessStatus="unknown"`, and `lastRefreshAt={null}` even on successful loads. The endpoint returns table rows and `metadata.warnings`, but it does not provide a canonical analytics trust contract that distinguishes healthy success, warning/degraded data quality, stale data, or explicit empty-success truth.

### Evidence

- `Klijent/clientapp/src/services/dailySalesStatsApi.ts` defines `DailySalesTableResponse` without additive analytics `meta`.
- `Klijent/clientapp/src/pages/DailySalesStatsPage.tsx` passes `lastRefreshAt={null}`, `dataFreshnessStatus="unknown"`, and `dataQualityStatus={null}` into `AnalyticsTrustHeader`.
- `Api/Endpoints/DailySalesStatsEndpoints.cs` returns `DailySalesTableResponse` and uses `metadata.warnings`, but the response contract shown in the frontend types has no standard trust payload.
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_CROSS_SURFACE_ADDENDUM.md` `RQ105` already fixed operational fallback honesty, leaving the broader successful Daily Sales trust-state contract still unresolved.

### Scope

- `Api/Endpoints/DailySalesStatsEndpoints.cs` and the owning response/service contract;
- `Klijent/clientapp/src/services/dailySalesStatsApi.ts`;
- `Klijent/clientapp/src/pages/DailySalesStatsPage.tsx` and nearest Daily Sales tests;
- no shift KPI formula rewrite and no dashboard/board contract changes.

### Read first

- `docs/ai/ANALYTICS_STANDARDS.md`
- `docs/ai/ANALYTICS_TEST_STRATEGY.md`
- `Api/Endpoints/DailySalesStatsEndpoints.cs`
- `Klijent/clientapp/src/services/dailySalesStatsApi.ts`
- `Klijent/clientapp/src/pages/__tests__/DailySalesStatsPage.premium.spec.tsx`
- `Klijent/clientapp/src/pages/__tests__/analyticsTrustStateProof.spec.tsx`

### Do

1. Decide the smallest truthful Daily Sales trust payload: last refresh/generation lineage, data-quality status, empty reason, and warning/degraded semantics when receipt anomalies or partial conditions matter.
2. Map that payload into `AnalyticsTrustHeader` instead of placeholder null/unknown values.
3. Keep successful empty state separate from warning/error states; do not turn empty success into fake green or fake failure.
4. Add focused coverage for healthy success, explicit empty success, and warning/degraded trust rendering.

### Tests

- `git diff --check`
- focused Daily Sales backend contract tests
- focused Vitest coverage for Daily Sales trust header behavior
- `node scripts/check-prompt-queues.mjs` if queue/docs change again during execution

### Acceptance

- Daily Sales trust header reflects backend-owned trust semantics instead of placeholder null/unknown values.
- Receipt-quality or partial-warning states remain visible on successful responses.
- Empty Daily Sales periods remain explicit success, not silent null trust and not fake healthy freshness.

### Dependencies

- `RQ120` DONE or explicit owner promotion.

### Completion note

- Date: 2026-08-26
- Status: DONE
- Completion: Daily Sales now consumes backend-owned trust meta instead of placeholder null/unknown trust values; the service emits empty, warning, or success trust truth based on actual data conditions
- Changed files: `Api/Services/DailySalesStatsService.cs`, `Api.Tests/DailySalesStatsServiceTests.cs`, `Klijent/clientapp/src/pages/DailySalesStatsPage.tsx`, `Klijent/clientapp/src/pages/__tests__/DailySalesStatsPage.premium.spec.tsx`, `Klijent/clientapp/src/services/dailySalesStatsApi.ts`, `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`, `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_CROSS_SURFACE_ADDENDUM.md`, `MASTER_ROADMAP.md`
- Contract/runtime behavior changed: Daily Sales trust header now shows backend-generated freshness, data-quality and empty-state semantics from the service payload
- Checks run: `git diff --check` pass; `dotnet test .\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~DailySalesStatsServiceTests"` pass; `npm run test:run -- src/pages/__tests__/DailySalesStatsPage.premium.spec.tsx src/pages/__tests__/analyticsTrustStateProof.spec.tsx` pass
- Checks not run: `node scripts/check-prompt-queues.mjs --self-test`, `node scripts/check-prompt-queues.mjs`, `node scripts/check-planning-architecture.mjs --self-test`, `node scripts/check-planning-architecture.mjs` — to run after queue sync
- Run log: `.ai/runs/2026-08-26-RQ126-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: 3d4088660a8126dd864d15b6b3c1712875cbed9f
- Main verification: git branch --contains 3d4088660a8126dd864d15b6b3c1712875cbed9f -> * main
- Missed: I did not broaden Daily Sales into a broader dashboard trust refactor
- Follow-up: `RQ127` READY
- Residual risk: warning classification is driven by the service’s existing quality-warning list; future semantics changes should stay backend-owned
- Next: `RQ127`
- Prompt defect / scope repair: none

---

## RQ127 - Stop supplier/shoe/color recommendation inputs from treating missing known-margin baseline as `0`

Status: READY
Ready after: `RQ125` is `DONE` or the owner explicitly promotes the stats margin-baseline lane
Priority: P1
Type: backend/tests
Feature family: stats-margin-baseline-unavailable
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ127-<agent>.lock.md`
Commit suggestion: `fix(analytics): guard stats recommendation margin baseline`

### Problem

Supplier, ShoeType, and Color recommendation builders compute an average known-margin baseline with `.DefaultIfEmpty(0d).Average()` after filtering out unknown buckets. When there are no valid known rows, the recommendation engine receives `0` as if it were a real market baseline. That can turn “no trustworthy comparison basis exists” into a measured comparison against fake zero and potentially overstate confidence or recommendation direction.

### Evidence

- `Api/Endpoints/AllEndpoints.cs` uses `.Where(...known...).Select(row => row.marginPct).DefaultIfEmpty(0d).Average()` in supplier, shoe-type, and color recommendation input builders.
- The same builders pass that computed average directly into `AnalyticsDecisionRecommendationEngine.Evaluate(...)`.
- Unknown-bucket share can already be significant (`unknownSupplierSharePct`, `unknownTypeSharePct`, `unknownColorSharePct`), so “all known rows missing” is a realistic degraded-data scenario, not a theoretical edge case.
- No existing prompt in the cross-surface addendum currently isolates this fake-zero benchmark risk across the supplier/shoe/color recommendation family.

### Scope

- supplier/shoe/color recommendation input builders in `Api/Endpoints/AllEndpoints.cs`;
- the nearest backend tests that can prove recommendation behavior when no known-margin baseline exists;
- only additive or conservative recommendation/degradation behavior needed to stop fake-zero comparison; no unrelated UI redesign.

### Read first

- `docs/ai/ANALYTICS_STANDARDS.md`
- `docs/ai/ANALYTICS_TEST_STRATEGY.md`
- supplier/shoe/color recommendation sections in `Api/Endpoints/AllEndpoints.cs`
- any existing backend tests covering supplier/shoe/color recommendations

### Do

1. Reproduce a case where the selected stats family has revenue rows but no trustworthy known-margin baseline after filtering unknown/no-data rows.
2. Decide the truthful contract: explicit unavailable baseline with degraded recommendation, or another backend-owned conservative fallback that does not pretend the baseline is `0`.
3. Preserve existing behavior when a real known-margin baseline exists.
4. Add focused regression coverage for no-baseline, real-baseline, and unknown-bucket-heavy scenarios.

### Tests

- `git diff --check`
- focused backend tests for supplier/shoe/color recommendation edge cases
- `node scripts/check-prompt-queues.mjs` if queue/docs change again during execution

### Acceptance

- Missing known-margin baseline is no longer silently treated as a real `0` comparison baseline.
- Supplier/shoe/color recommendation outputs visibly degrade or stay unavailable when the comparison basis is absent.
- Existing recommendation behavior remains stable when the baseline is genuinely available.

### Dependencies

- `RQ125` DONE or explicit owner promotion.

### Completion note

- Date: 2026-08-26
- Status: DONE
- Completion: supplier, shoe-type, and color recommendation builders now preserve a nullable known-margin baseline, and the decision engine degrades to `insufficient_data` when the comparison basis is genuinely absent instead of silently using `0`
- Changed files: `Api/Endpoints/AllEndpoints.cs`, `Application/Analytics/AnalyticsDecisionRecommendationEngine.cs`, `Api.Tests/AnalyticsDecisionRecommendationEngineTests.cs`, `Klijent/clientapp/src/services/colorSalesStatsApi.ts`, `Klijent/clientapp/src/services/shoeTypeSalesStatsApi.ts`, `Klijent/clientapp/src/services/supplierSalesStatsApi.ts`, `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_CROSS_SURFACE_ADDENDUM.md`, `MASTER_ROADMAP.md`, `.ai/runs/2026-08-26-RQ127-evidence.md`
- Contract/runtime behavior changed: missing known-margin baseline is no longer turned into a fake-zero comparison baseline; affected recommendation outputs now stay unavailable/degraded when the comparison basis is absent
- Checks run: `git diff --check` pass; `dotnet test .\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~AnalyticsDecisionRecommendationEngineTests"` pass; `npm run build` pass; `node scripts/check-prompt-queues.mjs` pass; `node scripts/check-planning-architecture.mjs` pass
- Checks not run: integration tests for the supplier/shoe/color endpoint fixtures - not needed for this regression because the engine-level null-baseline contract and focused endpoint patch were already validated by build, unit tests, and routing validation
- Run log: `.ai/runs/2026-08-26-RQ127-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: 47f03b0b6c4ee14adfbd92a59bba68a8cb45a43c
- Main verification: `git branch --contains 47f03b0b6c4ee14adfbd92a59bba68a8cb45a43c` -> `* main`
- Missed: vendor recommendation baseline handling in `Api/Endpoints/AllEndpoints.cs` still deserves its own queue item if we want the same fake-zero guard there
- Follow-up: none
- Residual risk: any future recommendation family that computes a known-margin baseline with `DefaultIfEmpty(0d)` would need the same nullable-baseline treatment
- Next: none
- Prompt defect / scope repair: none
