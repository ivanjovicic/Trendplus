# Decision Explainability Executive Board Reuse Contract

Status: frozen contract for DEX18
Date: 2026-08-14
Related roadmap: `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md`
Reference contracts: `docs/architecture/DECISION_GRAPH_CONTRACT.md`, `docs/architecture/DECISION_EXPLAINABILITY_CROSS_FAMILY_READINESS.md`

## Purpose

This contract freezes how Executive Decision Board can reuse the shared DEX vocabulary without inventing local scoring, local Why text or decision-tree semantics.

Executive Decision Board is a composed consumer. It does not own the underlying recommendation truth; it aggregates upstream recommendation families into a single executive view.

## Executive surfaces in scope

| Surface | Already present | Reuse meaning |
| --- | --- | --- |
| Board envelope | `periodFrom`, `periodTo`, `lastRefreshAt`, `dataFreshnessStatus`, `dataQualityStatus`, `warnings`, `loadWarnings`, `Meta` | Trust, freshness and load-state envelope for the executive view |
| Board cards | `DecisionBoardCardDto` / `BoardCard` fields exposed by `Api/Endpoints/DecisionBoardEndpoints.cs` and `Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx` | The executive card is the presentation shell for a backend-led recommendation, blocker, action or outcome |
| Product cards | `confidenceLevel`, `confidenceScore`, `reliabilityPct`, `expectedImpactRsd`, `riskIfIgnored`, `recommendedNextAction`, `warningCodes`, `dataQualityStatus`, `generatedAtUtc` | Deterministic product recommendation evidence reused from the Product Decision Center |
| Supplier cards | `confidenceLevel`, `confidenceScore`, `recommendationAllowed`, `warningCodes`, `reasonCodes`, `dataQualityStatus`, `generatedAtUtc` | Supplier-facing recommendation truth reused as an executive aggregate, not rewritten locally |
| Inventory cards | `confidenceLevel`, `confidenceScore`, `confidenceSource`, `reasonCodes`, `warningCodes`, `recommendationAllowed`, `dataQualityStatus`, `generatedAtUtc` | Signal-backed inventory card with explicit workflow-only fallback when `confidenceSource=workflow_status_only` |
| Blocker cards | `confidenceLabel`, `confidenceScore`, `warningCodes`, `dataQualityStatus`, `recommendedNextAction` | Data-quality and freshness blockers are constraints, not new recommendations |
| Action cards | `confidenceLabel`, `confidenceScore`, `riskIfIgnored`, `recommendedNextAction`, `warningCodes`, `dataQualityStatus`, `alreadyInAction`, `alreadyClosed` | Open/closed executive actions remain traceable to source state |
| Outcome cards | `confidenceLabel`, `confidenceScore`, `measuredImpactRsd`, `realizationRatio`, `warningCodes`, `dataQualityStatus` | Measured outcome evidence for historical review, not a new source of recommendation truth |

## Board card identity and traceability

The board card shell already carries enough identity to keep the reuse contract traceable without inventing a new source of truth.

| Field | Contract role | Notes |
| --- | --- | --- |
| `id` | Presentation identity | Stable enough for rendering and UI state, but not a truth token |
| `sectionKey` | Executive grouping | Places the card in urgent / impact / risk / blockers / action / outcome sections |
| `kind` | Family tag | Distinguishes product, supplier, inventory, blocker, action and outcome cards |
| `sourceModule` | Upstream family label | Human-readable source family name |
| `sourceType` | Source family identifier | Source lineage, not authorization |
| `sourceKey` | Source correlation key | Stable trace into the upstream domain |
| `sourceLink` | Navigation target | Presentation navigation only |
| `actionHref` | Action destination | Workflow navigation only |
| `actionStateLabel` | Workflow state label | Surface state, not evidence state |
| `generatedAtUtc` | Freshness marker | When the source snapshot was generated |

## Recommendation, confidence and reason vocabulary

The executive contract may reuse only backend-led explanation fields:

- `confidenceLevel`
- `confidenceScore`
- `confidenceSource`
- `reliabilityPct`
- `recommendationAllowed`
- `reasonCodes`
- `warningCodes`
- `dataQualityStatus`
- `riskIfIgnored`
- `recommendedNextAction`

Rules for that vocabulary:

1. `confidenceSource=workflow_status_only` is a fallback label, not evidence-backed confidence.
2. `recommendationAllowed=false` is a constraint signal, not a confidence score.
3. `reasonCodes` and `warningCodes` are machine-readable vocabulary, not Why-panel prose.
4. Missing confidence, impact or reason evidence stays explicit instead of being converted into a fake green default.
5. `DecisionBoardCard.Id` is a presentation identifier and does not replace upstream identities.

