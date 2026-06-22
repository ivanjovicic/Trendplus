# Decision Board Freshness and Warning Contract

Date: 2026-06-22
Local HEAD: `e5273580e9709ea432f403bb502031ee2fc9ddcd`

## Scope

- [docs/qa/DECISION_BOARD_BACKEND_AGGREGATE_GATE.md](./DECISION_BOARD_BACKEND_AGGREGATE_GATE.md)
- [docs/qa/DECISION_BOARD_CANDIDATE_CONTRACT_AUDIT.md](./DECISION_BOARD_CANDIDATE_CONTRACT_AUDIT.md)
- [docs/qa/DECISION_BOARD_DEDUPE_RULES.md](./DECISION_BOARD_DEDUPE_RULES.md)
- [docs/qa/DECISION_BOARD_RANKING_PARITY_PLAN.md](./DECISION_BOARD_RANKING_PARITY_PLAN.md)
- [docs/Analytics/DECISION_CONFIDENCE_CONTRACT.md](../Analytics/DECISION_CONFIDENCE_CONTRACT.md)
- [Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx](../../Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx)
- [Klijent/clientapp/src/types/analytics.ts](../../Klijent/clientapp/src/types/analytics.ts)
- [Klijent/clientapp/src/services/supplierDecisionHubApi.ts](../../Klijent/clientapp/src/services/supplierDecisionHubApi.ts)

## Goal

Define the trust/freshness/warning contract that the Executive Decision Board needs before a backend aggregate endpoint can safely centralize composition.

This document covers:

- snapshot-level freshness and warning semantics
- source/module-level freshness and warning semantics
- candidate-level trust metadata
- current UI behavior
- missing fields and contract gaps

Q63 remains blocked. This task does not implement the aggregate endpoint.

## Current Contract Layers

The board currently has three trust layers:

### 1. Snapshot / aggregate layer

Current `DecisionBoardAggregateResponse` trust fields:

- `generatedAtUtc`
- `periodFromUtc`
- `periodToUtc`
- `lastRefreshAtUtc`
- `overallDataQualityStatus`
- `warnings`
- `meta`

This layer decides whether the whole board is fresh, partial, empty, or warning-like.

### 2. Source / module layer

Current `DecisionBoardSourceState` trust fields:

- `sourceKey`
- `displayName`
- `status`
- `generatedAtUtc`
- `warningCodes`
- `message`
- `sourceLink`

This layer is supposed to tell the board which input module is stale, warning-like, critical, or unknown.

### 3. Candidate / card layer

Current `DecisionBoardCard` trust fields:

- `confidenceLevel`
- `confidenceScore`
- `warningCodes`
- `dataQualityStatus`
- `generatedAtUtc`

This layer tells the operator whether one specific decision candidate is trustworthy enough to act on.

## Current UI Consumption

The current page uses trust data in these ways:

### Snapshot-level consumption

- `payload.meta?.isPartial`
- `payload.warnings`
- `payload.overallDataQualityStatus`
- `payload.meta?.emptyReason`
- `payload.lastRefreshAtUtc`

These flow into:

- `AnalyticsTrustHeader`
- global `isPartial`
- empty-state reason
- partial-note banner

### Source-level consumption

- `payload.sourceStates.find((state) => state.sourceKey === "refresh-status")`

This currently affects:

- trust-header `dataFreshnessStatus`

Important limitation:

- source states are not rendered as a full visible source-status table on the page
- most source-state warning detail is collapsed into the derived `isPartial` boolean

### Candidate-level consumption

- card warning chips render `warningCodes`
- card footer renders `dataQualityStatus`
- confidence presentation is derived from:
  - `confidenceLevel`
  - `confidenceScore`
  - `dataQualityStatus`

Important limitation:

- there is no explicit candidate-level `inputFreshnessStatus`
- warning provenance is not shown
- section-level warnings are not preserved in the local render model

## Current Partial / Warning Logic

The local board model currently marks the board as partial when **any** of these are true:

- `payload.meta?.isPartial`
- `payload.warnings.length > 0`
- any `sourceState.status` is not `good`, `fresh`, or `excellent`
- `overallDataQualityStatus !== "good"`

This is appropriately conservative, but it also means:

- warning richness is reduced to one boolean in the local model
- the aggregate UI does not distinguish enough between:
  - stale
  - partial
  - critical
  - insufficient data
  - unknown

That is one of the central freshness-contract gaps.

## Source Module Mapping

### 1. Refresh / worker status

Current source:

- `AnalyticsRefreshStatus`

Current trust fields:

- `dataFreshnessStatus`
- `lastSuccessfulRefreshAtUtc`
- `lastAttemptAtUtc`
- `lastFailureAtUtc`
- `lastErrorMessage`
- `workerWarning`
- `workerProcessWarning`
- `cacheWarning`
- `generatedAtUtc`

Current board use:

- creates blocker card when freshness is `stale` or `critical`
- contributes `refresh-status` source state at aggregate level
- drives trust-header freshness if present

Contract requirement:

- refresh freshness must remain visible both as source-state metadata and candidate/blocker metadata when it blocks trust

### 2. Pilot readiness / intake report

Current source:

- `PilotDataQualityIntakeReport`

Current trust fields:

- `generatedAtUtc`
- `lastImportAtUtc`
- `lastRefreshAtUtc`
- `readinessStatus`
- `readinessLabel`
- `meta`
- issue counts and impact counts:
  - `missingCostCount`
  - `missingSupplierCount`
  - `insufficientSignalCount`

Current board use:

- generates blocker cards for:
  - missing cost
  - missing supplier
  - insufficient signal / critical readiness

Contract requirement:

- readiness-derived blockers must keep:
  - candidate warning code
  - candidate data-quality status
  - generated timestamp
  - upstream source freshness

Current gap:

- blocker cards expose only `generatedAtUtc`
- they do not carry explicit `inputFreshnessStatus`

### 3. Data quality health

Current source:

- `AnalyticsDataQualityHealth`

Current trust fields:

- `generatedAt`
- `scoreStatus`
- `scoreSummary`
- `meta`
- cost/supplier impact shares

Current board use:

- builds a blocker card when status is not `excellent` or `good`
- uses warning codes such as:
  - `missing_cost`
  - `missing_supplier`

Contract requirement:

- health score severity must remain explicit
- aggregate should not flatten `warning` and `critical` into the same generic source note

### 4. Dashboard freshness

Current source:

- `AnalyticsDashboardBootstrap.validationFreshness`
- `AnalyticsDashboardBootstrap.meta`

Current trust fields:

- `validationFreshness.status`
- `validationFreshness.message`
- `validationFreshness.score`
- `meta.generatedAtUtc`

Current board use:

- builds a blocker card when dashboard freshness is not `good`
- uses warning code `freshness`

Contract requirement:

- dashboard freshness must remain distinguishable from worker refresh freshness
- they are related but not identical trust failures

### 5. Product decisions

Current source:

- `ProductDecisionCenterResponse`
- `ProductDecisionCenterItem`

Current trust fields already available:

- `generatedAtUtc` on response
- per-item:
  - `dataQualityStatus`
  - `confidenceLevel`
  - `confidenceScore`
  - `warningCodes`
  - `reasonCodes`
  - `inputFreshnessStatus`

Current board use:

- active aggregate card DTO consumes candidate warning codes and `dataQualityStatus`
- shadow composition helper merges `warningCodes + reasonCodes`
- current board contract does **not** preserve item `inputFreshnessStatus`

Contract requirement:

- product candidates must carry:
  - source freshness status
  - candidate warning codes
  - trust status
  - generated timestamp

Current gap:

- `DecisionBoardCard` does not expose `inputFreshnessStatus`

### 6. Inventory

Current source:

- `InventoryInsights`
- inventory rows used by `buildInventorySignalActionSpec(...)`

