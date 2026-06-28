# Analytics Reliability Prompt Queue - Cross-Surface Addendum

Date: 2026-06-28
Repo: `ivanjovicic/Trendplus`
Current READY prompt: none in this addendum
Main queue READY prompt: `RQ01` in `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`

Use this queue with `docs/ai/PROMPT_QUEUE_PROTOCOL.md`.

Purpose: add reliability prompts for cross-surface analytics inconsistencies: supplier/shoe/color stats, vendor pre/post nivelacija, inventory sorting/export/action lineage and freshness semantics. These prompts remain WAITING until explicitly reprioritized.

## Status summary

| Task | Status | Feature family | Purpose |
|---|---|---|---|
| RQ51 | WAITING | color-insufficient-data-status | Stop mapping color insufficient_data to Zadrzi |
| RQ52 | WAITING | color-local-recommendation-fallback | Remove or label Color frontend recommendation fallback |
| RQ53 | WAITING | color-shoetype-datascope-lineage | Pass/verify dataScope in Color and ShoeType pages |
| RQ54 | WAITING | vendor-nivelacija-scope-lineage | Add/verify dataScope/store lineage on Vendor pre/post page |
| RQ55 | WAITING | supplier-hidden-unknown-denominators | Clarify denominators when unknown suppliers are hidden |
| RQ56 | WAITING | total-cost-fallback-guardrail | Do not clamp inconsistent implied cost to fake zero |
| RQ57 | WAITING | inventory-risk-global-sort | Make inventory OOS/overstock sort global or clearly page-local |
| RQ58 | WAITING | inventory-screen-csv-order | Make CSV ekran match displayed risk-sorted rows |
| RQ59 | WAITING | inventory-signal-review-impact | Do not attach confirmed impact to weak signal-check actions |
| RQ60 | WAITING | inventory-fake-zero-value | Preserve unknown inventory value when cost is missing |
| RQ61 | WAITING | inventory-freshness-lineage | Separate inventory panel freshness timestamps |
| RQ62 | WAITING | vendor-previous-comparison-failure | Warn when previous-period request fails |
| RQ63 | WAITING | vendor-change-share-naming | Rename/clarify top5 share of absolute change |

---

## RQ51 - Color insufficient_data must not become Zadrzi

Status: WAITING
Ready after: RQ01 or explicit reprioritization
Priority: P0
Type: frontend-contract/tests
Feature family: color-insufficient-data-status
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ51-<agent>.lock.md`
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

---

## RQ52 - Color frontend recommendation fallback

Status: WAITING
Ready after: RQ51 DONE or explicit unblocking
Priority: P1
Type: frontend-contract/tests
Feature family: color-local-recommendation-fallback
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ52-<agent>.lock.md`
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

---

## RQ53 - Color/ShoeType dataScope lineage

Status: WAITING
Ready after: RQ39/RQ51 or explicit reprioritization
Priority: P1
Type: frontend-contract/tests
Feature family: color-shoetype-datascope-lineage
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ53-<agent>.lock.md`
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

---

## RQ54 - Vendor pre/post scope lineage

Status: WAITING
Ready after: RQ53 or explicit unblocking
Priority: P1
Type: frontend-contract/tests
Feature family: vendor-nivelacija-scope-lineage
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ54-<agent>.lock.md`
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

Status: WAITING
Ready after: RQ01 or explicit reprioritization
Priority: P0
Type: frontend/backend-contract/tests
Feature family: inventory-risk-global-sort
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ57-<agent>.lock.md`
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

---

## RQ58 - Inventory screen CSV order parity

Status: WAITING
Ready after: RQ57 or explicit unblocking
Priority: P1
Type: frontend-export/tests
Feature family: inventory-screen-csv-order
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ58-<agent>.lock.md`
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

---

## RQ59 - Inventory signal-review impact trust

Status: WAITING
Ready after: RQ57 or explicit reprioritization
Priority: P1
Type: frontend/action-contract/tests
Feature family: inventory-signal-review-impact
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ59-<agent>.lock.md`
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

---

## RQ60 - Inventory fake-zero value guardrail

Status: WAITING
Ready after: RQ59 or explicit unblocking
Priority: P1
Type: frontend-contract/tests
Feature family: inventory-fake-zero-value
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ60-<agent>.lock.md`
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

---

## RQ61 - Inventory freshness lineage

Status: WAITING
Ready after: RQ57/RQ60 or explicit unblocking
Priority: P1
Type: frontend-trust/tests
Feature family: inventory-freshness-lineage
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ61-<agent>.lock.md`
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

---

## RQ62 - Vendor previous-period failure warning

Status: WAITING
Ready after: RQ54 or explicit unblocking
Priority: P1
Type: frontend-comparison/tests
Feature family: vendor-previous-comparison-failure
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ62-<agent>.lock.md`
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

---

## RQ63 - Vendor absolute-change share naming

Status: WAITING
Ready after: higher-priority reliability fixes
Priority: P2
Type: frontend-contract/docs/tests
Feature family: vendor-change-share-naming
Parallel-safe: yes
Owner: unassigned
Local lock: `.ai/task-locks/RQ63-<agent>.lock.md`
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
