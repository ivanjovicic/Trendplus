# Analytics Reliability Audit Round 4

Date: 2026-09-06
Repo: `ivanjovicic/Trendplus`
Queue state before sync: `RQ167` remained the existing `READY` prompt; no active lock blocked appending new `WAITING` prompts.

## Scope

This pass inspected in-scope analytics screens and their nearest API/DTO/backend owners, with emphasis on inventory trust aggregation, sales/pre-post numeric states, Decision Board provenance and existing queue coverage. Forecast-only, Trend Models, Shopify, scrapers and unrelated test functionality were not promoted into this queue.

## Confirmed New Findings

| Prompt | Surface | Confirmed defect | User risk |
|---|---|---|---|
| RQ237 | `/analytics/inventory` | The composite trust header uses the first meta for quality/partial state but the newest timestamp for refresh. A warning or stale secondary source can therefore be hidden by a good primary source and a newer query timestamp. | The inventory page can look healthy even though one of its displayed datasets is degraded or its source freshness is unknown. |
| RQ238 | Shoe Type Sales / pre-post detail | `ShoeTypeSalesStatsPage` derives `coveragePct` as `0` when `brojArtikalaUkupno` is zero, although that is an undefined ratio. The row type also declares the derived value as a non-null number. | An unavailable denominator can be presented or exported as measured zero coverage, conflating no denominator with a real zero. |
| RQ239 | Executive Decision Board compatibility fallback | `buildExecutiveFallbackSupplierCards` assigns `summary.to` to `generatedAtUtc`. `summary.to` is the selected period end, not response generation or refresh time. | A period boundary is shown as provenance/freshness metadata and can mislead users about when the fallback data was produced. |
| RQ240 | Analytics Details inventory KPIs | The page coalesces missing `outOfStockCount` and `lowStockCount` to zero before calculating in-stock and red-zone percentages. With a known total SKU count, missing counts become false `100%` and `0%`. | Missing inventory evidence is presented as a clean result rather than unavailable data. |

## Evidence Map

- `Klijent/clientapp/src/pages/InventoryPage.tsx:658-676` builds `primaryInventoryMetas`, selects the newest refresh timestamp and then selects `primaryInventoryMetas[0]` as the quality owner.
- `Klijent/clientapp/src/pages/InventoryPage.tsx:1046-1054` passes those inconsistent values to `AnalyticsTrustHeader`.
- `RQ176` and `RQ187` address query/cache timestamps, but neither owns aggregation of several independent inventory metas in the page header.
- `Klijent/clientapp/src/pages/ShoeTypeSalesStatsPage.tsx:91` declares `coveragePct: number`, while `:435-437` maps a zero denominator to `0`.
- `Api/Endpoints/AllEndpoints.cs:2264-2265` supplies the source counts used by the shoe-type response; a zero denominator is not evidence of zero measured coverage.
- `Klijent/clientapp/src/pages/AnalyticsDetails.tsx:303-306` maps `inventory?.outOfStockCount` and `inventory?.lowStockCount` to zero before calculating `inStock` and `red`.
- `Klijent/clientapp/src/pages/AnalyticsDetails.tsx:408-414` renders those ratios as decision-facing risk KPIs.
- `Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx:599-604` builds the compatibility supplier fallback, and `:668` assigns `summary.to` to `generatedAtUtc`.
- `Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx:1278-1282` renders the card timestamp to users.
- `docs/qa/DECISION_BOARD_CANDIDATE_CONTRACT_AUDIT.md:227-228` already requires missing candidate freshness to stay unknown, but no queue item owns this concrete fallback assignment.

## Coverage and Non-Duplicates

- Rebalance row actionability and missing `recommendationAllowed` remain covered by `RQ178`; no duplicate prompt was created.
- Supplier concentration, report filter fidelity, supplier report actionability and supplier report numeric state remain covered by `RQ233-RQ236`.
- Existing `RQ204` owns Analytics Details inventory period/scope parity. `RQ240` is narrower: it protects nullable inventory counts from becoming ratio inputs and does not redefine period scope.
- Existing `RQ145`/`RQ141` remain broad parity and lineage owners. `RQ237` is a concrete inventory-page aggregation repair, not a replacement for those broader proofs.
- The unused internal `coveragePct` field is not itself treated as a separate product finding beyond the zero-denominator contract in `RQ238`; the prompt must prove every table/detail/export consumer before changing it.

## Files and History Inspected

- `AGENTS.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- `Klijent/clientapp/src/pages/InventoryPage.tsx`
- `Klijent/clientapp/src/pages/AnalyticsDetails.tsx`
- `Klijent/clientapp/src/pages/ShoeTypeSalesStatsPage.tsx`
- `Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx`
- `Klijent/clientapp/src/components/inventory/RebalancingTable.tsx`
- `Application/Analytics/Queries/GetRebalanceSuggestions/GetRebalanceSuggestionsQuery.cs`
- `Application/Analytics/Queries/GetRebalanceSuggestions/GetRebalanceSuggestionsHandler.cs`
- `Api/Endpoints/AllEndpoints.cs`
- nearest inventory, Analytics Details, shoe-type and Executive Board tests
- recent Git history for the inspected page/component files

## Validation

- Canonical queue validator: pass after synchronizing `RQ237-RQ240`.
- `git diff --check`: pass for tracked changes.
- New Markdown whitespace check: pass; any LF/CRLF notice is an encoding-normalization warning, not a content failure.
- Runtime tests, backend/frontend builds and browser console/live refresh proof were not run because this round changes only queue/audit documentation.

## Delivery Truth

- `RQ237-RQ240` are `WAITING`, not completed fixes.
- Existing `RQ167 READY` was preserved.
- No lock was claimed or changed.
- No production code or tests were changed in this audit round.
- No commit or push was performed.

## Residual Risk

The four findings require separate implementation and failing-first regression proof. Until then, the affected surfaces are not proven fully reliable for composite freshness, undefined ratios, provenance timestamps or nullable inventory risk ratios.
