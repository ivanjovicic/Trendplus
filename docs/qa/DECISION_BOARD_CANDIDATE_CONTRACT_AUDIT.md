# Decision Board Candidate Contract Audit

Date: 2026-06-22T10:03:13+02:00
Local HEAD: `0ec51d512787399701eed0873c399d0f4f1566b3`

## Scope

- [docs/qa/DECISION_BOARD_BACKEND_AGGREGATE_GATE.md](./DECISION_BOARD_BACKEND_AGGREGATE_GATE.md)
- [docs/Analytics/EXECUTIVE_DECISION_BOARD_PLAN.md](../Analytics/EXECUTIVE_DECISION_BOARD_PLAN.md)
- [docs/Analytics/DECISION_CONFIDENCE_CONTRACT.md](../Analytics/DECISION_CONFIDENCE_CONTRACT.md)
- [Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx](../../Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx)
- [Klijent/clientapp/src/pages/__tests__/ExecutiveDecisionBoardPage.spec.ts](../../Klijent/clientapp/src/pages/__tests__/ExecutiveDecisionBoardPage.spec.ts)
- [Klijent/clientapp/src/types/analytics.ts](../../Klijent/clientapp/src/types/analytics.ts)
- [Klijent/clientapp/src/services/analyticsApi.ts](../../Klijent/clientapp/src/services/analyticsApi.ts)

## Goal

Document the exact candidate/card shape that the Executive Decision Board currently depends on, including:

- transport DTO shape
- render-model shape
- field ownership
- nullable behavior
- no-fake rules
- gaps that still block a stable backend aggregate contract

## Current State Summary

The board currently has two overlapping contract layers:

1. Active transport contract:
   - the page loads `DecisionBoardAggregateResponse` from `getDecisionBoardAggregate(...)`
   - this is the only runtime load path currently used by `ExecutiveDecisionBoardPage`
2. Local render contract:
   - the page maps API cards into a local `BoardCard` shape with derived UI fields such as `confidenceLabel`, `confidenceTone`, `actionCta`, `actionStateLabel`, and `sourceLink`
3. Shadow composition contract:
   - the same page still contains local candidate builder functions for product, inventory, supplier, blocker, action, and outcome cards
   - those builders are not part of the current runtime fetch path, but they still show what the frontend expects a candidate to contain

This means the repo does not yet have one clean candidate contract. It has:

- a transport DTO
- a render DTO
- leftover local normalization logic that still carries ranking, impact, and trust assumptions

That split is one of the reasons Q63 must remain blocked.

## Evidence From Current Tests

Current Executive Decision Board tests already lock several candidate-contract behaviors:

- missing expected impact stays `null`, not fake `0 RSD`
- `insufficient_data` stays visibly insufficient, not high-confidence
- stale and warning source states stay visible in the board model
- empty payload returns an empty model instead of fabricated content

These tests confirm the current board already relies on nullable and warning-rich candidates.

## Contract Layers

### 1. Transport layer: `DecisionBoardAggregateResponse`

Current API DTO fields:

| Field | Nullable | Meaning |
| --- | --- | --- |
| `generatedAtUtc` | no | Aggregate generation timestamp |
| `periodFromUtc` | yes | Shared board period start |
| `periodToUtc` | yes | Shared board period end |
| `lastRefreshAtUtc` | yes | Last refresh anchor |
| `overallDataQualityStatus` | no | Snapshot-level trust state |
| `recommendationNote` | no | Snapshot-level guidance |
| `warnings` | no | Snapshot-level warning codes |
| `metrics` | no | Summary counters |
| `sourceStates` | no | Source freshness/trust states |
| `sections` | no | Section list with cards |
| `meta` | yes | API meta contract |

This layer is close to a backend aggregate contract, but it is still not enough by itself because candidate-level semantics are not fully normalized.

### 2. Candidate layer: `DecisionBoardCard`

Current API card fields:

