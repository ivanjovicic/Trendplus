# Analytics Third Calculation Audit

Date: 2026-09-06  
Repository: `ivanjovicic/Trendplus`  
Scope: new findings after the 2026-09-05 calculation recheck and the 2026-09-06 second calculation audit

## Verdict

This pass found five additional, bounded reliability gaps. They are not duplicates of `RQ154`-`RQ170`, and they are not being silently fixed in this audit. Each gap has a concrete source/consumer path, a failing-first test shape and a separate waiting prompt in the canonical queue.

No product runtime files were changed in this pass. The work is an audit and queue refill only.

## Rechecked boundaries

| Surface | React/component | API client/endpoint | DTO/backend/SQL source | Cache/refresh | Existing proof | Result |
|---|---|---|---|---|---|---|
| Inventory signal freshness | `InventoryPage.tsx`, `InventoryAlertsFeed.tsx`, `RebalancingTable.tsx`, `SizeCurvePanel.tsx` | `analyticsApi.ts`; cached `/api/analytics/cached/inventory/alerts`, `/rebalance-suggestions`, `/size-curve` | `InventoryAlertListDto`, `RebalanceSuggestionListDto`, `InventorySizeCurveListDto`; handlers read `analytics_*_snapshot` relations | `AnalyticsCachePolicy.Inventory.Ttl`; response `GeneratedAtUtc` is assigned in the reader handler | `InventorySnapshotContractTests`, inventory page freshness/null-evidence tests | Query time is still used as secondary freshness; size-curve state loses warning/empty distinction; actionability and copy are not fully contract-backed |
| Supplier footwear signal | `SupplierFootwearAnalyticsPage.tsx` | `vendorSalesNivelacijaApi.ts`; `/api/analytics/vendor-sales-nivelacija` | `VendorSalesNivelacijaResponse`; `AllEndpoints.cs` vendor pre/post SQL/reader path | vendor analytics cache/meta path | `SupplierFootwearAnalyticsPage.spec.tsx`, pre/post parity tests | Page can label data `fresh` from `generatedAt` when no proven refresh metadata exists |
| Pre/post aggregate parity | `ProdajaPrePostNivelacijePage.tsx` | `vendorSalesNivelacijaApi.ts`; vendor pre/post endpoint | `VendorSalesNivelacijaTotalsDto` and vendor rows from `AllEndpoints.cs` | vendor analytics cache/meta path | `ProdajaPrePostNivelacijePage.spec.tsx`, pre/post trust utility tests | Frontend reconstructs absolute-change total/share when backend total is unavailable instead of keeping backend as sole owner |

## Findings

### RQ176: Inventory snapshot query time is shown as data freshness

Evidence:

- `GetInventoryAlertsHandler.cs:90`, `GetRebalanceSuggestionsHandler.cs:94` and `GetInventorySizeCurveHandler.cs:103` set `GeneratedAtUtc: DateTime.UtcNow` while reading the snapshot.
- `InventoryPage.tsx:667-675` takes those `generatedAtUtc` values as `secondaryPanelTimestamps`.
- `InventoryPage.tsx:714-725` describes that value as secondary-panel freshness and compares it with the primary last refresh.
- The three list DTOs expose `GeneratedAtUtc`, but no snapshot freshness/last-successful-refresh field.
- `docs/qa/FORECAST_SNAPSHOT_PROVENANCE_CONTRACT_2026-08-20.md` already establishes the semantic rule that generated response time is not snapshot freshness. This finding applies that rule to the non-forecast inventory signals.

Risk: a cache hit or an old snapshot read now can look freshly refreshed at the time of the HTTP query. The page can therefore show a false freshness comparison while the actual source snapshot age is unknown.

History checked: `41790622`, `29a5943a`, `4c8844b9` and `e3933c0d` contain earlier trust/freshness and inventory-signal hardening. They do not add snapshot freshness metadata for alerts, rebalance or size curve.

### RQ177: Size-curve missing relation and successful empty result collapse in the UI

Evidence:

- `GetInventorySizeCurveHandler.cs:102-110` returns `SnapshotAvailable=true` plus an explicit empty warning for a successful empty result.
- `GetInventorySizeCurveHandler.cs:117-125` returns `SnapshotAvailable=false` plus a missing-snapshot warning for a missing relation.
- `SizeCurvePanel.tsx:60-63` uses one branch for both `!snapshotAvailable` and `items.length === 0`, renders the same `Nema size curve podataka` copy and does not render `sizeCurve.warning`.
- Backend `InventorySnapshotContractTests` cover both states, but no panel test proves that missing relation, successful empty and partial warning remain distinct to the user.

Risk: an unavailable table can look like a valid empty SKU result, and a backend warning about partial/missing evidence can disappear from the primary panel. This violates empty-is-not-error and visible degraded-state rules.

