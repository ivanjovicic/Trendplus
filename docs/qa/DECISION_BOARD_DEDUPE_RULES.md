# Decision Board Dedupe Rules

Date: 2026-06-22
Local HEAD: `f6cb56db95ee190bdeda0d9b2000dc59729094b5`

## Scope

- [docs/qa/DECISION_BOARD_BACKEND_AGGREGATE_GATE.md](./DECISION_BOARD_BACKEND_AGGREGATE_GATE.md)
- [docs/qa/DECISION_BOARD_CANDIDATE_CONTRACT_AUDIT.md](./DECISION_BOARD_CANDIDATE_CONTRACT_AUDIT.md)
- [docs/Analytics/EXECUTIVE_DECISION_BOARD_PLAN.md](../Analytics/EXECUTIVE_DECISION_BOARD_PLAN.md)
- [docs/Analytics/DECISION_CONFIDENCE_CONTRACT.md](../Analytics/DECISION_CONFIDENCE_CONTRACT.md)
- [Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx](../../Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx)
- [Klijent/clientapp/src/pages/__tests__/ExecutiveDecisionBoardPage.spec.ts](../../Klijent/clientapp/src/pages/__tests__/ExecutiveDecisionBoardPage.spec.ts)
- [Klijent/clientapp/src/types/analytics.ts](../../Klijent/clientapp/src/types/analytics.ts)

## Goal

Define how Executive Decision Board candidates should be identified, when they may be intentionally repeated across sections, and when they must be deduped before a future backend aggregate endpoint can be considered safe.

Q63 remains blocked. This document does not implement dedupe behavior. It freezes the rule set that later Q63C-Q63F can test and gate.

## Current State Summary

Current evidence shows three different identity ideas in the board code:

1. Card instance identity:
   - API cards expose `id`
   - shadow composition helpers often generate section- or index-based IDs
2. Source identity:
   - action lineage already keys off `sourceType:sourceKey`
   - `resolveActionState(...)` and `actionStateIndex(...)` use this pair today
3. Recommendation identity:
   - `recommendationType` exists on some upstream DTOs, especially Product Decision and Action ledger snapshots
   - it is not yet mandatory on board candidates

That means the board does not yet have one stable dedupe key.

## Current Practical Signals In Code

### Action lineage already trusts `sourceType + sourceKey`

The board currently determines whether something is already in action by matching:

- `action.sourceType`
- `action.sourceKey`

This is strong evidence that source identity already matters operationally.

### Supplier already synthesizes a composite action key

The current supplier helper builds:

- supplier family
- action kind (`negotiation` vs `signal_check`)
- supplier id
- period
- store scope
- data scope

This shows supplier cards already need more than a simple numeric ID to avoid false collisions.

### Inventory already dedupes one local overlap

The shadow inventory builder merges:

- `topAgedItems`
- `topCapitalLockedItems`

by local item ID before creating cards.

This shows one class of dedupe is already considered necessary even before aggregate centralization.

### Product cards can naturally repeat across lanes

The test fixture already repeats the same product card in both:

- `urgent`
- `impact`

That is current evidence that repeated cards across sections are intentional, not automatically a bug.

## Canonical Identity Vocabulary

### 1. Card instance ID

Definition:

- the unique ID of one rendered card instance in one section

Use:

- React key
- click tracking
- section-local ordering

Rule:

- card instance ID must not be treated as the business dedupe key

Reason:

- the same underlying recommendation may need to appear in multiple sections
- some current IDs are index-based or lane-based and therefore unstable for dedupe

### 2. Source identity

Definition:

- `sourceType + sourceKey`

Use:

- workflow lineage
- action state lookup
- source collision detection

Rule:

- source identity is the minimum dedupe anchor
- future board candidates should not omit it except for truly synthetic blockers that have no upstream entity

### 3. Recommendation identity

Definition:

- `sourceType + sourceKey + recommendationType`

Use:

- recommendation-level dedupe
- repeated-card policy
- parity testing

Rule:

- this is the canonical candidate identity target for backend aggregate work
- if `recommendationType` is unavailable, the candidate is not ready for deterministic aggregate dedupe yet

## Canonical Dedupe Key

Recommended dedupe key for future aggregate work:

`sourceType + sourceKey + recommendationType`

Why this key:

- `sourceType` separates product/inventory/supplier/data-quality/action semantics
- `sourceKey` anchors one source entity or one action lineage record
- `recommendationType` separates two different recommendations about the same source entity

Examples:

- one product can have `REPLENISH` and later `MARKDOWN`
- one supplier can have `NEGOTIATE` and `SIGNAL_REVIEW`
- one inventory item can have `REBALANCE` and `DEAD_STOCK_REVIEW`

Without `recommendationType`, these would collide incorrectly.

## When Repetition Is Intentional

Repetition is allowed when all of these are true:

1. the repeated cards point to the same recommendation identity
2. the cards appear in different sections
3. each section explains a distinct business reason for surfacing the same recommendation
4. the UI makes it clear that the repeated item links back to the same source/workflow context

Examples of valid repetition:

- the same product recommendation appears in `urgent` and `impact`
- the same stock-risk recommendation appears in `urgent` and `stockRisk`
- the same supplier recommendation appears in `urgent` and `supplierRisk`
- the same open action appears in `actionsDecision` and still influences `urgent` if it is overdue or high-risk

This is not “duplicate noise” if the lane meaning is different.

## When Repetition Becomes A Real Duplicate

A candidate is a real duplicate if:

1. it has the same canonical dedupe key
2. it appears more than once in the same section
3. the copies do not add distinct lane meaning or workflow state
4. the repeated cards would send the operator to the same source and same action context

Examples of real duplicates:

- the same supplier recommendation appears twice in `supplierRisk` because it was collected from two summary lists
- the same inventory signal appears twice in `stockRisk` through two overlapping source arrays
- the same action row appears twice in `actionsDecision` with identical status
- the same outcome row appears twice in `actionsOutcome` with identical lifecycle meaning

These must be deduped before a backend aggregate is considered stable.

## Collision Classes

### 1. Same source, same recommendation, same section

Example:

- the same inventory item appears from `topAgedItems` and `topCapitalLockedItems`
- both resolve to the same `sourceType + sourceKey + recommendationType`
- both try to enter `stockRisk`

Rule:

- collapse into one card

Priority:

- keep the variant with stronger warning severity
- if warning severity ties, keep the higher `priorityScore`
- if still tied, keep the one with richer impact/freshness fields

### 2. Same source, same recommendation, different sections

Example:

- the same product replenishment recommendation appears in `urgent` and `impact`

Rule:

- keep both if the lane meaning is distinct

Required:

- identical source identity
- identical recommendation identity
- no conflicting workflow state

### 3. Same source, different recommendation type

Example:

- same product or inventory source entity but two different recommendation types such as OOS/replenish vs markdown

Rule:

- do not dedupe

Reason:

- same entity does not mean same business decision

### 4. Same source identity, recommendation card plus action card

Example:

- product recommendation card in `urgent`
- open action with the same `sourceType + sourceKey` in `actionsDecision`

Rule:

- do not dedupe into one card
- keep both if the board needs both “what should be done” and “what is already in workflow”

Required behavior:

- recommendation card must reflect `alreadyInAction=true`
- action card must look like workflow state, not a fresh new recommendation

### 5. Same source identity, recommendation card plus outcome card

Example:

- supplier or product recommendation lineage also appears in `actionsOutcome`

Rule:

- do not dedupe into one card

Reason:

- recommendation and learning feedback are different lifecycle stages

### 6. Synthetic blockers with no source entity

Example:

- stale refresh blocker
- missing cost blocker
- missing supplier blocker

Rule:

- these use synthetic identities and should dedupe by blocker family, not by source entity

Recommended synthetic recommendation types:

- `REFRESH_STALE`
- `MISSING_COST`
- `MISSING_SUPPLIER`
- `INSUFFICIENT_SIGNAL`
- `DATA_QUALITY_HEALTH`
- `DASHBOARD_FRESHNESS`

## Module-Specific Collision Examples

### Product

Potential collisions:

- `REPLENISH` candidate in `urgent`
- same `REPLENISH` candidate in `impact`
- same product also linked to an open action in `actionsDecision`

Rule:

- keep repeated recommendation across `urgent` and `impact`
- keep action card separately
- mark recommendation card as already in action if lineage matches

### Inventory

Potential collisions:

- one item appears in both `topAgedItems` and `topCapitalLockedItems`
- OOS/replenishment and dead-stock logic target the same entity with different recommendation types

Rule:

- same recommendation type in same lane collapses
- different recommendation types stay separate

### Supplier

Potential collisions:

- one supplier appears in both `topGrowSuppliers` and `topRiskSuppliers`
- one supplier can have `negotiation` and `signal_check` action families

Rule:

- same supplier + same recommendation type in `supplierRisk` collapses
- different supplier recommendation types stay separate

### OOS / Replenishment

These may currently surface through Product Decision and Inventory shapes.

Potential collisions:

- same SKU/source entity produces:
  - product-side replenish recommendation
  - inventory-side stock-risk recommendation

Rule:

- do not auto-dedupe cross-module unless the future contract proves they share the same canonical recommendation identity

Reason:

- they may look similar, but one may be sales-driven and the other stock-coverage-driven

### Markdown

These may currently surface through Product Decision or future markdown-specific surfaces.

Potential collisions:

- same SKU has both markdown and replenish-style signals at different times

Rule:

- markdown must not dedupe with non-markdown recommendation types

Reason:

- they are opposite operational decisions

### Action / Outcome

Potential collisions:

- open action row and recommendation row share source lineage
- outcome row and closed action lineage share source lineage

Rule:

- never collapse recommendation, action, and outcome into one card class
- preserve lifecycle-stage separation

## Section Rules

### Same-section dedupe

Rule:

- required

Meaning:

- one canonical recommendation identity may appear at most once per section

Exception:

- none currently documented

### Cross-section dedupe

Rule:

- optional and policy-driven, not automatic

Meaning:

- repeated cards across sections are allowed when section meaning differs

Required future backend behavior:

- the aggregate must explicitly mark whether repetition is intentional
- the aggregate must not accidentally multiply one source recommendation across lanes due to raw list joins

## Recommended Priority When A Same-Section Collision Happens

If two candidates collapse to the same canonical dedupe key inside one section, keep the candidate with the strongest operator value using this order:

1. higher warning severity if one candidate is a blocker and the other is only advisory
2. stronger action state visibility if one candidate is already in workflow
3. richer trust metadata
4. higher `priorityScore`
5. richer impact metadata
6. newest `generatedAtUtc`

This is not yet a ranking contract. It is only a tie-break rule for true duplicates.

## Minimum Fields Needed For Safe Dedupe

Future backend aggregate work should not proceed until each candidate can provide:

- `sourceType`
- `sourceKey`
- `recommendationType`
- `sectionKey`
- `alreadyInAction`
- `alreadyClosed`
- `warningCodes`
- `dataQualityStatus`

Preferably also:

- `recommendationId`
- `sourceRecommendationId`
- `generatedAtUtc`
- candidate-level `sourceLink`

## Proposed Test Cases For Later Tasks

These are not implemented in Q63B. They should inform later parity or frontend tests.

1. same product recommendation repeated in `urgent` and `impact` is preserved intentionally
2. same inventory recommendation repeated inside `stockRisk` collapses to one card
3. same supplier appearing in grow/risk inputs with same recommendation type yields one lane card
4. same source identity with different recommendation types does not collapse
5. recommendation card and action card with shared lineage both remain visible, but recommendation reflects `alreadyInAction`
6. outcome summary and outcome action rows do not erase each other

## Blocking Gaps Still Open After This Document

This document resolves the dedupe-policy vocabulary, but it does not remove all blockers.

Still open:

- candidate contract still lacks mandatory `recommendationType` on board cards
- source identity is still nullable in the current board card DTO
- recommendation identity is still not distinct from card instance ID
- lane-level repetition still needs ranking-parity and freshness-contract follow-up

That is why Q63 remains blocked and Q63C/Q63D still matter.

## Conclusion

The board should dedupe by recommendation identity, not by card instance ID and not by source entity alone.

For Trendplus, the safest rule set is:

- same section + same `sourceType + sourceKey + recommendationType` => dedupe
- different sections + same `sourceType + sourceKey + recommendationType` => preserve if lane meaning is distinct
- same source identity but different recommendation type => keep separate
- recommendation vs action vs outcome => keep separate lifecycle cards

These rules are concrete enough for later parity planning, but not yet sufficient to unblock Q63.