| Field | Nullable | Meaning |
| --- | --- | --- |
| `id` | no | Card instance identifier |
| `kind` | no | Candidate family: `product`, `inventory`, `supplier`, `blocker`, `action`, `outcome` |
| `sectionKey` | no | Section/lane key |
| `sourceModule` | no | Display source module |
| `sourceType` | yes | Source family key |
| `sourceKey` | yes | Source identity key |
| `title` | no | Card title |
| `summary` | yes | Short explanation |
| `confidenceLevel` | no | Confidence vocabulary, but still open-ended string |
| `confidenceScore` | yes | Numeric confidence |
| `reliabilityPct` | yes | Reliability score if available |
| `expectedImpactRsd` | yes | Expected business impact |
| `measuredImpactRsd` | yes | Measured impact if available |
| `realizationRatio` | yes | Outcome realization ratio if available |
| `riskIfIgnored` | no | Business downside |
| `recommendedNextAction` | no | Next operator action |
| `actionHref` | no | Workflow target |
| `alreadyInAction` | no | Open workflow flag |
| `alreadyClosed` | no | Closed workflow flag |
| `warningCodes` | no | Candidate warning codes |
| `dataQualityStatus` | no | Candidate trust status |
| `generatedAtUtc` | yes | Candidate timestamp |
| `priorityScore` | no | Ordering score |
| `impactScore` | no | Numeric impact sort aid |

This is the closest thing to the current candidate contract.

### 3. Render layer: local `BoardCard`

`ExecutiveDecisionBoardPage.tsx` maps API cards into a local `BoardCard` that adds UI-only fields:

| Local field | Source | Nullable | Keep in backend contract? |
| --- | --- | --- | --- |
| `confidenceLabel` | derived from `confidenceLevel`, `confidenceScore`, `dataQualityStatus` | no | no, UI-only |
| `confidenceTone` | derived from `confidenceLevel`, `confidenceScore`, `dataQualityStatus` | no | no, UI-only |
| `actionCta` | derived from `alreadyInAction` / `alreadyClosed` | no | no, UI-only |
| `sourceLink` | copied from section-level `sourceLink` | no | not as-is; candidate needs explicit origin link |
| `actionStateLabel` | derived from action booleans | no | no, UI-only |

The render layer proves the backend card contract is not yet self-sufficient:

- confidence presentation is still reinterpreted in the browser
- source navigation is still section-derived, not candidate-owned
- workflow CTA copy is still generated locally

## Candidate Field Ownership

### Required candidate fields already expected by the board

| Candidate field | Current owner | Notes |
| --- | --- | --- |
| `id` | backend aggregate card | Required, but currently card-instance oriented |
| `kind` | backend aggregate card | Required for lane rendering |
| `sectionKey` | backend aggregate card | Required for lane placement |
| `sourceModule` | backend aggregate card | Used directly in UI |
| `title` | backend aggregate card | Used directly in UI |
| `riskIfIgnored` | backend aggregate card | Used directly in UI |
| `recommendedNextAction` | backend aggregate card | Used directly in UI |
| `actionHref` | backend aggregate card | Used directly in UI |
| `alreadyInAction` | backend aggregate card | Used to derive CTA/state copy |
| `alreadyClosed` | backend aggregate card | Used to derive CTA/state copy |
| `warningCodes` | backend aggregate card | Used directly in UI |
| `dataQualityStatus` | backend aggregate card | Used directly and for confidence fallback |
| `priorityScore` | backend aggregate card | Used as already-computed ordering signal |
| `impactScore` | backend aggregate card | Used as numeric companion for ranking |

### Optional but already consumed candidate fields

