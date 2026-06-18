# Decision Confidence Contract

Updated: 2026-06-18

## Purpose

Trendplus analytics recommendations must explain not only *what* to do, but also *how confident the system is*, *why the recommendation exists*, *what impact is expected*, and *what risk remains if the operator ignores it*.

This document defines the canonical Phase 1 contract for analytics recommendations across:

- Product Decision Center
- Inventory
- Supplier analytics / scorecard
- Action Outcome Summary as a feedback signal, not as the recommendation source

## Contract rules

1. `insufficient_data` must never be presented as high confidence.
2. Missing denominator or missing evidence means nullable impact fields, not fake zero.
3. Frontend must not invent confidence fields that the backend did not send.
4. Warnings must be visible near the recommendation, not buried in diagnostics only.
5. Recommendation confidence is a decision-support signal, not a proof of correctness.

## Canonical recommendation contract

This is the shared shape every recommendation should converge toward.

| Field | Type | Required | Meaning |
|---|---|---:|---|
| `recommendationId` | `string` | yes | Stable identifier for one recommendation instance. Can be derived from source + type + effective period if no natural ID exists. |
| `sourceType` | `string` | yes | Canonical origin: `product`, `inventory`, `supplier`, `dashboard`, etc. |
| `sourceKey` | `string` | yes | Stable business key for the recommendation source. |
| `recommendationType` | `string` | yes | Canonical recommendation family, e.g. `REPLENISH`, `MARKDOWN`, `NEGOTIATE`, `SIGNAL_REVIEW`. |
| `confidenceLevel` | `high \| medium \| low \| insufficient_data` | yes | Human-readable confidence tier derived from evidence quality and confidence score. |
| `confidenceScore` | `number?` | recommended | Numeric 0-100 score when the module already has one. Nullable if the module cannot compute it yet. |
| `dataQualityStatus` | `string` | yes | Canonical trust state for the underlying data. |
| `warningCodes` | `string[]` | yes | Canonical warning and caveat codes that should be shown near the recommendation. |
| `primaryDrivers` | `string[]` | yes | Main explanation drivers, drawn from canonical set below. |
| `expectedImpactRsd` | `number?` | recommended | Expected financial impact in RSD. Nullable when denominator or input is missing. |
| `impactWindowDays` | `number?` | recommended | Time window over which the impact is expected. Nullable when not yet modeled. |
| `riskIfIgnored` | `string` | recommended | Short business-language explanation of downside if the recommendation is skipped. |
| `recommendedAction` | `string` | yes | Operator-facing action label or canonical action code. |
| `explainabilityText` | `string` | yes | Serbian business-language explanation that can be shown directly to users. |
| `generatedAtUtc` | `string` | yes | When the recommendation was produced. |
| `inputFreshnessStatus` | `string` | yes | Canonical freshness indicator for the inputs behind the recommendation. |

### Canonical `primaryDrivers`

Use these driver labels consistently across modules:

- `sales_velocity`
- `margin`
- `stock_risk`
- `trend`
- `supplier_reliability`
- `missing_cost`
- `sparse_sales`

Modules may add more specific internal drivers later, but these seven should remain the common vocabulary for Phase 1.

## Current module mapping

### 1. Product Decision Center

Current backend surface:

- `Api/Endpoints/CachedAnalyticsEndpoints.cs`
- `ProductDecisionCenterRowDto`

Current fields already available:

- `recommendationStatus`
- `recommendationLabel`
- `recommendationReason`
- `recommendedAction`
- `reasonCodes`
- `confidencePct`
- `reliabilityPct`
- `signalConfidencePct`
- `dataQualityStatus`
- `generatedAtUtc` on the enclosing response
- strong business signals such as `velocityUnitsPerDay`, `marginContribution`, `marginPct`, `stockGap`, `daysSinceLastSale`, `trendPct`, `lostSalesEstimate`, `stockCoverStatus`, `sellThroughStatus`

Current frontend surface:

- `Klijent/clientapp/src/types/analytics.ts`
- `ProductDecisionCenterItem`
- `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx`

Current gaps:

- no stable `recommendationId`
- no explicit `confidenceLevel`
- no explicit `primaryDrivers`
- no explicit `expectedImpactRsd` contract field separate from current local queue heuristics
- no explicit `impactWindowDays`
- no explicit `riskIfIgnored`
- no explicit `inputFreshnessStatus`
- `explainabilityText` exists only implicitly through `recommendationReason`

Phase 1 interpretation:

- Product Decision Center is the best first place to add the contract because it already has the richest evidence set and the strongest business recommendation semantics.
- It should become the reference implementation for other modules.

### 2. Inventory

Current backend surface:

- `Api/Dtos/InventoryListItemDto.cs`
- `Api/Endpoints/InventoryEndpoints.cs`

Current fields already available:

- `stockCoverStatus`
- `stockCoverStatusLabel`
- `sellThroughStatus`
- `sellThroughStatusLabel`
- `signalConfidencePct`
- `recommendationAllowed`
- `reasonCodes`
- `dataQualityStatus`
- inventory value inputs such as `estimatedValue`, `nabavnaCena`, `kolicina`, `minimalnaKolicina`

Current frontend surface:

- `Klijent/clientapp/src/pages/InventoryPage.tsx`
- `buildInventorySignalActionSpec(...)`

Current gaps:

- no stable `recommendationId`
- no explicit `confidenceLevel`
- `confidenceScore` is not yet canonicalized as a recommendation DTO field
- no explicit `primaryDrivers`
- no explicit `impactWindowDays`
- no explicit `riskIfIgnored`
- no explicit `explainabilityText`
- no explicit `inputFreshnessStatus`
- the UI currently uses heuristics for action creation and estimated impact rather than a shared recommendation contract

Phase 1 interpretation:

- Inventory should be second because it already has signal-level trust and action generation, but it still needs a cleaner confidence contract before it can be treated as a decision product.

### 3. Supplier

Current backend surface:

- `Api/Endpoints/SupplierDecisionHubEndpoints.cs`
- `SummarySupplierItem`
- `QuadrantItem`
- `RankingItem`
- `SupplierHeaderDto`
- `SupplierDecisionReportResponse`

Current fields already available:

- `RecommendationCode`
- `ConfidenceScore`
- `ReliabilityPct`
- `DataQualityStatus`
- `ReasonCodes`
- `StatusReason`
- `AiExplanation`
- `TopFeature1`, `TopFeature2`, `TopFeature3`
- `RecommendationAllowed` on report-level response
- `GeneratedAtUtc`, `PeriodFrom`, `PeriodTo`, `LastRefreshAtUtc`

Current frontend surface:

- `Klijent/clientapp/src/pages/SupplierDecisionHubPage.tsx`
- `Klijent/clientapp/src/pages/SupplierConsolidatedPage.tsx`

Current gaps:

- no stable `recommendationId`
- no explicit `confidenceLevel`
- no shared `sourceKey`
- no explicit `primaryDrivers` vocabulary
- no canonical `expectedImpactRsd`
- no canonical `impactWindowDays`
- no canonical `riskIfIgnored`
- no shared `explainabilityText` contract across list/detail/report layers
- no explicit `inputFreshnessStatus` field on the recommendation rows

Phase 1 interpretation:

- Supplier comes third because it already has useful confidence and explanation fields, but it is spread across multiple shapes and needs normalization after Product and Inventory are stabilized.

## Action Outcome Summary

The Action Outcome Summary endpoint is important, but it is a feedback loop, not the source of recommendation truth.

Canonical role:

- measure realized impact
- measure outcome coverage
- show warning codes when sample size is small or denominators are missing
- help validate whether the recommendation contract is performing well

Do not use it to invent recommendation confidence.

Recommended link:

- `GET /api/analytics/actions/outcomes/summary`

Relevant response concepts:

- `expectedImpactRsd`
- `measuredImpactRsd`
- `realizationRatio`
- `warningCodes`
- `sampleSize`
- `measuredSampleSize`

## Recommended field derivation

### Confidence level

Suggested derivation order:

1. `insufficient_data` if the data quality is insufficient or a required signal is missing.
2. `low` if evidence exists but is weak, sparse, or conflicting.
3. `medium` if the recommendation has usable support but still has caveats.
4. `high` only when the evidence is strong, fresh, and internally consistent.

### Confidence score

- Use backend-computed `confidenceScore` or `confidencePct` when available.
- If the backend cannot yet compute a numeric score, keep the field nullable rather than fabricating one in the UI.

### Primary drivers

Recommended mapping examples:

- sales velocity -> product movement, recent units sold, sell-through
- margin -> margin contribution, margin pct, supplier quality index
- stock risk -> stock gap, stock cover, OOS risk
- trend -> trend pct, seasonality, demand shift
- supplier reliability -> supplier quality index, reliability pct, repeated winner / markdown dependency
- missing cost -> missing or weak margin inputs
- sparse sales -> low sample size, low velocity, weak history

## Phase 1 implementation order

1. Product Decision Center
2. Inventory
3. Supplier

Reason:

- Product Decision Center already has the most complete recommendation semantics and should anchor the canonical contract.
- Inventory can follow using the same vocabulary with its own signal-specific nuance.
- Supplier should be normalized last because it already spans multiple DTO shapes and report surfaces.

## Test strategy

When implementation starts, add tests at three levels:

### 1. Contract tests

- verify confidence level mapping
- verify insufficient data never becomes high confidence
- verify nullable impact fields remain nullable when evidence is missing
- verify warnings appear near recommendation payloads

### 2. Backend DTO / endpoint tests

- Product Decision Center response exposes the contract consistently
- Inventory recommendation rows expose the same confidence vocabulary
- Supplier list/detail/report surfaces do not drift into a different naming scheme

### 3. Frontend rendering tests

- confidence label is rendered from backend values, not invented locally
- warning codes are visible next to the recommendation
- low / insufficient confidence does not visually read as “safe”
- empty or partial data states do not masquerade as confident recommendations

## Current gaps summary

### Product Decision Center

- needs stable recommendation identity
- needs canonical confidence level
- needs explicit primary drivers
- needs explicit impact window
- needs explicit risk text
- needs canonical freshness state

### Inventory

- needs canonical recommendation identity
- needs confidence level normalization
- needs primary drivers and risk text
- needs confidence/impact contract separate from heuristic queue builders

### Supplier

- needs a shared contract across summary, ranking, detail, and report views
- needs recommendation identity and source key consistency
- needs explicit confidence level and fresh input state

## Non-goals

- No new recommendation algorithm in this document.
- No broad auth or workflow redesign.
- No UI redesign.
- No fake precision where data is missing.

## Implementation note

The next implementation task should add Product Decision Center confidence first, using this document as the source of truth. After that, Inventory and Supplier can adopt the same contract without re-deciding the semantics.

