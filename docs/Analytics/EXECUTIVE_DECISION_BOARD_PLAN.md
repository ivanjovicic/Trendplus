# Executive Decision Board Plan

Updated: 2026-06-19

## Purpose

Trendplus needs one executive board that answers the simplest and most important question:

- What should I do first?

The board is not a new analytics algorithm. It is a read layer that composes the strongest existing decision surfaces into one prioritized view.

## Board route

- `/analytics/decision-board`

## Board goals

1. Show the top prioritized analytics decisions in one place.
2. Explain why each decision matters.
3. Make expected impact, confidence and risk visible together.
4. Show what is already in action and what is still waiting for a decision or an outcome.
5. Keep `insufficient_data`, stale data and missing denominator states honest.

## Core principle

The board must reuse backend truth from existing analytics modules.

It must not:

- invent confidence in the browser
- invent `0 RSD` when impact is missing
- hide stale or low-quality data behind a green card
- rank weak evidence as if it were highly reliable

## Primary source modules

The board should compose from these current surfaces:

| Module | What it contributes |
|---|---|
| `AnalyticsDashboard` | executive health, refresh context, macro trends |
| `PilotReadinessPage` | readiness, blockers, data quality, trust warnings |
| `ProductDecisionCenterPage` | highest-value product recommendations |
| `SupplierConsolidatedPage` | supplier risk/opportunity decisions |
| `InventoryPage` | stock risk, replenish, dead stock, transfer decisions |
| `AnalyticsActionsPage` | actions awaiting decision and action backlog |
| Action Outcome Summary | outcome coverage, expected vs measured impact, learning feedback |

## Board sections

### 1. Top 5 urgent decisions

Purpose:

- show the most urgent actions that need attention first

Typical source mix:

- Product Decision Center
- Inventory
- Supplier
- Data quality blockers
- Open action queue items

Prioritization:

- blocking data quality can outrank raw impact
- overdue or aging actions can outrank newer actions
- `insufficient_data` cannot rank as a high-confidence urgent recommendation

### 2. Highest expected impact

Purpose:

- show where the biggest business gain is expected if the operator acts

Typical source mix:

- Product Decision Center
- Inventory
- Supplier

Display rules:

- show `expectedImpactRsd` only when the backend provides it or when the source has a reliable basis
- if impact is missing, show a warning and a nullable value, never a fake zero

### 3. Stock risk decisions

Purpose:

- make replenish, out-of-stock risk, dead stock and transfer decisions immediately obvious

Typical source mix:

- InventoryPage
- ProductDecisionCenterPage for stock-driven decisions

Typical cards:

- replenish now
- high OOS risk
- slow moving or dead stock
- rebalancing / transfer

### 4. Supplier risk/opportunity decisions

Purpose:

- show supplier negotiations, concentration risk and opportunity decisions

Typical source mix:

- SupplierConsolidatedPage
- supplier decision hub / scorecard data

Typical cards:

- negotiate
- protect margin
- reduce dependency
- review low-confidence supplier signals

### 5. Data quality blockers

Purpose:

- show what is preventing confident decisions

Typical source mix:

- PilotReadinessPage
- dashboard trust/freshness signals

Typical cards:

- missing cost
- missing supplier
- insufficient signal
- stale refresh
- blocked recommendation cohort

### 6. Actions awaiting decision

Purpose:

- show open actions that still need human decision

Typical source mix:

- AnalyticsActionsPage

Typical statuses:

- `new`
- `accepted`
- `deferred`

Interpretation:

- these are already actionable items, but they are not yet closed-loop outcomes

### 7. Actions awaiting outcome

Purpose:

- show actions that were handled but still need learning feedback

Typical source mix:

- AnalyticsActionsPage
- Action Outcome Summary

Typical states:

- `pending`
- `not_measured`
- overdue measurement

Interpretation:

- these are not failures
- they are unresolved learning items
- they should stay visible until measured or explicitly marked not measurable

## Standard card shape

Every executive board card should contain:

| Field | Required | Meaning |
|---|---:|---|
| `title` | yes | Short business-facing decision label |
| `sourceModule` | yes | Which existing module produced the card |
| `confidenceLevel` | yes | `high`, `medium`, `low`, `insufficient_data` |
| `confidenceScore` | recommended | Numeric score only if backend already provides it |
| `expectedImpactRsd` | recommended | Nullable if input or denominator is missing |
| `riskIfIgnored` | yes | Short explanation of downside |
| `recommendedNextAction` | yes | Concrete next step for the operator |
| `sourceLink` | yes | Link back to the originating screen |
| `actionCta` | yes | Open, continue, review, accept, defer, measure, or resolve |
| `warningCodes` | yes | Caveats and trust warnings |
| `generatedAtUtc` | yes | Freshness anchor |
| `dataQualityStatus` | yes | Trust state for the signal |
| `alreadyInAction` | yes | Whether there is already a linked action item |

### Card copy rules

- title should be short and operational
- `riskIfIgnored` must read like business guidance, not a model explanation
- `recommendedNextAction` should tell the operator what to do next, not what the model did
- `sourceLink` should land on the screen where the underlying decision can be inspected

## Prioritization rules

The board should rank cards using these rules:

1. Safety and blockers first.
2. High-impact, high-confidence opportunities next.
3. Actions already in progress, but not yet resolved.
4. Lower-confidence or exploratory items last.

### Detailed ranking guardrails

- `insufficient_data` must never be treated as top high-confidence.
- Warnings lower confidence unless there is an explicit business override.
- Stale data must always be visible as a warning.
- Missing impact denominator means nullable impact, not zero.
- A blocked decision can outrank a large but noisy opportunity.
- Already-in-action items should surface their current workflow state so the board does not suggest duplicate effort.