| Candidate field | Current owner | Notes |
| --- | --- | --- |
| `sourceType` | backend aggregate card | Needed for source identity, but currently nullable |
| `sourceKey` | backend aggregate card | Needed for source identity, but currently nullable |
| `summary` | backend aggregate card | Optional display copy |
| `confidenceScore` | backend aggregate card | Numeric confidence input |
| `reliabilityPct` | backend aggregate card | Present in DTO, but currently not rendered in the board |
| `expectedImpactRsd` | backend aggregate card | Must stay nullable |
| `measuredImpactRsd` | backend aggregate card | Optional outcome detail |
| `realizationRatio` | backend aggregate card | Optional outcome detail |
| `generatedAtUtc` | backend aggregate card | Optional freshness anchor |

### Derived frontend-only fields

| Local field | How it is derived | Why it matters |
| --- | --- | --- |
| `confidenceLabel` | from `confidenceLevel`, `confidenceScore`, `dataQualityStatus` | Shows the backend contract is not yet presentation-complete |
| `confidenceTone` | from `confidenceLevel`, `confidenceScore`, `dataQualityStatus` | Warning vs insufficient is still normalized locally |
| `actionCta` | from `alreadyInAction` / `alreadyClosed` | Current backend does not express CTA semantics directly |
| `actionStateLabel` | from action booleans | Workflow state wording still lives in frontend |
| `sourceLink` | inherited from section | Candidate cannot currently point to its own exact source row/view |

## Source Module Mapping

### Active runtime mapping

The runtime page currently expects the backend aggregate endpoint to deliver already-composed candidates for these source families:

| Candidate kind | Current expected source family | Minimum fields the board expects |
| --- | --- | --- |
| `product` | Product Decision Center | confidence, nullable impact, warning codes, risk, next action |
| `inventory` | Inventory decision surfaces | confidence, nullable impact, stock risk reason, action state |
| `supplier` | Supplier decision surfaces | confidence, trust warning, action state |
| `blocker` | Pilot readiness, data quality, refresh, dashboard trust | warning codes, trust status, risk, next action |
| `action` | Analytics Actions | workflow state, impact, confidence |
| `outcome` | Action outcome summary / action outcomes | expected vs measured impact, sample warnings |

### Shadow composition mapping still present in the page

`ExecutiveDecisionBoardPage.tsx` still contains local builder functions for:

- `buildProductCards(...)`
- `buildInventoryCards(...)`
- `buildSupplierCards(...)`
- `buildActionCards(...)`
- `buildOutcomeCards(...)`
- `buildBlockerCards(...)`

These functions are useful audit evidence because they reveal the frontend's historical assumptions:

- product cards fall back to `lostSalesEstimate` when `expectedImpactRsd` is missing
- inventory cards fall back to `estimatedValueAmount` / `estimatedValue`
- supplier cards still use revenue as a rough impact companion and keep `expectedImpactRsd` null
- blocker cards synthesize trust blockers from refresh, intake, health, and dashboard freshness sources
- outcome cards treat small samples and warning-heavy outcomes as `insufficient_data` or helper signal, not strong confidence

They are not the current active fetch path, but they still encode candidate expectations that any future aggregate contract must either preserve or deliberately replace.

## Nullable Fields That Must Stay Nullable

These fields must not be normalized into fake values:

| Field | Why nullable matters |
| --- | --- |
| `sourceType` | Dedupe/source identity cannot be faked if source lineage is missing |
| `sourceKey` | Must remain missing when lineage is missing; fake keys would hide collisions |
| `summary` | Empty summary is safer than invented explanation |
| `confidenceScore` | Missing numeric confidence must not become `0` or be displayed as strong certainty |
| `reliabilityPct` | Missing reliability must stay unavailable |
| `expectedImpactRsd` | Missing impact must stay `null`, never fake `0 RSD` |
| `measuredImpactRsd` | Missing measurement must stay `null` |
| `realizationRatio` | Missing outcome ratio must stay `null` |
| `generatedAtUtc` | Missing freshness timestamp must stay unknown |
| `periodFromUtc` / `periodToUtc` / `lastRefreshAtUtc` | Board window/freshness must stay nullable when not known |

## No-Fake Rules The Candidate Contract Must Preserve