## Impact and history vocabulary

The board may surface impact and history fields only as downstream evidence:

- `expectedImpactRsd`
- `measuredImpactRsd`
- `realizationRatio`
- `impactScore`
- `priorityScore`

Contract meaning:

- expected impact is what the board believes may happen if action is taken;
- measured impact is what was observed later, if outcome evidence exists;
- realization ratio compares expected and measured outcome evidence;
- priority score is an ordering helper, not a source of truth by itself;
- outcome cards must not be presented as fresh recommendation truth.

## Canonical reuse rules

1. Backend remains the source of truth for recommendation status, confidence, reliability, reason codes, fallback and data-quality semantics.
2. The executive board may aggregate those fields, but it may not invent local scoring or local Why text.
3. `confidenceSource=workflow_status_only` must stay visible when signal evidence is missing.
4. `recommendationAllowed=false` must stay visible as a constraint.
5. `warnings`, `warningCodes` and `reasonCodes` are explanatory vocabulary, not a tree path.
6. Do not infer a decision tree from ordering, prose, reason codes or board section placement.
7. Do not collapse missing snapshot or missing evidence into a visually complete but semantically weaker fallback.

## Gap matrix

| Surface | What is already present | What still blocks reuse | Reuse state |
| --- | --- | --- | --- |
| Board envelope | Period, freshness, data-quality and warning metadata | No frozen board-level explainability snapshot identity existed before this contract | Frozen envelope, runtime pending |
| Product cards | Deterministic confidence, impact, reason and action vocabulary | No board-specific Why/tree semantics; board must keep using the upstream product contract | Reuse ready as a consumer view |
| Supplier cards | Confidence, recommendation allowance, reason and fallback semantics | No board-owned semantic layer; helper/signal labeling still needs to stay explicit | Reuse ready as a consumer view |
| Inventory cards | Confidence source, reason codes and fallback warning path | Workflow-status-only fallback must remain explicit and not become evidence truth | Partial, with explicit fallback |
| Blockers | Quality and freshness blockers already surface | Blockers are constraints, not recommendations | Constraint only |
| Actions / outcomes | Action state, expected/measured impact and realization fields | Outcome evidence must not replace the recommendation contract | Consumer only |

## Smallest next rollout

The smallest safe Executive Board rollout is a frozen board contract that reuses only these backend-led fields:

- `id`
- `sourceModule`
- `sourceType`
- `sourceKey`
- `confidenceLevel`
- `confidenceScore`
- `confidenceSource`
- `reliabilityPct`
- `recommendationAllowed`
- `reasonCodes`
- `warningCodes`
- `dataQualityStatus`
- `expectedImpactRsd`
- `measuredImpactRsd`
- `realizationRatio`
- `riskIfIgnored`
- `recommendedNextAction`
- `generatedAtUtc`

That vocabulary is enough to render executive cards consistently without authorizing a new API shape, local scoring or a synthetic Why tree.

## Compatibility notes

- This document does not change runtime behavior.
- This document does not authorize a new API shape.
- This document does not make Executive Decision Board a second source of truth.
- If a future board card lacks one of the fields above, the gap should stay visible instead of being synthesized away.

## References

- `docs/architecture/DECISION_GRAPH_CONTRACT.md`
- `docs/architecture/DECISION_EXPLAINABILITY_CROSS_FAMILY_READINESS.md`
- `Api/Endpoints/DecisionBoardEndpoints.cs`
- `Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx`

## Completion note

- Date: 2026-08-14
- Status: DONE
- Completion: 100%
- Changed files: docs/architecture/DECISION_EXPLAINABILITY_EXECUTIVE_BOARD_REUSE.md; docs/architecture/DECISION_EXPLAINABILITY_CROSS_FAMILY_READINESS.md; docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md; docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md
- Checks run: node scripts/check-planning-architecture.mjs pass; git diff --check pass
- Checks not run: dotnet build/test; npm run build; docs/contracts only
- Run log: .ai/runs/2026-08-14-DEX18-evidence.md
- Delivery mode: direct-main
- Main commit SHA: 5a9e05e317542c582ed38474b75d70604183684a
- Main verification: git rev-parse origin/main -> 5a9e05e317542c582ed38474b75d70604183684a
- Missed: none known
- Follow-up: DEX19 Executive Board explainability runtime slice
- Residual risk: Executive Board runtime wiring still needs a later prompt, but the reuse contract is frozen
- Prompt defect / scope repair: froze the board-specific explainability reuse contract and kept Executive Board in consumer role only