### Suggested ranking inputs

Use a small composition score built from existing backend values:

- confidence level / confidence score
- expected impact
- freshness
- urgency or overdue state
- warning severity
- open action age
- data quality severity

The frontend may use these inputs only for ordering and grouping.
It must not manufacture backend-like recommendation semantics.

## Phase 1 implementation option

### Frontend composition only

Phase 1 can build the board entirely from existing endpoints:

- Dashboard bootstrap and refresh metadata
- Pilot readiness and data quality endpoints
- Product Decision Center endpoint
- Inventory endpoint
- Supplier consolidated / decision hub endpoints
- Action list endpoint
- Action Outcome Summary endpoint

Benefits:

- smallest change
- no new backend contract required
- easy to ship incrementally

Limits:

- the board will depend on multiple requests
- ranking logic lives partly in frontend composition
- some cards will be assembled from multiple source views

### Suggested Phase 1 behavior

- render the board shell with trust header and freshness context
- fetch existing module data in parallel
- normalize each item into a shared executive card model
- deduplicate obvious overlaps where the same source already appears in another lane
- show section-level empty states when a lane has no qualifying items

## Phase 2 implementation option

### Backend aggregate endpoint

Later, the board can move to a dedicated aggregate endpoint such as:

- `GET /api/analytics/decision-board`

Possible server-side benefits:

- one network call
- consistent ranking logic
- simpler frontend rendering
- centralized trust and warning semantics

Phase 2 should only be added after Phase 1 proves the section model and ranking rules are stable.

### Backend aggregate contract sketch

The backend aggregate should stay read-only and should not replace Phase 1 until the board card model is stable.

Suggested response shape:

| Field | Purpose |
|---|---|
| `sections` | Seven board lanes with cards already ranked server-side |
| `metrics` | Executive summary counters for urgent, impact, blockers and action states |
| `generatedAtUtc` | Freshness anchor for the whole board snapshot |
| `periodFrom` / `periodTo` | Shared decision window for all lanes |
| `overallDataQualityStatus` | Combined trust status for the snapshot |
| `recommendationNote` | Short explanation of board composition and fallback behavior |
| `warnings` | Snapshot-level warning codes if some lanes are partial or stale |

Recommended server-side responsibilities:

- compose from the same source modules used in Phase 1
- preserve nullable confidence and nullable impact
- keep stale data and data-quality blockers visible
- deduplicate overlapping cards from the same source key
- rank cards with a small shared ordering function rather than ad hoc frontend sorting

Recommended server-side tests:

- aggregate endpoint returns all seven sections
- urgent lane still prioritizes blockers and strong opportunities
- `insufficient_data` and missing impact remain nullable, not zero
- action-linked cards keep `alreadyInAction` / `alreadyClosed` states
- response metadata stays honest when one source is partial or missing
- source link mapping still points back to the originating screen

## No-fake rules

1. Never show `0 RSD` for unknown impact.
2. Never convert `null` confidence into a confident-looking UI state.
3. Never hide stale refresh or data quality blockers.
4. Never promote `insufficient_data` to a top high-confidence card.
5. Never use Action Outcome Summary as the source of recommendation confidence.
6. Never let an already-open action appear as a fresh new decision without linking it back to the existing action item.

## Tests needed

### Frontend tests

- board groups cards into the seven sections correctly
- top urgent lane does not rank `insufficient_data` as high confidence
- stale data appears with warning copy
- missing impact does not render a fake zero
- cards link back to the correct source screen
- create / continue action CTA appears only when a linked action is appropriate
- already-in-action cards show their current workflow state

### Backend tests for Phase 2

- aggregate endpoint returns the expected lane model
- summary values match the underlying module endpoints
- warning and trust semantics remain nullable where needed
- no fake zero / no fake confidence behavior stays intact

## Suggested card examples

### Product decision

- Title: `Dopuni top prodajni artikal`
- Source module: `ProductDecisionCenter`
- Confidence: `high`
- Expected impact: nullable if missing denominator
- Risk if ignored: `gubitak prodaje i rast stock-out rizika`

### Inventory decision

- Title: `Rebalansiraj spor artikl`
- Source module: `Inventory`
- Confidence: `medium`
- Expected impact: derived from inventory value and risk profile
- Risk if ignored: `zamrznuta zaliha i slab obrt`

### Supplier decision

- Title: `Pregledaj dobavljača sa margin risk signalom`
- Source module: `SupplierConsolidated`
- Confidence: `low` or `medium` depending on evidence
- Expected impact: nullable unless the source already provides it
- Risk if ignored: `pad marže ili zavisnost od slabog dobavljača`

### Data quality blocker

- Title: `Dostavi nabavnu cenu`
- Source module: `PilotReadiness`
- Confidence: `insufficient_data`
- Expected impact: nullable
- Risk if ignored: `preporuke ostaju slabije pouzdane`

## Non-goals

- No new analytics algorithm.
- No new confidence invention in frontend.
- No broad auth or workflow redesign.
- No full backend aggregate in Phase 1 unless explicitly promoted later.
- No supplier/inventory/product algorithm changes.
- No backend aggregate endpoint until Phase 1 composition proves the lane model in production.

## Acceptance

The plan is complete when:

- the board route is defined
- the seven board sections are defined
- each card has a stable shape
- prioritization rules protect against fake confidence and fake zero
- Phase 1 composition and Phase 2 aggregate paths are both documented
- the board reuses current modules instead of inventing a new decision system