1. `expectedImpactRsd` must remain nullable when impact evidence is missing.
2. `confidenceScore` must remain nullable when the source does not send a real numeric confidence value.
3. `confidenceLevel=insufficient_data` must stay visually insufficient even if another field is present.
4. `warningCodes` must stay candidate-visible, not metadata-only.
5. `alreadyInAction` and `alreadyClosed` must not imply a fresh recommendation if the candidate actually points to an existing workflow item.
6. Outcome feedback must not become recommendation confidence by itself.
7. Missing source identity must stay explicit; the board must not invent dedupe-safe keys.

## Current Gaps That Block A Stable Backend Aggregate Contract

### 1. Card identity is not the same as recommendation identity

Current `id` is a card-instance identifier, not a canonical recommendation identity.

Missing:

- `recommendationId`
- lane-independent identity
- explicit repeated-card semantics for same recommendation in multiple sections

### 2. Source identity is optional, but dedupe needs it to be mandatory

`sourceType` and `sourceKey` are nullable today.

That is not strong enough for:

- dedupe rules
- repeated-card detection
- action lineage
- aggregate parity tests

### 3. Candidate freshness is too thin

Current candidate freshness is mostly:

- `generatedAtUtc`
- section/source-state freshness

Missing:

- per-candidate `inputFreshnessStatus`
- freshness provenance
- stale-vs-partial semantics attached directly to each candidate

### 4. Confidence semantics are still partially normalized in frontend

The page still derives:

- confidence label
- confidence tone
- insufficient-data downgrade behavior

This means the candidate contract is not yet self-describing enough to remove frontend reinterpretation.

### 5. Origin navigation is still section-owned, not candidate-owned

`sourceLink` is not part of the transport card. The local render model takes it from the section.

That is too weak for a stable aggregate because:

- one section can contain candidates from multiple exact source views
- the aggregate should not assume one generic link is good enough for every card

### 6. Warning severity and provenance are still under-specified

`warningCodes` exist, but the contract still lacks:

- warning severity
- whether the warning is source-level or candidate-level
- whether the warning blocks action, lowers confidence, or is informational only

### 7. Impact provenance is inconsistent across sources

The shadow composition functions still show different fallback logic for impact:

- product: `expectedImpactRsd` or `lostSalesEstimate`
- inventory: action spec impact or estimated inventory value
- supplier: impact often unavailable
- blockers: impact mostly null, sometimes proxy metrics
- actions/outcomes: expected vs measured impact from workflow data

Without a canonical impact provenance field, backend aggregate parity will stay ambiguous.

### 8. Action semantics are still represented as booleans plus local copy

Current cards expose:

- `alreadyInAction`
- `alreadyClosed`

The UI then derives labels and CTA wording locally.

Missing:

- canonical action state enum
- explicit workflow meaning for each state
- explicit repeated-card behavior when a recommendation is already in action

### 9. Shadow composition logic still lives beside the aggregate model

The file still contains local builder and scoring functions even though runtime currently loads the aggregate endpoint.

That creates a contract ambiguity:

- which fields are truly required by the aggregate
- which assumptions are legacy
- which ranking and impact derivations are still acceptable

Q63 should not proceed until that ambiguity is resolved by later blocker tasks.

## Recommended Contract Target For Later Tasks

The eventual backend-safe candidate contract should add or lock these fields:

- `recommendationId`
- mandatory `sourceType`
- mandatory `sourceKey`
- `recommendationType`
- `primaryDrivers`
- `inputFreshnessStatus`
- candidate-level `sourceLink`
- candidate-level warning semantics
- canonical action state
- impact provenance or basis metadata

These are not implementation instructions for Q63. They are blockers that Q63A makes explicit for Q63B-Q63F.

## Conclusion

The current board already relies on a rich candidate shape, but that shape is split across:

- the transport DTO
- the local render model
- older composition helpers still present in the page

Because of that split, the repo still lacks one clean, frozen candidate contract that a backend aggregate endpoint can safely promise.

That is the correct reason Q63 remains blocked.
