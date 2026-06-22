# Decision Board Ranking Parity Plan

Date: 2026-06-22T10:08:32+02:00
Local HEAD: `99253a47c5b4e39a6aca5c7a76a07ff70e3e231c`

## Scope

- [docs/qa/DECISION_BOARD_BACKEND_AGGREGATE_GATE.md](./DECISION_BOARD_BACKEND_AGGREGATE_GATE.md)
- [docs/qa/DECISION_BOARD_CANDIDATE_CONTRACT_AUDIT.md](./DECISION_BOARD_CANDIDATE_CONTRACT_AUDIT.md)
- [docs/qa/DECISION_BOARD_DEDUPE_RULES.md](./DECISION_BOARD_DEDUPE_RULES.md)
- [docs/Analytics/EXECUTIVE_DECISION_BOARD_PLAN.md](../Analytics/EXECUTIVE_DECISION_BOARD_PLAN.md)
- [Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx](../../Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx)
- [Klijent/clientapp/src/pages/__tests__/ExecutiveDecisionBoardPage.spec.ts](../../Klijent/clientapp/src/pages/__tests__/ExecutiveDecisionBoardPage.spec.ts)

## Goal

Lock the current Executive Decision Board ranking behavior into an explicit parity plan before any future backend aggregate endpoint attempts to centralize ordering.

This document does not implement ranking changes.
It defines:

- the current ranking inputs
- the current fallback/scoring logic still visible in frontend composition helpers
- the cases that require exact backend parity
- the cases that may stay lane-specific
- the acceptance bar that Q63 must meet later

## Current Ranking Reality

The repo currently has two ranking layers:

### 1. Active runtime ranking contract

The runtime page now loads `DecisionBoardAggregateResponse` and maps cards in the order already provided by backend sections.

That means the live page currently assumes:

- section membership is already decided
- card order inside each section is already decided
- `priorityScore` and `impactScore` are already meaningful

The page does not re-sort `payload.sections[n].cards`; it preserves backend order.

### 2. Shadow composition ranking logic still present in the page

`ExecutiveDecisionBoardPage.tsx` still contains local builder functions with ranking/scoring behavior:

- `buildProductCards(...)`
- `buildInventoryCards(...)`
- `buildSupplierCards(...)`
- `buildActionCards(...)`
- `buildOutcomeCards(...)`
- `buildBlockerCards(...)`
- `computePriorityScore(...)`
- `capInsufficientDataPriority(...)`

Those helpers are not the active fetch path today, but they are still the clearest available evidence of how the board model was intended to rank candidates.

## Current Ranking Inputs

The shadow composition logic uses these inputs today:

| Input | Current use |
| --- | --- |
| `expectedImpactRsd` | main business impact input |
| fallback impact proxies | `lostSalesEstimate`, inventory estimated value, supplier revenue, action impact estimate |
| `confidenceScore` / `confidencePct` | core trust/ordering input |
| `confidenceLevel` | especially to cap `insufficient_data` |
| `dataQualityStatus` | warning/critical/insufficient penalty |
| `recommendationStatus` | bonus or penalty in `computePriorityScore(...)` |
| `warningCodes` | indirectly matter through data-quality interpretation; not yet a fully explicit numeric severity input |
| `alreadyInAction` / workflow lineage | not a direct score input, but changes how a recommendation should be interpreted |
| action priority / due date | action lane specific sorting |
| outcome sample warnings | lowers outcome trust and can trigger `insufficient_data` cap |
| blocker family | blockers currently use hardcoded high priority values |

## Current Frontend Composition Ranking Logic

### Shared priority helper

`computePriorityScore(...)` currently combines:

- impact component:
  - `min(max(expectedImpact, 0), 500_000) / 5_000`
- confidence component:
  - normalized confidence score capped to 0-100
- data quality penalty:
  - `critical`: `-35`
  - `warning`: `-15`
  - `insufficient_data`: `-25`
- recommendation status bonus:
  - `REPLENISH`: `+20`
  - `EXPAND`: `+20`
  - `BOOST`: `+18`
  - `MARKDOWN`: `+14`
  - `FIX_DATA`: `+22`
  - `INSUFFICIENT_DATA`: `-15`

### Insufficient-data cap

`capInsufficientDataPriority(...)` enforces:

- if confidence tone is `insufficient`
- or `dataQualityStatus === insufficient_data`

then:

- `priorityScore` is capped at `40`

This is one of the most important exact-parity rules.

### Product shadow ranking

Current local product ordering does this:

1. sort descending by:
   - `(expectedImpactRsd ?? lostSalesEstimate ?? 0) + normalizedConfidence`
2. keep top 12
3. compute candidate `priorityScore`
4. cap insufficient-data priority

### Inventory shadow ranking

Current local inventory ordering does this:

1. merge aged + capital-locked candidate sources
2. dedupe by local item id
3. compute impact from:
   - `actionSpec.expectedImpactRsd`
   - fallback to estimated inventory value
4. compute `priorityScore`
5. cap insufficient-data priority
6. sort descending by `priorityScore`

### Supplier shadow ranking

Current local supplier ordering does this:

1. collect grow + risk supplier groups
2. compute supplier action key
3. use supplier revenue as rough impact proxy
4. compute `priorityScore`
5. cap insufficient-data priority
6. sort descending by `priorityScore`

### Action lane ranking

Current action lane ordering is not score-based first.
It sorts by:

1. action priority:
   - `P1` > `P2` > everything else
2. earliest due date
3. only then each row still receives a `priorityScore` for candidate metadata

This means backend parity for the action lane must preserve workflow priority semantics, not just generic score order.

### Outcome lane ranking

Current outcome lane has two parts:

1. summary outcome card:
   - score is mostly expected-impact-based
   - warnings push the card toward warning semantics
   - low measured sample size can force `insufficient_data`
2. pending-outcome action cards:
   - use `computePriorityScore(...) - 20`
   - then sort descending by `priorityScore`

### Blocker lane ranking

Current blocker lane uses explicit hardcoded priorities:

- stale/critical refresh blocker: `300`
- data quality health critical/warning: `280` / `190`
- dashboard freshness blocker: `240`
- missing cost blocker: `220`
- missing supplier blocker: `210`
- insufficient signal blocker: `260` or `180`, then capped if `insufficient_data`

This means blocker precedence is currently policy-like, not emergent from one generic scoring formula.

## Current Test Evidence

Existing `ExecutiveDecisionBoardPage` tests currently prove:

- payload section order is preserved
- missing expected impact remains null
- insufficient-data cards render as insufficient
- stale/warning source states stay visible
- empty payload produces no fake data

Existing tests do **not** yet prove:

- exact within-section ranking order
- exact tie-break behavior
- exact blocker precedence
- exact action-lane priority vs due-date behavior
- exact cross-lane parity between product/inventory/supplier candidates

That gap is exactly why this parity plan is needed before Q63.

## Exact-Parity Rules

The following rules require exact backend parity.

### 1. Insufficient-data cap

Exact rule:

- a candidate with `confidenceTone=insufficient`
- or `dataQualityStatus=insufficient_data`

must never outrank strong candidates as if it were high confidence.

Minimum parity expectation:

- candidate score is capped to an equivalent of the current `40` ceiling
- backend does not let insufficient candidates float to the top of urgent/impact lanes

### 2. Missing impact remains non-advantaged

Exact rule:

- missing `expectedImpactRsd` must never become fake `0 RSD`
- missing impact must not win over a real positive impact just because other metadata is present

Minimum parity expectation:

- missing impact remains nullable
- ranking logic distinguishes `unknown` from a true zero-like low-impact case

### 3. Stale/partial trust lowers ranking or keeps warning visible

Exact rule:

- stale/warning/critical trust states must not be ranked like fresh green recommendations

Minimum parity expectation:

- backend applies the same or stricter downgrade semantics as the current frontend penalties/caps

### 4. Blockers can outrank raw impact

Exact rule:

- critical freshness or data-quality blockers may outrank high-impact opportunity cards

Minimum parity expectation:

- blocker lane precedence remains explicit
- the aggregate does not bury critical blockers below attractive but noisy business wins

### 5. Action lane remains workflow-first

Exact rule:

- action cards in `actionsDecision` must preserve:
  - priority class first
  - due date second

Minimum parity expectation:

- backend does not replace workflow order with only `priorityScore`

### 6. Lifecycle separation remains intact

Exact rule:

- recommendation cards
- action cards
- outcome cards

must preserve their own ranking semantics and not collapse into one generic ordering rule.

## Section-Specific Rules That May Stay Lane-Specific

These do not need one perfectly universal formula across every lane, but they do need explicit lane policy.

### 1. Product urgent ranking details

Allowed lane-specific behavior:

- product urgent lane may continue using impact + confidence as its main local ranking shape

Requirement:

- still obey insufficient-data cap and warning penalties

### 2. Supplier rough impact proxy behavior