Current trust fields already available:

- `InventoryInsights.meta`
- per-row:
  - `dataQualityStatus`
  - `reasonCodes`
  - `signalConfidencePct`
  - `recommendationAllowed`

Current board use:

- shadow composition helper turns `reasonCodes` into candidate warnings
- freshness is inherited weakly from response meta only

Contract requirement:

- inventory candidates need explicit freshness propagation, not only response-level metadata

Current gap:

- no explicit inventory candidate `inputFreshnessStatus`
- no explicit warning provenance beyond reason codes

### 7. Supplier

Current source:

- `SummaryResponse`
- `ScorecardTrustMetadata`
- `SummarySupplierItem`

Current trust fields already available:

- `trustMetadata.dataCoverageStatus`
- `trustMetadata.lastRefreshAtUtc`
- `trustMetadata.usedFallback`
- `trustMetadata.fallbackReason`
- `trustMetadata.recommendationAllowed`
- `meta`

Current board use:

- supplier cards currently inherit trust mostly from `trustMetadata.dataCoverageStatus`
- warning codes are currently synthesized as:
  - `[dataCoverageStatus]` when status is not good

Contract requirement:

- supplier candidate freshness must capture:
  - data coverage status
  - fallback usage
  - fallback reason
  - last refresh anchor

Current gaps:

- warning codes are too coarse
- fallback is not carried as explicit candidate warning metadata in the board card
- no explicit candidate freshness field

### 8. Action queue

Current source:

- `AnalyticsActionListResponse`
- `AnalyticsActionItem`

Current trust fields already available:

- `dataQualityStatus`
- `confidencePct`
- `reliabilityPct`
- `updatedAtUtc`
- workflow status

Current board use:

- action cards use `dataQualityStatus` as warning code when present
- action cards use `updatedAtUtc` as generated timestamp

Contract requirement:

- action cards should preserve both workflow state and trust state

Current gap:

- action trust warnings are flattened into `[dataQualityStatus]`
- no explicit distinction between stale action evidence and stale upstream recommendation evidence

### 9. Action outcome summary / outcomes

Current source:

- `AnalyticsActionOutcomeSummaryResponse`
- `AnalyticsActionOutcomeSummaryMeta`
- `AnalyticsActionItem` for pending outcomes

Current trust fields already available:

- summary meta:
  - `generatedAtUtc`
  - `sampleSize`
  - `measuredSampleSize`
  - `warnings`
  - `emptyReason`
- action item:
  - `outcomeStatus`
  - `dataQualityStatus`
  - `updatedAtUtc`

Current board use:

- summary warning codes come from `meta.warnings`
- small sample size forces warning/insufficient semantics
- pending outcome rows use `outcomeStatus` as warning code

Contract requirement:

- learning feedback trust must remain visible as sample quality, not recommendation certainty

Current gap:

- no explicit freshness contract for outcome measurement evidence
- sample warning provenance is not structured beyond string codes

## Snapshot-Level Contract Requirements

The aggregate snapshot should always preserve:

- `generatedAtUtc`
- `periodFromUtc`
- `periodToUtc`
- `lastRefreshAtUtc`
- `overallDataQualityStatus`
- `warnings`
- `meta.success`
- `meta.isPartial`
- `meta.emptyReason`

Recommended additions or lock-ins:

- explicit snapshot freshness status
- explicit snapshot invalidation reason when one source is critically stale
- explicit snapshot warning severity model

## Source-Level Contract Requirements

Every board source should be mappable into `DecisionBoardSourceState` with:

- stable `sourceKey`
- display name
- status
- `generatedAtUtc`
- `warningCodes`
- optional message
- optional `sourceLink`

Recommended additions:

- `lastRefreshAtUtc`
- `lastSuccessfulRefreshAtUtc`
- `warningSeverity`
- `isBlocking`
- `isPartial`
- `freshnessReason`

Current gap:

- the source-state DTO is too thin to explain whether a status is stale because of worker drift, fallback coverage, partial data, or unknown lineage

## Candidate-Level Contract Requirements

Every board candidate should preserve:

- `dataQualityStatus`
- `warningCodes`
- `generatedAtUtc`
- `confidenceLevel`
- `confidenceScore`

Recommended additions:

- `inputFreshnessStatus`
- `sourceRecommendationId`
- `warningSeverity`
- `warningSource`
- `isBlocking`

Current gap:

- candidate warning source and freshness source are not explicit
- candidate freshness cannot currently be compared cleanly across modules

## UI Behavior Rules

### Snapshot rules

1. If aggregate load fails:
   - show `AnalyticsErrorState`
   - do not render fake board data
2. If the board is empty but successful:
   - show `AnalyticsEmptyState`
   - use `meta.emptyReason` if present
3. If the board is partial:
   - show trust-header partial state
   - show the partial note
   - keep warnings visible

### Source rules

1. If a source is stale or critical:
   - it must influence trust-header freshness
   - it must also remain inspectable as source/module evidence
2. Source-level warnings must not disappear into one aggregate boolean only.

### Candidate rules

1. Candidate warning codes must remain visible near the card.
2. Candidate stale/partial/insufficient states must not render as green confidence.
3. Missing freshness data must render as unknown, not fresh.
4. Missing impact due to freshness/quality gaps must remain nullable.

## Current Rendering Gaps

These are the most important frontend/render contract gaps visible today:

### 1. Section warnings are dropped

Transport `DecisionBoardSection` includes:

- `warnings: string[]`

Local `BoardSection` in `ExecutiveDecisionBoardPage.tsx` does not preserve them.

Impact:

- section-specific warning semantics are lost before render

### 2. Source states are under-rendered

The board currently uses source states mostly to:

- derive `isPartial`
- set trust-header freshness from `refresh-status`

Impact:

- module-level freshness/warning semantics are not visible enough for operators

### 3. Candidate freshness is missing

The candidate DTO and local render model do not expose:

- `inputFreshnessStatus`

Impact:

- cards can show warning codes and data-quality states without clearly stating the freshness of their own evidence

### 4. Warning provenance is missing

The board currently has warning strings, but not enough structure to say:

- source-level warning
- section-level warning
- candidate-level warning
- snapshot-level warning

Impact:

- aggregate parity would be ambiguous

### 5. Unknown vs warning vs partial is not fully separated

The local model compresses several trust states into:

- `model.isPartial`
- `overallDataQualityStatus`

Impact:

- the UI cannot cleanly distinguish:
  - unknown freshness
  - stale source
  - partial source failure
  - broad warning state

## Backend Aggregate Requirements Before Q63 Can Be Revisited

A future aggregate endpoint must preserve:

1. snapshot-level warnings separately from source-level warnings
2. source-level warnings separately from candidate warning codes
3. per-candidate trust metadata, including freshness
4. explicit partial-failure semantics when one module fails
5. explicit invalidation semantics when freshness is too old to trust the whole board

At minimum, the future backend-safe contract should support:

- aggregate:
  - `overallDataQualityStatus`
  - `warnings`
  - `generatedAtUtc`
  - `lastRefreshAtUtc`
  - `meta.isPartial`
  - `meta.emptyReason`
- source:
  - `status`
  - `generatedAtUtc`
  - `warningCodes`
  - freshness reason
  - optional blocking flag
- candidate:
  - `dataQualityStatus`
  - `warningCodes`
  - `generatedAtUtc`
  - `inputFreshnessStatus`
  - explicit blocking/warning severity if applicable

## Conclusion

The board already has freshness and warning information in the repo, but it is split across:

- aggregate snapshot metadata
- source states
- candidate warning codes
- source-specific DTOs that still expose richer trust context than the final board card carries

Before Q63 can move forward, Trendplus needs a contract that preserves those trust layers explicitly instead of collapsing them into:

- one partial boolean
- one overall status
- a few warning chips