History checked: the inventory snapshot fixes in `e3933c0d` and the current `InventorySignalNullEvidence` tests protect null values, but not this panel-level state projection.

### RQ178: Inventory signal rows lack a complete user-safe actionability/copy contract

Evidence:

- `InventoryAlertsFeed.tsx:77` renders a confidence slot for every alert, including `null` evidence (formatted as `N/A`), and `:83` renders the raw `alertType` (`Tip: ...`).
- `RebalancingTable.tsx:98` renders `item.reason` directly; the snapshot DTO has no `recommendationAllowed`, decision status or reason-code mapping field.
- `InventoryAlertListDto`, `RebalanceSuggestionListDto` and `InventorySizeCurveListDto` expose warning text but do not expose a shared backend actionability/evidence contract.
- The handlers correctly preserve null evidence and emit warnings, but the panels still render recommendation-like rows and navigation/action affordances without a backend-owned allowed/blocked state.

Risk: internal values such as `inventory_missing` or a technical reason can reach users, and an incomplete rebalance row can look executable even though the API has no way to state `recommendationAllowed=false`. The existing `RQ143`/`RQ145` prompts own the broad cross-surface rule; this prompt is limited to these three inventory snapshot panels and their DTO parity.

History checked: `5db83e1`/`e3933c0d` fixed null coercion and reader-count semantics; `RQ151` fixed action messages, but did not cover inventory snapshot `alertType`/`reason` or a shared snapshot actionability field.

### RQ179: Supplier footwear marks query-time generated data as fresh

Evidence:

- `SupplierFootwearAnalyticsPage.tsx:575-576` sets `dataFreshnessStatus` to `fresh` whenever `data.generatedAt` exists and no warning flag exists.
- `SupplierFootwearAnalyticsPage.tsx:655-656` repeats the same fallback for the visible trust header.
- The same code passes `data.meta?.lastRefreshAtUtc` separately, so the page can show no last successful refresh while still displaying `fresh`.
- The vendor response’s `generatedAt` is produced by the vendor analytics request path; it is not, by itself, a successful materializer/refresh timestamp.

Risk: a supplier footwear signal can be presented as fresh solely because the request returned a response. Unknown refresh lineage is not the same as fresh data.

History checked: `570a31e8`, `41790622`, `a84d8a42` and `29a5943a` harden pre/post comparability/trust, but this page-specific freshness fallback remains.

### RQ180: Pre/post frontend reconstructs a backend-owned aggregate

Evidence:

- `ProdajaPrePostNivelacijePage.tsx:630-634` falls back to `rows.reduce((sum, item) => sum + Math.abs(item.changeRevenue), 0)` when the backend total is unavailable.
- `ProdajaPrePostNivelacijePage.tsx:646-649` then derives per-row absolute-change share from that reconstructed total.
- The same value feeds the top-five concentration and vendor concentration views around `:733-745`, `:847-894` and `:942-963`, and the table/export column contract around `:151-155`.
- Backend already calculates `VendorSalesNivelacijaTotalsDto.AbsoluteChangeRevenue` and `VendorSalesNivelacijaVendorStat.AbsoluteChangeRevenue` in `AllEndpoints.cs`; the frontend fallback can therefore become a second aggregate owner when payload completeness differs.

Risk: cards, table, chart and export can use a frontend-derived denominator that differs from the backend aggregate or from a filtered/partially comparable vendor set. This is separate from `RQ140` comparability and `RQ156` coverage semantics: it is specifically an ownership/parity defect.

History checked: `570a31e8` and `a84d8a42` improved the backend pre/post contract; the current page still contains the local fallback arithmetic.

## Not new findings in this pass

- The pre/post `AbsoluteChangeRevenue = 0m` initializer is overwritten by `vendorStats.Sum(...)` in the normal endpoint path; it is not recorded as a standalone bug without an exception/empty-path reproducer.
- Inventory snapshot null-to-zero/false coercion, count/truncation, placeholder-zero, search/store lineage and size-curve boolean evidence are already marked DONE in the inventory-signal addendum (`RQ64`-`RQ71`).
- The primary inventory freshness fallback was already addressed by `RQ61`; this pass only records the remaining secondary snapshot query-time path.
- Existing broader owners remain authoritative: `RQ141` for lineage/refresh, `RQ143` for backend decision ownership, `RQ145` for parity/safe messaging and `RQ146` for runtime schema/refresh proof.

## Queue result

Added waiting prompts `RQ176`-`RQ180` to `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`. `RQ154` remains the only `READY` prompt. No forecast, Shopify or external connector prompt was promoted.

## Proof boundary

This audit is static/repository evidence only. It proves the source paths and missing tests; it does not claim that the runtime fixes are complete. The new prompts require failing-first backend/frontend tests, parity checks, safe copy, and the appropriate focused builds when executed.