Allowed lane-specific behavior:

- supplier lane may continue using revenue as a coarse impact companion until better supplier impact fields exist

Requirement:

- this must stay documented as proxy behavior, not equivalent to expected financial impact

### 3. Outcome summary scoring

Allowed lane-specific behavior:

- outcome summary card may keep its own feedback-specific scoring logic

Requirement:

- low sample size or warning-heavy outcomes must stay downgraded

### 4. Blocker hardcoded precedence

Allowed lane-specific behavior:

- blockers may continue using explicit priority bands rather than a shared score formula

Requirement:

- their precedence order must be documented and tested

## Ranking Parity Matrix

| Case | Current frontend evidence | Required parity level | Notes |
| --- | --- | --- | --- |
| High confidence vs insufficient data | `capInsufficientDataPriority(...)`, existing insufficient-data test | Exact | Must not regress |
| Expected impact present vs missing | nullable impact rendering and current scoring fallbacks | Exact | Missing impact must stay nullable and non-advantaged |
| Stale data | source-state warning visibility, data-quality penalty logic | Exact | Warning/critical trust must lower rank or remain visibly downgraded |
| Urgent action | action lane priority + due-date sort, product/inventory urgency expectations | Exact within action lane; lane-specific elsewhere | Workflow order must stay intact |
| Blocked recommendation / blocker cards | explicit blocker priorities, `FIX_DATA` bonus, blocker lane policy | Exact at blocker-precedence level | Safety blockers may outrank opportunity cards |
| Product repeated in urgent and impact | intentional cross-lane repetition | Exact as policy, not exact same numeric score | Dedupe policy from Q63B applies |
| Supplier proxy impact | supplier revenue proxy in shadow logic | Lane-specific | Must remain clearly documented as proxy |
| Outcome feedback cards | summary + pending outcome formulas | Lane-specific with exact trust downgrade | Sample warnings must remain visible |

## Proposed Test Matrix For Later Tasks

These are parity tests to add later, not in Q63C itself.

### Matrix A: Confidence and insufficient data

1. high-confidence positive-impact candidate outranks insufficient-data candidate in the same lane
2. insufficient-data candidate remains capped even when proxy impact is large
3. warning candidate may rank above insufficient-data candidate when trust is still usable

### Matrix B: Impact presence vs absence

1. candidate with real positive `expectedImpactRsd` outranks same-trust candidate with missing impact
2. missing-impact candidate renders as unknown, not `0 RSD`
3. zero-like true impact case is distinguishable from missing impact

### Matrix C: Stale and partial trust

1. stale candidate is downgraded below fresh equivalent candidate
2. critical blocker outranks attractive but stale opportunity
3. partial source warning remains visible even when card keeps section membership

### Matrix D: Workflow urgency

1. `P1` action ranks above `P2`
2. same priority actions sort by earlier due date
3. recommendation with matching open action lineage stays visible but does not erase workflow card

### Matrix E: Blocked/data-quality-first behavior

1. `FIX_DATA` or critical blocker can outrank larger but noisy opportunity
2. `missing_cost`/`missing_supplier` style blockers remain ahead of low-confidence commercial suggestions when trust is materially impaired
3. `insufficient_signal` stays visibly insufficient and does not get promoted

## Backend Parity Acceptance Criteria

Q63 must not start until a future backend aggregate can prove all of the following:

1. The backend can reproduce insufficient-data caps without inventing frontend-only confidence semantics.
2. The backend preserves nullable impact and never treats missing impact as a valid low number.
3. The backend preserves or strengthens stale/partial/critical trust downgrades.
4. The backend preserves blocker precedence over noisy opportunity cards.
5. The backend preserves workflow-first ordering in `actionsDecision`.
6. The backend documents which lane policies are universal and which are section-specific.
7. Test fixtures compare aggregate ordering against the currently documented lane rules rather than a vague “similar enough” score.

## What This Plan Does Not Require

This plan does not require:

- one universal numeric formula for every lane
- immediate refactoring of current board page logic
- replacing hardcoded blocker priorities today
- implementing the backend aggregate endpoint

It only requires that later backend work be judged against explicit parity criteria.

## Conclusion

The most important parity rule is not “copy the exact current numbers everywhere.”

It is:

- preserve trust downgrades
- preserve insufficient-data caps
- preserve blocker precedence
- preserve workflow-first action ordering
- preserve nullable impact semantics

If a future backend aggregate cannot prove those behaviors, it is not ready to replace the current board model.
