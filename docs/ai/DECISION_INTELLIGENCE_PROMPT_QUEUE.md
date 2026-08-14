# Trendplus Decision Intelligence Planning Queue







Created: 2026-08-08



Roadmap: `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md`



Purpose: planning/contracts only until later roadmap gates explicitly authorize runtime work.







## Current READY by program







| Program | Current READY | Execution class |



|---|---|---|



| DEX - Decision Explainability | `DEX19` | backend/frontend - Executive Board explainability reuse runtime |



| RL - Recommendation Learning | `RL07` | docs/contracts only - measurement statistics review surface |



| DT - Decision Timeline | `DT07` | backend/frontend - timeline export runtime slice |







Only one prompt per program may be READY. A READY prompt in this file does not outrank the existing BCI/STAB/RQ/QDB/MT/GAI execution priority from `MASTER_ROADMAP.md` and does not authorize broad runtime implementation.







---







---

## DEX19 - Implement Executive Decision Board explainability reuse runtime slice

Status: READY
Priority: future / planning
Feature family: decision-explainability-executive-reuse
Parallel-safe: yes, when path-safe vs current execution
Owner: unassigned
Local lock: .ai/task-locks/DEX19-<agent>.lock.md

### Problem

The Executive Board explainability reuse contract is frozen, but the board runtime still needs to reuse that backend-led vocabulary without inventing local scoring, local Why text or a synthetic decision tree.

### Evidence

- docs/architecture/DECISION_EXPLAINABILITY_EXECUTIVE_BOARD_REUSE.md
- docs/architecture/DECISION_EXPLAINABILITY_CROSS_FAMILY_READINESS.md
- Api/Endpoints/DecisionBoardEndpoints.cs
- Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx

### Scope

- backend-led explainability payload reuse on Executive Decision Board;
- keep confidence, reliability, recommendation allowance, reason codes and fallback labels authoritative;
- no new API shape unless a named gap blocks reuse of the frozen contract;
- no schema migration.

### Read first

- DEX18 completion note
- Executive Board explainability reuse contract
- DEX11 cross-family readiness
- Decision Board aggregation contract tests

### Do

1. Wire the frozen board reuse vocabulary into the executive aggregate/card payload and board UI.
2. Keep `recommendationAllowed=false` and `confidenceSource=workflow_status_only` explicit.
3. Preserve empty and error states; missing snapshot or Why evidence stays visible.
4. Do not invent local confidence, Why prose or a decision tree from section placement.

### Tests

- board cards reuse backend-led confidence/reason/fallback fields instead of local duplicates;
- missing evidence does not become a healthy-looking zero or complete Why tree;
- no frontend-invented scoring is added.

### Acceptance

- Executive Decision Board reuses the frozen DEX18 vocabulary at runtime;
- the board remains a consumer, not a second source of truth;
- READY pointer remains single for DEX.

### Dependencies

- DEX18 DONE.

## DEX18 - Prepare Executive Decision Board explainability reuse contract

Status: DONE
Priority: future / planning
Feature family: decision-explainability-executive-reuse
Parallel-safe: yes, docs/contracts only
Owner: Cursor Auto
Local lock: .ai/task-locks/DEX18-cursor.lock.md (removed after DONE)

### Problem

Product, supplier and inventory explainability snapshots exist, but Executive Decision Board still lacks a frozen reuse contract that maps board cards to the shared DEX vocabulary without local scoring or invented Why/tree semantics.

### Evidence

- docs/architecture/DECISION_EXPLAINABILITY_CROSS_FAMILY_READINESS.md
- docs/architecture/DECISION_GRAPH_CONTRACT.md
- Api/Endpoints/DecisionBoardEndpoints.cs
- Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx

### Scope

- docs/contracts only for Executive Decision Board explainability reuse;
- no new API shape, local scoring or invented decision tree;
- reuse backend-led recommendation, confidence, fallback and reason codes only.

### Read first

- DEX11 cross-family readiness
- DEX12 / DEX13 reuse contract style
- Decision Board aggregation contract tests

### Do

1. Map which board card fields already expose recommendation, confidence, reason, fallback and snapshot identity.
2. Mark gaps that block reuse of the shared DEX vocabulary.
3. Keep helper/signal labeling explicit when recommendationAllowed=false or fallback is used.
4. Do not authorize runtime work in this prompt.

### Tests

- contract forbids frontend-invented confidence or Why text;
- missing snapshot/tree stays explicit;
- docs remain UTF-8; no runtime code lands.

### Acceptance

- a docs-only Executive Board explainability reuse contract exists;
- READY pointer remains single for DEX.

### Dependencies

- DEX17 DONE;
- DEX11 DONE.

### Completion note

- Date: 2026-08-14
- Status: DONE
- Completion: 95%
- Changed files:
  - docs/architecture/DECISION_EXPLAINABILITY_EXECUTIVE_BOARD_REUSE.md
  - docs/architecture/DECISION_EXPLAINABILITY_CROSS_FAMILY_READINESS.md
  - docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md
  - docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md
  - MASTER_ROADMAP.md
  - docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md
  - docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md
  - docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md
  - docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_TEST_HARDENING_ADDENDUM.md
  - .ai/runs/2026-08-14-DEX18-evidence.md
- Checks run:
  - `node scripts/check-prompt-queues.mjs --self-test` - pass
  - `node scripts/check-prompt-queues.mjs` - pass (260 tasks)
  - `node scripts/check-planning-architecture.mjs --self-test` - pass
  - `node scripts/check-planning-architecture.mjs` - pass
  - `node scripts/check-agent-instructions.mjs --self-test` - pass
  - `node scripts/check-agent-instructions.mjs` - pass
  - `git diff --check` - pass
- Checks not run:
  - `dotnet build` / `dotnet test` - docs/contracts only
  - `npm run build` / frontend tests - docs/contracts only
- Run log: .ai/runs/2026-08-14-DEX18-evidence.md
- Delivery mode: direct-main
- Main commit SHA: 731131fd198ab9390bb3cc158887456dc041e738
- Main verification: pending push to origin/main
- Missed: Executive Board runtime wiring is out of scope and is not a live DEX READY prompt
- Follow-up: `RL07` (current execution); DEX program READY is `DEX19`
- Residual risk: board cards can still omit a frozen field at runtime; the contract requires the gap to stay visible rather than be synthesized
- Prompt defect / scope repair: previous close left DEX18 Status READY after the contract landed; this run closes DEX18 and inserts DEX19 as the required single DEX READY
- Next: `RL07` - Prepare measurement-statistics review surface contract


## DEX17 - Implement Supplier Decision Hub explainability snapshot runtime slice

Status: DONE

Priority: future / planning

Feature family: decision-explainability-supplier-reuse

Parallel-safe: yes, backend and surface wiring

Owner: Codex

Local lock: removed after DONE

### Problem

Supplier Decision Hub already exposes backend-led trust, fallback, confidence and report semantics, but the runtime page and report surfaces still do not render the shared explainability snapshot that makes those semantics explicit end to end.

### Evidence

- `docs/architecture/DECISION_EXPLAINABILITY_SUPPLIER_REUSE.md`
- `docs/architecture/DECISION_EXPLAINABILITY_CROSS_FAMILY_READINESS.md`
- `docs/qa/SUPPLIER_CONFIDENCE_CONTRACT_AUDIT.md`
- `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md`

### Scope

- render the frozen supplier explainability snapshot on supplier decision hub surfaces;
- reuse backend-led confidence, reliability, recommendation allowance, fallback and reason codes only;
- keep helper/signal labeling explicit when recommendationAllowed=false or fallback is used;
- no new API shape, local scoring or invented decision tree.

### Read first

- supplier explainability reuse contract
- cross-family readiness contract
- supplier confidence contract audit
- Decision Intelligence roadmap

### Do

1. Surface the frozen supplier explainability snapshot in summary, ranking, detail and report views.
2. Keep confidence, reliability, recommendationAllowed, fallback and reason codes backend-led.
3. Preserve helper/signal labeling when the surface is fallback-based or recommendationAllowed=false.
4. Keep the smallest possible surface change.

### Tests

- snapshot rendering uses backend-led supplier fields only;
- missing confidence or fallback stays explicit;
- no local tree or synthetic recommendation truth is introduced.

### Acceptance

- supplier runtime surfaces render shared snapshot semantics;
- no local tree or synthetic recommendation truth is added;
- READY pointer remains single for DEX.

### Dependencies

- DEX12 DONE.
- DEX11 DONE.

### Completion note

- Date: 2026-08-13
- Status: DONE
- Completion: 96%
- Changed files:
  - Klijent/clientapp/src/components/supplierDecisionHub/SupplierExplainabilitySnapshot.tsx
  - Klijent/clientapp/src/components/analytics/SupplierDecisionReport.tsx
  - Klijent/clientapp/src/pages/SupplierDecisionHubPage.tsx
  - Klijent/clientapp/src/services/supplierDecisionReport.ts
  - Klijent/clientapp/src/services/__tests__/supplierDecisionReport.spec.ts
  - Klijent/clientapp/src/components/analytics/__tests__/SupplierDecisionReport.spec.tsx
  - Klijent/clientapp/src/components/analytics/__tests__/SupplierExplainabilitySnapshot.spec.tsx
  - Klijent/clientapp/src/pages/__tests__/SupplierDecisionHubPage.spec.tsx
  - docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md
  - .ai/runs/2026-08-13-DEX17-evidence.md
- Checks run:
  - `git diff --check` - pass
  - `cd Klijent/clientapp; npm run test -- --run src/services/__tests__/supplierDecisionReport.spec.ts src/components/analytics/__tests__/SupplierDecisionReport.spec.tsx src/components/analytics/__tests__/SupplierExplainabilitySnapshot.spec.tsx src/pages/__tests__/SupplierDecisionHubPage.spec.tsx` - pass
- Checks not run:
  - `dotnet build` - not run; no backend files changed.
  - `dotnet test` - not run; no backend files changed.
  - `npm run check:analytics-guardrails` - not run; targeted UI/test validation covered the touched surfaces.
  - `npm run build` - not run; no build-only regression signal surfaced in the exercised surfaces.
- Run log: .ai/runs/2026-08-13-DEX17-evidence.md
- Delivery mode: direct-main
- Main commit SHA: ffc76554f0d9b1fe3c6f7fdc8eb8edbf4b9ecf19
- Main verification: `git rev-parse origin/main -> ffc76554f0d9b1fe3c6f7fdc8eb8edbf4b9ecf19`
- Missed: full repository build was intentionally skipped in favor of targeted validation.
- Follow-up: none
- Residual risk: snapshot semantics still depend on payload metadata staying in sync with the backend contract.
- Prompt defect / scope repair: DEX17 now renders the shared supplier explainability snapshot in hub and report surfaces with backend-led metadata reuse.

## DEX16 - Implement Inventory Detail and Insight explainability snapshot runtime slice

Status: DONE

Priority: future / planning

Feature family: decision-explainability-inventory-reuse

Parallel-safe: yes, backend and surface wiring

Owner: Codex

Local lock: removed after DONE

### Problem

Inventory detail and insight surfaces now have a frozen snapshot contract, but the runtime pages still do not render the backend-led explainability snapshot or its explicit missing-evidence gaps.

### Evidence

- `docs/architecture/DECISION_EXPLAINABILITY_INVENTORY_REUSE.md`
- `docs/architecture/DECISION_EXPLAINABILITY_CROSS_FAMILY_READINESS.md`
- `docs/qa/INVENTORY_SIGNAL_CONFIDENCE_CONTRACT.md`
- `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md`

### Scope

- wire the frozen inventory explainability snapshot into the detail and insight surfaces;
- reuse confidence, recommendation allowance, reason codes and data quality from backend-led inventory fields;
- preserve empty, error and fallback states and keep missing evidence explicit;
- no invented local tree or schema migration.

### Read first

- DEX15 completion note
- inventory explainability reuse contract
- cross-family readiness doc
- Decision Intelligence roadmap

### Do

1. Surface the frozen inventory explainability snapshot on detail and insight views.
2. Keep confidence, reason codes and recommendation allowance backend-led.
3. Preserve empty, error and insufficient-data states rather than synthesizing a green default.
4. Keep the smallest possible surface change.

### Tests

- detail/insight surfaces show missing evidence explicitly;
- no local confidence model or invented tree semantics are added;
- inventory explainability reuse stays aligned with the backend contract.

### Acceptance

- inventory detail and insight surfaces render the shared snapshot semantics;
- no local confidence model or invented tree semantics are added;
- READY pointer remains single for DEX.

### Dependencies

- DEX15 DONE.

### Completion note

- Date: 2026-08-13
- Status: DONE
- Completion: 98%
- Changed files:
  - Api/Dtos/InventoryExperienceDtos.cs
  - Api/Endpoints/InventoryEndpoints.cs
  - Api.Tests/InventoryListEndpointIntegrationTests.cs
  - Klijent/clientapp/src/components/inventory/InventoryExplainabilitySnapshot.tsx
  - Klijent/clientapp/src/components/inventory/InventoryInsightPanels.tsx
  - Klijent/clientapp/src/components/inventory/InventoryInsightPanels.spec.tsx
  - Klijent/clientapp/src/components/inventory/SKUDetailModal.tsx
  - Klijent/clientapp/src/components/inventory/SKUDetailModal.spec.tsx
  - Klijent/clientapp/src/components/inventory/inventoryUtils.ts
  - Klijent/clientapp/src/types/analytics.ts
  - .ai/runs/2026-08-13-DEX16-evidence.md
  - docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md
  - docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md
  - MASTER_ROADMAP.md
- Checks run:
  - `git diff --cached --check` - pass
  - `dotnet test Api.Tests/Api.Tests.csproj --filter InventoryListEndpointIntegrationTests` - pass
- `cd Klijent/clientapp; npm run test -- --run src/components/inventory/SKUDetailModal.spec.tsx` - pass
- `cd Klijent/clientapp; npm run test -- --run src/components/inventory/InventoryInsightPanels.spec.tsx` - pass
- `cd Klijent/clientapp; npm run check:analytics-guardrails` - pass
- `cd Klijent/clientapp; npm run build` - pass
- `node scripts/check-agent-instructions.mjs` - pass
- `node scripts/check-prompt-queues.mjs` - pass
- `node scripts/check-planning-architecture.mjs` - pass
- Checks not run:
  - `node scripts/check-agent-instructions.mjs --self-test` - not run.
  - `node scripts/check-prompt-queues.mjs --self-test` - not run.
  - `node scripts/check-planning-architecture.mjs --self-test` - not run.
- Run log: .ai/runs/2026-08-13-DEX16-evidence.md
- Delivery mode: direct-main
- Main commit SHA: 345b7e692b4b5c0eefea768746bb1020c08b10e0
- Main verification: `git rev-parse origin/main -> 345b7e692b4b5c0eefea768746bb1020c08b10e0`
- Missed: no broader inventory report/export follow-up was taken beyond the runtime snapshot surfaces.
- Follow-up: DEX17 supplier explainability snapshot runtime slice
- Residual risk: detail and insight sell-through evidence can still surface insufficient-data reasons when denominator evidence is missing.
- Prompt defect / scope repair: DEX16 needed backend-led snapshot wiring plus frontend reuse; the queue/router now reflects the completed runtime slice.


## DEX15 - Prepare Inventory Detail and Insight explainability snapshot contract

Status: DONE

Priority: future / planning

Feature family: decision-explainability-inventory-reuse

Parallel-safe: yes, docs/contracts only

Owner: Codex

Local lock: removed after DONE

### Problem

Inventory detail and insight surfaces still expose consumer facts and rollups, but the family lacks a frozen explainability snapshot that shows how backend-led DEX vocabulary should appear without a synthetic decision tree.

### Evidence

- `docs/architecture/DECISION_EXPLAINABILITY_INVENTORY_REUSE.md`
- `docs/architecture/DECISION_EXPLAINABILITY_CROSS_FAMILY_READINESS.md`
- `docs/qa/INVENTORY_SIGNAL_CONFIDENCE_CONTRACT.md`
- `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md`

### Scope

- frozen inventory explainability snapshot for detail and insight surfaces;
- reuse confidence, recommendation allowance, reason codes and data quality from backend-led inventory fields;
- no invented local tree, Why panel or workflow truth;
- no schema migration.

### Read first

- DEX14 completion note
- inventory explainability reuse contract
- cross-family readiness doc
- Decision Intelligence roadmap

### Do

1. Define the smallest detail/insight explainability snapshot contract for inventory surfaces.
2. Keep confidence, reason codes and recommendation allowance backend-led.
3. Preserve empty and error states and avoid synthetic defaults.
4. Keep the smallest possible docs-only surface change.

### Tests

- contract distinguishes evidence-backed signals from workflow-only state;
- missing evidence stays explicit;
- detail/insight surfaces remain consumer surfaces, not synthetic decision trees.

### Acceptance

- inventory detail and insight surfaces have a frozen explainability snapshot contract;
- no local confidence model or invented tree semantics are added;
- READY pointer remains single for DEX.

### Dependencies

- DEX14 DONE.

## DEX14 - Implement Inventory Decision Surface explainability reuse runtime slice

Status: DONE

Priority: future / planning

Feature family: decision-explainability-inventory-reuse

Parallel-safe: yes, backend and surface wiring

Owner: Codex

Local lock: removed after DONE

### Problem

The inventory explainability reuse contract is frozen, but the runtime wiring still needs to reuse shared DEX vocabulary on the inventory decision surface without inventing local confidence semantics.

### Evidence

- `docs/architecture/DECISION_EXPLAINABILITY_INVENTORY_REUSE.md`
- `docs/architecture/DECISION_EXPLAINABILITY_CROSS_FAMILY_READINESS.md`
- `docs/qa/INVENTORY_SIGNAL_CONFIDENCE_CONTRACT.md`
- `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md`

### Scope

- backend-led explainability payload reuse for the inventory decision surface;
- keep confidence, reliability and recommendation semantics authoritative;
- no new decision-tree vocabulary;
- no schema migration.

### Read first

- DEX13 completion note
- inventory explainability reuse contract
- cross-family readiness doc
- Decision Intelligence roadmap

### Do

1. Wire the shared DEX explainability payload into the inventory decision surface.
2. Keep Why / reason codes, confidence and recommendation allowance backend-led.
3. Preserve empty and error states instead of inventing local fallbacks.
4. Keep the smallest possible surface change.

### Tests

- surface shows shared DEX vocabulary rather than local duplicates;
- missing evidence does not become a healthy-looking zero or blank default;
- inventory explainability reuse stays aligned with the backend contract.

### Acceptance

- inventory decision surface reuses shared DEX explainability semantics;
- no local confidence model or invented tree semantics are added;
- READY pointer remains single for DEX.

### Dependencies

- DEX13 DONE.


## DEX13 - Prepare Inventory Decision Surface explainability reuse contract

Status: DONE
Priority: future / planning
Feature family: decision-explainability-inventory-reuse
Parallel-safe: yes, docs/contracts only
Owner: Codex
Local lock: none

### Problem

Inventory Decision Board still lacks a frozen explainability reuse contract that maps shared DEX vocabulary onto list, workflow and board surfaces without inventing a local tree.

### Evidence

- `docs/architecture/DECISION_EXPLAINABILITY_CROSS_FAMILY_READINESS.md`
- `docs/architecture/DECISION_EXPLAINABILITY_INVENTORY_REUSE.md`
- `docs/qa/INVENTORY_SIGNAL_CONFIDENCE_CONTRACT.md`

### Scope

- docs/contracts only
- no runtime implementation

### Read first

- DEX12 completion note
- `docs/architecture/DECISION_EXPLAINABILITY_INVENTORY_REUSE.md`

### Do

1. Map which inventory explainability fields are already exposed.
2. Mark gaps that block reuse of the shared DEX vocabulary.
3. Keep backend-led confidence and recommendation semantics authoritative.
4. Propose the smallest next inventory rollout without authorizing runtime work.

### Tests

- checklist distinguishes present vs missing vs invented fields;
- no runtime code or fake evidence is introduced;
- governance READY pointer remains single for DEX.

### Acceptance

- queue keeps one READY DEX prompt
- runtime remains gated

### Dependencies

- DEX12 DONE.


## DEX12 - Prepare Supplier Decision Hub explainability reuse contract







Status: DONE



Priority: future / planning



Feature family: decision-explainability-supplier-reuse



Parallel-safe: yes, docs/contracts only



Owner: Codex



Local lock: removed after DONE







### Problem







Supplier Decision Hub already exposes explicit confidence, reliability, recommendation allowance and report semantics, but the repo still lacks a frozen supplier-specific explainability reuse contract that shows how the DEX vocabulary should map without inventing local tree or Why semantics.







### Evidence







- `docs/architecture/DECISION_EXPLAINABILITY_CROSS_FAMILY_READINESS.md` identifies Supplier Decision Hub as the smallest cross-family explainability rollout candidate.



- `docs/qa/SUPPLIER_CONFIDENCE_CONTRACT_AUDIT.md` documents the current supplier confidence safety gaps.



- `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md` DI-4 keeps cross-family rollout in docs-only mode until a family-specific contract exists.







### Scope







- docs/contracts only: supplier explainability reuse readiness checklist and gap matrix;



- no runtime implementation;



- no new confidence or decision-tree semantics.







### Read first







- DEX11 completion note



- `docs/architecture/DECISION_EXPLAINABILITY_CROSS_FAMILY_READINESS.md`



- `docs/qa/SUPPLIER_CONFIDENCE_CONTRACT_AUDIT.md`



- `docs/architecture/DECISION_GRAPH_CONTRACT.md`



- `MASTER_ROADMAP.md`







### Do







1. Map which explainability fields Supplier Decision Hub already exposes.



2. Mark any gaps that would block reuse of the shared DEX vocabulary.



3. Keep backend-led confidence/reliability/recommendation semantics authoritative.



4. Propose the smallest next supplier rollout without authorizing runtime work.







### Tests







- checklist distinguishes present vs missing vs invented fields;



- no runtime code or fake evidence is introduced;



- governance READY pointer remains single for DEX.







### Acceptance







- a docs-only supplier explainability reuse contract exists;



- no supplier surface is marked ready without an explicit gap list;



- runtime remains gated.







### Dependencies







- DEX11 DONE.







### Completion note







- Date: 2026-08-13



- Agent: Codex



- Changed files:



  - `docs/architecture/DECISION_EXPLAINABILITY_SUPPLIER_REUSE.md`



  - `docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md`



- Checks:



  - `node scripts/check-prompt-queues.mjs` - pass



  - `node scripts/check-planning-architecture.mjs` - pass



  - `git diff --check` - pass



- Remaining risk:



  - This is a docs-only supplier reuse contract; runtime tree or evidence-snapshot behavior still needs a later prompt.



- Next:



  - DEX13 - Prepare Inventory Decision Surface explainability reuse contract

## DEX11 - Prepare cross-family Decision Explainability readiness contract







Status: DONE



Priority: future / planning



Feature family: decision-explainability-cross-family



Parallel-safe: yes, docs/contracts only



Owner: unassigned



Local lock: removed after DONE







### Problem







Product Decision Center now has graph, evidence, confidence, alternatives, Why, decision tree and immutable evidence snapshots, but other decision families still lack a shared readiness checklist for reusing the same explainability contracts without inventing local semantics.







### Evidence







- `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md` DI-4 calls for cross-family reuse after the first-family explainability loop.



- `docs/architecture/DECISION_GRAPH_CONTRACT.md` is the shared vocabulary.



- DEX01â€“DEX10 closed the Product Decision Center first-family loop through evidence snapshot freeze.







### Scope







- docs/contracts only: readiness checklist and gap matrix for inventory/supplier/decision-board families;



- no runtime implementation;



- no confidence mutation.







### Read first







- DEX10 completion notes



- `docs/architecture/DECISION_GRAPH_CONTRACT.md`



- `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md`







### Do







1. Document which explainability fields each major decision family already exposes.



2. Mark gaps that would break snapshot/Why/tree reuse.



3. Keep Product Decision Center as the reference implementation, not a template to copy blindly.



4. Propose the smallest next family rollout without authorizing runtime work.







### Tests







- checklist distinguishes present vs missing vs invented fields;



- no runtime code or fake evidence is introduced;



- governance READY pointer remains single for DEX.







### Acceptance







- a docs-only readiness contract exists for cross-family explainability reuse;



- no family is marked ready without an explicit evidence gap list;



- runtime remains gated.







### Dependencies







- DEX10 DONE.







### Completion note







- Date: 2026-08-13



- Agent: Codex



- Changed files:



  - `docs/architecture/DECISION_EXPLAINABILITY_CROSS_FAMILY_READINESS.md`



  - `docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md`



  - `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md`



  - `MASTER_ROADMAP.md`



- Checks:



  - `node scripts/check-prompt-queues.mjs` - pass



  - `node scripts/check-planning-architecture.mjs` - pass



  - `git diff --check` - pass



- Remaining risk:



  - This is a docs-only readiness contract; runtime reuse still needs later owner-promoted implementation.



- Next:



  - `DEX12 - Prepare Supplier Decision Hub explainability reuse contract`







## RL07 - Prepare measurement-statistics review surface contract

Status: READY
Priority: future / planning
Feature family: recommendation-learning-review-surface
Parallel-safe: yes, docs/contracts only
Owner: unassigned
Local lock: .ai/task-locks/RL07-<agent>.lock.md

### Problem

RL06 exposed measurementStatistics on the outcome summary endpoint, but there is still no frozen contract for an operator review surface that shows lifecycle funnel counts, measured coverage and outcome distribution without hiding gaps or mutating confidence.

### Evidence

- docs/Analytics/RECOMMENDATION_MEASUREMENT_STATISTICS_CONTRACT.md
- docs/architecture/RECOMMENDATION_LEARNING_STATISTICS_ROLLOUT_PLAN.md Slice 3
- Application/Analytics/RecommendationMeasurementStatisticsProjection.cs
- GET /api/analytics/actions/outcomes/summary

### Scope

- docs/contracts only for the review/dashboard/export presentation of measurement-only statistics;
- no confidence calibration;
- no schema migration;
- no frontend-local rates.

### Read first

- RL06 completion note
- measurement statistics contract
- RL02 rollout plan Slice 3

### Do

1. Define the review-surface fields, empty/insufficient states and warning placement.
2. Require the same denominators as measurementStatistics; do not reuse legacy 	otals rates as success.
3. Keep print/export failure graceful and gap-visible.
4. Do not authorize runtime UI work in this prompt.

### Tests

- contract keeps acceptance != success and execution != success;
- missing measured evidence stays not_measured;
- zero denominators stay null, not 0%.

### Acceptance

- a docs-only review-surface contract exists;
- READY pointer remains single for RL.

### Dependencies

- RL06 DONE.


## RL06 - Implement measurement-only recommendation statistics projection runtime slice

Status: DONE

Priority: future / planning

Feature family: recommendation-learning-statistics-projection

Parallel-safe: yes, backend-only

Owner: Codex

Local lock: removed after DONE

### Problem

The measurement-only statistics contract is frozen, but the runtime projection still needs a single backend slice that exposes the documented counts without mutating confidence or collapsing evidence gaps into zeroes.

### Evidence

- `docs/Analytics/RECOMMENDATION_MEASUREMENT_STATISTICS_CONTRACT.md`
- `docs/Analytics/RECOMMENDATION_OUTCOME_LEARNING_CONTRACT.md`
- `docs/architecture/RECOMMENDATION_LEARNING_STATISTICS_ROLLOUT_PLAN.md`
- `Infrastructure/Services/Analytics/AnalyticsActionItemService.cs`
- `Api/Endpoints/AnalyticsActionsEndpoints.cs`

### Scope

- backend-only runtime slice for the measurement-only statistics projection;
- preserve explicit denominators, warnings and empty-state semantics;
- do not add calibration or confidence mutation;
- no schema migration and no new event store.

### Read first

- RL05 completion note
- measurement statistics contract
- outcome learning contract
- rollout plan

### Do

1. Implement the smallest backend helper/service path that projects the documented measurement-only statistics.
2. Keep `not_measured`, `pending` and empty cohorts explicit.
3. Preserve warning codes and null denominators when evidence is missing.
4. Wire the projection through the existing outcome summary endpoint only if it can stay read-only.

### Tests

- focused `dotnet test` coverage for denominator handling;
- acceptance and execution are not success;
- missing measured evidence stays `not_measured`;
- empty cohorts stay empty with null rates.

### Acceptance

- the measurement-only statistics projection is exposed through the backend runtime path;
- the projection remains read-only and deterministic;
- confidence mutation and calibration remain out of scope;
- READY pointer remains single for RL.

### Dependencies

- RL05 DONE.

### Completion note

- Date: 2026-08-13
- Status: DONE
- Completion: 92%
- Changed files: Application/Analytics/RecommendationMeasurementStatisticsProjection.cs; Application/Analytics/RecommendationMeasurementStatisticsDto.cs; Infrastructure/Services/Analytics/AnalyticsActionItemService.cs; Api.Tests/RecommendationMeasurementStatisticsProjectionTests.cs; Api.Tests/AnalyticsActionItemServiceTests.cs; docs/Analytics/RECOMMENDATION_MEASUREMENT_STATISTICS_CONTRACT.md; docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md; docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md; MASTER_ROADMAP.md; .ai/runs/2026-08-13-RL06-evidence.md
- Checks run: dotnet test Api.Tests --filter RecommendationMeasurementStatisticsProjectionTests|GetOutcomeSummaryAsync_ReturnsNullRates|GetOutcomeSummaryAsync_UsesClosedDenominator pass (8); node scripts/check-prompt-queues.mjs --self-test pass; node scripts/check-prompt-queues.mjs pass; node scripts/check-planning-architecture.mjs --self-test pass; node scripts/check-planning-architecture.mjs fail (DEX/RL/DT 0 READY); node scripts/check-agent-instructions.mjs pass; git diff --check pass
- Checks not run: npm / frontend - backend-only; full dotnet test suite - focused proof only
- Run log: .ai/runs/2026-08-13-RL06-evidence.md
- Delivery mode: direct-main
- Main commit SHA: b8c4e22a5844e79ebda765e9f4d329b4e93a97c5
- Main verification: git rev-parse origin/main -> cacc6173a91f139269430e5670c3ae77146290a5
- Missed: frontend does not render measurementStatistics; existing totals keep prior closed/open meaning; no calibration
- Follow-up: none
- Residual risk: planning validator will report 0 READY for DEX/RL/DT after this close. Duplicate RL08 is marked OBSOLETE.
- Prompt defect / scope repair: queue header said docs/contracts only while the prompt body was a backend runtime slice; executed the prompt body because higher-priority execution programs had no READY and DEX/DT already shipped similar runtime slices. Marked RL08 OBSOLETE as a duplicate of RL06.

## RL05 - Prepare measurement-only recommendation statistics projection contract







Status: DONE



Priority: future / planning



Feature family: recommendation-learning-statistics-projection



Parallel-safe: yes, docs/contracts only



Owner: Codex



Local lock: removed after DONE







### Problem







Lifecycle capture and learning eligibility exist, but there was still no frozen measurement-only statistics projection contract that could count issued/accepted/executed/measured denominators without mutating confidence.







### Evidence







- `docs/Analytics/RECOMMENDATION_OUTCOME_LEARNING_CONTRACT.md`



- `docs/architecture/RECOMMENDATION_LEARNING_STATISTICS_ROLLOUT_PLAN.md`



- RL04 runtime eligibility projection







### Scope







- docs/contracts only for the measurement-only statistics DTO and denominator rules;



- no confidence calibration;



- no schema migration;



- no opaque scoring.







### Read first







- RL01 / RL02 / RL04



- Recommendation outcome learning contract



- Decision Intelligence roadmap







### Do







1. Define the measurement-only statistics response shape and denominators.



2. Require learning-eligible measured evidence before success/negative counts.



3. Keep acceptance and execution out of success denominators.



4. Gate any later calibration behind an explicit owner-promoted prompt.







### Tests







- contract keeps acceptance != success;



- missing measured evidence stays not_measured;



- no runtime implementation is authorized by this prompt.







### Acceptance







- a docs-only measurement-only statistics projection contract is ready for a later runtime slice;



- confidence mutation remains forbidden;



- READY pointer remains single for RL.







### Dependencies







- RL04 DONE.







### Completion note







- Date: 2026-08-13



- Agent: Codex



- Changed files:



- `docs/Analytics/RECOMMENDATION_MEASUREMENT_STATISTICS_CONTRACT.md`



  - `docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md`



  - `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md`



  - `MASTER_ROADMAP.md`



- Checks:



  - `node scripts/check-prompt-queues.mjs` - pass



  - `node scripts/check-planning-architecture.mjs` - pass



  - `git diff --check` - pass



- Remaining risk:



  - This is a docs-only measurement statistics contract; runtime projection still needs a later prompt.



- Next:



  - RL06 - Implement measurement-only recommendation statistics projection runtime slice





## RL08 - Measurement-only recommendation statistics follow-up





Status: OBSOLETE



Priority: future / planning



Feature family: recommendation-learning-statistics-projection



Parallel-safe: yes, backend-only



Owner: Codex



Local lock: removed after DONE





### Problem





The measurement-only statistics contract is frozen, but the runtime projection still needs a single backend slice that exposes the documented counts without mutating confidence or collapsing evidence gaps into zeroes.





### Evidence





- `docs/Analytics/RECOMMENDATION_MEASUREMENT_STATISTICS_CONTRACT.md`



- `docs/Analytics/RECOMMENDATION_OUTCOME_LEARNING_CONTRACT.md`



- `docs/architecture/RECOMMENDATION_LEARNING_STATISTICS_ROLLOUT_PLAN.md`



- `Infrastructure/Services/Analytics/AnalyticsActionItemService.cs`



- `Api/Endpoints/AnalyticsActionsEndpoints.cs`





### Scope





- backend-only runtime slice for the measurement-only statistics projection;



- preserve explicit denominators, warnings and empty-state semantics;



- do not add calibration or confidence mutation;



- no schema migration and no new event store.





### Read first





- RL05 completion note



- measurement statistics contract



- outcome learning contract



- rollout plan





### Do





1. Implement the smallest backend helper/service path that projects the documented measurement-only statistics.



2. Keep `not_measured`, `pending` and empty cohorts explicit.



3. Preserve warning codes and null denominators when evidence is missing.



4. Wire the projection through the existing outcome summary endpoint only if it can stay read-only.





### Tests





- focused `dotnet test` coverage for denominator handling;



- acceptance and execution are not success;



- missing measured evidence stays `not_measured`;



- empty cohorts stay empty with null rates.





### Acceptance





- the measurement-only statistics projection is exposed through the backend runtime path;



- the projection remains read-only and deterministic;



- confidence mutation and calibration remain out of scope;



- READY pointer remains single for RL.





### Dependencies





- RL05 DONE.







---







## DT07 - Implement Decision Timeline export and retrospective report runtime slice

Status: READY
Priority: future / planning
Feature family: decision-timeline-export-report
Parallel-safe: yes, backend/frontend report paths when RQ100 is not touching the same files
Owner: unassigned
Local lock: .ai/task-locks/DT07-<agent>.lock.md

### Problem

DT06 froze the export/retrospective contract, but there is still no runtime export or print/report document that reuses the Slice-2 filtered timeline without inventing events or silently widening the period.

### Evidence

- docs/architecture/DECISION_TIMELINE_EXPORT_REPORT_CONTRACT.md
- docs/architecture/DECISION_TIMELINE_CONTRACT.md
- docs/architecture/DECISION_TIMELINE_ROLLOUT_PLAN.md Slice 4
- DT05 Slice-2 filtered timeline

### Scope

- smallest runtime export/report path over the existing DT05 projection;
- honesty header, snapshot presence/absence and graceful export failure;
- no new event store and no invented replay history.

### Read first

- DT06 contract
- DT05 filtered timeline
- DEX10 snapshot vocabulary

### Do

1. Reuse the Slice-2 projection for export/report rows; do not rebuild history from live product rows.
2. Keep requested vs effective period equal; empty period stays empty with emptyReason.
3. Mark snapshots present or absent explicitly; live lookup must not be labelled snapshot.
4. Failed print/export must not fabricate zero rates.

### Tests

- focused backend or frontend proof that export/report failure is graceful;
- empty period does not silently widen;
- no fake events for missing stages.

### Acceptance

- a runtime timeline export or print/report path exists over Slice-2;
- READY pointer remains single for DT.

### Dependencies

- DT06 DONE;
- DT05 DONE.


## DT06 - Prepare Decision Timeline export and retrospective reporting contract







Status: DONE



Priority: future / planning



Feature family: decision-timeline-export-report



Parallel-safe: yes, docs/contracts only



Owner: Codex



Local lock: removed after DONE







### Problem







Filtered timeline Slice-2 exists for Product Decision Center, but export/reporting for decision retrospectives still lacks a frozen docs contract for period, freshness, data quality and snapshot-linked evidence.







### Evidence







- `docs/architecture/DECISION_TIMELINE_CONTRACT.md`



- `docs/architecture/DECISION_TIMELINE_ROLLOUT_PLAN.md` Slice 5 export/reporting



- DT03/DT05 projection surfaces



- DEX10 immutable evidence snapshots







### Scope







- docs/contracts only for timeline export/report fields and honesty rules;



- no runtime export implementation;



- no invented replay history.







### Read first







- DT01 / DT02 / DT05



- DEX10 evidence snapshot notes



- Decision Timeline rollout plan







### Do







1. Define export/report fields for recommendation -> action -> outcome timelines.



2. Require snapshot presence/absence and empty-period honesty.



3. Keep print/export failure graceful in the contract language.



4. Do not authorize runtime work in this prompt.







### Tests







- contract forbids fake events and silent period widening;



- snapshot links remain optional but explicit when absent;



- no runtime code lands under this prompt.







### Acceptance







- a docs-only timeline export/retrospective contract exists;



- filtered Slice-2 remains the live runtime baseline;



- READY pointer remains single for DT.







### Dependencies







- DT05 DONE.



- DEX10 DONE (snapshot vocabulary reuse).







### Completion note

- Date: 2026-08-13
- Status: DONE
- Completion: 92%
- Changed files: docs/architecture/DECISION_TIMELINE_EXPORT_REPORT_CONTRACT.md; docs/architecture/DECISION_TIMELINE_CONTRACT.md; docs/architecture/DECISION_TIMELINE_ROLLOUT_PLAN.md; docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md; docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md; MASTER_ROADMAP.md; .ai/runs/2026-08-13-DT06-evidence.md
- Checks run: node scripts/check-prompt-queues.mjs --self-test pass; node scripts/check-prompt-queues.mjs pass; node scripts/check-planning-architecture.mjs --self-test pass; node scripts/check-planning-architecture.mjs fail (DEX 0 READY, DT 0 READY after close; RL06 still READY); node scripts/check-agent-instructions.mjs --self-test pass; node scripts/check-agent-instructions.mjs pass; git diff --check pass
- Checks not run: dotnet build/test and npm - docs-only, no runtime
- Run log: .ai/runs/2026-08-13-DT06-evidence.md
- Delivery mode: direct-main
- Main commit SHA: 9c82d0745656a2905ace3424605d6caa0dd498ad
- Main verification: git rev-parse origin/main -> e8d178538c2e9134bf5761ede9729838ab198d4a
- Missed: no runtime export endpoint or print CSS; no DT07 invented
- Follow-up: none
- Residual risk: planning validator still requires exactly one READY per DEX/DT; those lanes are empty by design until an owner creates a successor. Runtime export remains unauthorized.
- Prompt defect / scope repair: corrected stale DEX17 READY pointer in the Decision Intelligence header, roadmap current-READY sentence, and MASTER because DEX17 Status is DONE; no DEX18 invented. Did not start RL06 runtime from a planning READY.



## DEX10 - Implement Product Decision Center evidence snapshot contract







Status: DONE



Priority: future-high-value / implementation



Feature family: decision-explainability-product-decision-center-phase7



Parallel-safe: no, coupled backend/frontend contract



Owner: Cursor



Local lock: removed after DONE



Commit: `b52938cb647807c0bb36af68cc766a843a1466cc`



Completed: 2026-08-11







### Problem







The deterministic Decision Graph, evidence chain, alternatives, Why panel and decision-tree surfaces are now explicit, but acted-on recommendations still need an immutable evidence snapshot so later review does not silently drift when source data changes.







### Evidence







- `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md` puts decision evidence snapshots after the decision tree in the explainability sequence.



- `docs/architecture/DECISION_GRAPH_CONTRACT.md` already treats authoritative evidence as backend-led and versioned.



- `Api/Endpoints/CachedAnalyticsEndpoints.cs` already carries the row-level decision evidence needed to freeze a snapshot at action time.



- `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx` still lacks a first-class snapshot or evidence-history surface.







### Scope







- `Api/Endpoints/CachedAnalyticsEndpoints.cs`



- `Api.Tests/ProductDecisionCenterBuilderIntegrationTests.cs`



- `Api.Tests/AnalyticsProductDecisionConfidenceTests.cs`



- `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx`



- `Klijent/clientapp/src/pages/__tests__/ProductDecisionCenterPage.confidence.spec.tsx`



- `Klijent/clientapp/src/types/analytics.ts`







### Read first







- DEX09 output



- `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md`



- `docs/architecture/DECISION_GRAPH_CONTRACT.md`



- `docs/qa/PRODUCT_DECISION_CONFIDENCE_AUDIT.md`







### Do







1. Freeze a versioned evidence snapshot when a recommendation is acted on.



2. Keep the snapshot immutable even if source analytics refresh later.



3. Surface snapshot presence/absence explicitly in Product Decision Center.



4. Preserve the existing deterministic Decision Tree and Why panel contracts.







### Tests







- backend coverage proves snapshot payloads are stable and versioned;



- frontend coverage shows snapshot state without inventing history;



- no fake replay, fake evidence or inferred timeline is introduced.







### Acceptance







- acted recommendations retain an immutable evidence snapshot;



- later data changes do not rewrite historical decision evidence;



- the UI can show whether a snapshot exists and when it was captured.







### Dependencies







- DEX09 DONE.







### Completion notes







- Action upsert freezes `ledger.evidenceSnapshot` (schema v1) with period, confidence, reason codes, evidence chain and confidence breakdown.



- Creation/evidence snapshots stay immutable once written; outcome updates only merge resolution.



- PDC rows expose `evidenceSnapshotStatus=absent` plus a live preview contract until action time; UI shows absent vs captured timestamp after queue.



- Focused tests: `AnalyticsActionItemServiceTests` immutability, PDC builder/confidence snapshot preview, frontend confidence snapshot panel.







---







## DEX09 - Implement Product Decision Center deterministic decision-tree contract







Status: DONE



Priority: future-high-value / implementation



Feature family: decision-explainability-product-decision-center-phase6



Parallel-safe: no, coupled backend/frontend contract



Owner: Codex



Local lock: removed after DONE







### Problem







The Why panel now has a deterministic backend-led contract, but Product Decision Center still needs a separate deterministic decision-tree or branch-path view for rule-based logic so users can see which branch or guard produced the recommendation instead of collapsing every decision into a single explanation string.







### Evidence







- `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md` places Decision Tree immediately after the Why panel in the explainability sequence.



- `docs/architecture/DECISION_GRAPH_CONTRACT.md` keeps the decision graph and branch-path vocabulary deterministic and backend-led.



- `Api/Endpoints/CachedAnalyticsEndpoints.cs` already exposes the row-level decision fields that can anchor rule-path explanations when the logic is branch-based.



- `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx` still renders the decision summary without an explicit branch-path surface.







### Scope







- `Api/Endpoints/CachedAnalyticsEndpoints.cs`



- `Api.Tests/ProductDecisionCenterBuilderIntegrationTests.cs`



- `Api.Tests/AnalyticsProductDecisionConfidenceTests.cs`



- `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx`



- `Klijent/clientapp/src/pages/__tests__/ProductDecisionCenterPage.confidence.spec.tsx`



- `Klijent/clientapp/src/types/analytics.ts`







### Read first







- DEX08 output



- `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md`



- `docs/architecture/DECISION_GRAPH_CONTRACT.md`



- `docs/qa/PRODUCT_DECISION_CONFIDENCE_AUDIT.md`







### Do







1. Expose deterministic decision-tree or branch-path inputs from backend only when rule-based logic applies.



2. Render the branch path explicitly in Product Decision Center without inventing a local rule trace.



3. Keep non-rule-based recommendations honest by showing that no decision tree exists when the engine did not branch.



4. Preserve Decision Board compatibility and the existing Why-panel contract.







### Tests







- backend coverage confirms branch-path nodes are stable and traceable;



- frontend coverage shows the branch path or the absence of one explicitly;



- no fake zero, fake branch path or inferred rule trace is introduced.







### Acceptance







- Product Decision Center can show a deterministic branch path where rule-based logic exists;



- missing rule branches remain explicit;



- no frontend-local inference invents a decision tree.







### Dependencies







- DEX08 DONE.







### Progress







- claimed by Codex on 2026-08-11



- implemented deterministic Product Decision Center decision-tree payload, backend branching helpers, frontend rendering, and regression coverage







### Completion note







- Date: 2026-08-11



- Agent: Codex



- Commit SHA: c153b71



- Changed files:



  - `Api/Endpoints/CachedAnalyticsEndpoints.cs`



  - `Api.Tests/AnalyticsProductDecisionConfidenceTests.cs`



  - `Api.Tests/ProductDecisionCenterBuilderIntegrationTests.cs`



  - `Klijent/clientapp/src/pages/ProductDecisionCenterPage.css`



  - `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx`



  - `Klijent/clientapp/src/pages/__tests__/ProductDecisionCenterPage.confidence.spec.tsx`



  - `Klijent/clientapp/src/types/analytics.ts`



  - `docs/architecture/DECISION_GRAPH_CONTRACT.md`



  - `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md`



  - `docs/qa/PRODUCT_DECISION_CONFIDENCE_AUDIT.md`



  - `docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md`



- Checks:



  - `dotnet test .\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~ProductDecisionCenterBuilderIntegrationTests|FullyQualifiedName~AnalyticsProductDecisionConfidenceTests"` - pass



  - `npm run test -- --run src/pages/__tests__/ProductDecisionCenterPage.confidence.spec.tsx` - pass



  - `npm run build` - pass



  - `git diff --check` - pass



- Risk:



  - Older payloads may omit `decisionTree`, so the UI still needs an explicit absence state.



- Next:



  - `RL01 - Define recommendation outcome-learning contract`







---







## DEX08 - Implement Product Decision Center deterministic Why-panel contract







Status: DONE



Priority: future-high-value / implementation



Feature family: decision-explainability-product-decision-center-phase5



Parallel-safe: no, coupled backend/frontend contract



Owner: unassigned



Local lock: removed after DONE







### Problem







DEX07 makes alternatives explicit, but the Product Decision Center Why panel still needs a single deterministic contract for how reason, evidence, confidence and alternatives are composed into the concise user-facing explanation instead of relying on ad hoc UI glue or free-form inference.







### Progress







- claimed by Codex on 2026-08-11



- completed a single backend-led Why-panel contract for Product Decision Center







### Evidence







- `docs/architecture/DECISION_GRAPH_CONTRACT.md` defines the Why panel as deterministic backend fields only.



- `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md` places Why panel after alternatives in the explainability sequence.



- `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx` now renders the explanation surface from the backend WhyPanel bundle and preserves explicit fallback labels.



- `docs/qa/PRODUCT_DECISION_CONFIDENCE_AUDIT.md` keeps Product Decision Center as the reference implementation for the shared explainability contract.







### Scope







- `Api/Endpoints/CachedAnalyticsEndpoints.cs`



- `Api.Tests/ProductDecisionCenterBuilderIntegrationTests.cs`



- `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx`



- `Klijent/clientapp/src/pages/__tests__/ProductDecisionCenterPage.confidence.spec.tsx`



- `Klijent/clientapp/src/types/analytics.ts`







### Read first







- DEX07 output



- `docs/architecture/DECISION_GRAPH_CONTRACT.md`



- `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md`



- `docs/qa/PRODUCT_DECISION_CONFIDENCE_AUDIT.md`







### Do







1. Compose the Why panel from backend-authoritative deterministic fields only.



2. Keep evidence, confidence and alternatives grouped without inventing missing context.



3. Preserve explicit missing-data and fallback states.



4. Keep Decision Board compatibility unchanged.







### Tests







- frontend coverage verifies Why-panel composition and regression paths;



- backend contract coverage still confirms the same deterministic fields;



- no fake zero, fake alternative or inferred explanation is introduced.







### Acceptance







- Product Decision Center renders a single deterministic Why panel from backend fields;



- missing evidence remains explicit;



- no local inference path invents explanation structure.







### Dependencies







- DEX07 DONE.







### Completion note







- Date: 2026-08-11



- Agent: Codex



- Commit SHA: 9c5f355



- Changed files:



  - `Api/Endpoints/CachedAnalyticsEndpoints.cs`



  - `Api.Tests/AnalyticsProductDecisionConfidenceTests.cs`



  - `Api.Tests/ProductDecisionCenterBuilderIntegrationTests.cs`



  - `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx`



  - `Klijent/clientapp/src/pages/__tests__/ProductDecisionCenterPage.confidence.spec.tsx`



  - `Klijent/clientapp/src/types/analytics.ts`



  - `docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md`



- Checks:



  - `dotnet test .\\Api.Tests\\Api.Tests.csproj --filter "FullyQualifiedName~ProductDecisionCenterBuilderIntegrationTests|FullyQualifiedName~AnalyticsProductDecisionConfidenceTests"` - pass



  - `npm run test -- --run src/pages/__tests__/ProductDecisionCenterPage.confidence.spec.tsx` - pass



  - `npm run build` - pass



  - `git diff --check` - pass



- Risk:



  - The Why panel is deterministic and backend-led, but the frontend still keeps a compatibility fallback if older payloads omit `whyPanel`.



- Next:



  - `DEX09 - Implement Product Decision Center deterministic decision-tree contract`







---







## DEX07 - Implement Product Decision Center alternative recommendations contract







Status: DONE



Priority: future-high-value / implementation



Feature family: decision-explainability-product-decision-center-phase4



Parallel-safe: no, coupled backend/frontend contract



Owner: unassigned



Local lock: removed after DONE







### Problem







DEX06 makes confidence contributors explicit, but the Why panel still needs deterministic alternative recommendations that show which valid actions were considered and why the selected recommendation ranked above them instead of leaving alternatives implicit or inventing them locally.







### Completion note







- 2026-08-11: completed by Codex.



- Commit SHA: b11de57.



- Changed files:



  - `Api/Endpoints/CachedAnalyticsEndpoints.cs`



  - `Api.Tests/AnalyticsProductDecisionConfidenceTests.cs`



  - `Api.Tests/ProductDecisionCenterBuilderIntegrationTests.cs`



  - `Klijent/clientapp/src/pages/ProductDecisionCenterPage.css`



  - `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx`



  - `Klijent/clientapp/src/pages/__tests__/ProductDecisionCenterPage.confidence.spec.tsx`



  - `Klijent/clientapp/src/types/analytics.ts`



  - `docs/architecture/DECISION_GRAPH_CONTRACT.md`



  - `docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md`



- Checks:



  - `dotnet test .\\Api.Tests\\Api.Tests.csproj --filter "FullyQualifiedName~ProductDecisionCenterBuilderIntegrationTests|FullyQualifiedName~AnalyticsProductDecisionConfidenceTests"` - pass



  - `npm run test -- --run src/pages/__tests__/ProductDecisionCenterPage.confidence.spec.tsx` - pass



  - `dotnet build` - pass



  - `npm run check:analytics-guardrails` - pass



  - `npm run build` - pass



  - `node scripts/check-prompt-queues.mjs` - pass



  - `node scripts/check-planning-architecture.mjs` - fail (existing MASTER_ROADMAP.md gaps for BCI, STAB and RQ)



- Risk:



  - Alternative ranking is deterministic but still heuristic-weighted and should be revisited if Product Decision Center semantics expand.



- Next:



  - `DEX08 - Implement Product Decision Center deterministic Why-panel contract`







### Evidence







- `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md` places alternative recommendations after confidence breakdown.



- `Api/Endpoints/CachedAnalyticsEndpoints.cs` already computes deterministic product decision evidence and confidence state that can anchor alternative reasoning.



- `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx` still lacks an explicit alternatives view in the Why panel.



- `docs/qa/DECISION_BOARD_CANDIDATE_CONTRACT_AUDIT.md` shows the downstream Decision Board should keep consuming the same canonical semantics.







### Scope







- `Api/Endpoints/CachedAnalyticsEndpoints.cs`



- `Api.Tests/ProductDecisionCenterBuilderIntegrationTests.cs`



- `Api.Tests/AnalyticsProductDecisionConfidenceTests.cs`



- `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx`



- `Klijent/clientapp/src/pages/__tests__/ProductDecisionCenterPage.confidence.spec.tsx`



- `Klijent/clientapp/src/types/analytics.ts`







### Read first







- DEX06 output



- `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md`



- `docs/Analytics/DECISION_CONFIDENCE_CONTRACT.md`



- `docs/qa/PRODUCT_DECISION_CONFIDENCE_AUDIT.md`







### Do







1. Expose alternative candidates or alternative decision metadata deterministically from backend.



2. Render alternatives in Product Decision Center without local heuristics or invented options.



3. Keep rejected or unavailable alternatives explicit.



4. Preserve missing-data semantics and downstream Decision Board compatibility.







### Tests







- backend coverage confirms alternatives are deterministic and traceable;



- frontend coverage shows alternatives and keeps missing values explicit;



- API failure and empty-state regressions still avoid fake zero or fake alternatives;



- downstream Decision Board compatibility does not regress.







### Acceptance







- Product Decision Center can explain valid alternatives and why they ranked lower;



- no frontend-local heuristic invents alternatives;



- no Decision Board regression was introduced.







### Dependencies







- DEX06 DONE.







---







## DEX06 - Implement Product Decision Center confidence breakdown contract







Status: DONE



Priority: future-high-value / implementation



Feature family: decision-explainability-product-decision-center-phase3



Parallel-safe: no, coupled backend/frontend contract



Owner: unassigned



Local lock: removed after DONE







### Problem







DEX05 added a structured evidence chain to Product Decision Center, but the Why panel still needs a deterministic confidence breakdown that explains how freshness, coverage, reliability and data quality contribute to the final confidence level instead of leaving confidence as a single opaque number.







### Evidence







- `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md` places confidence breakdown immediately after evidence chain in the decision explainability sequence.



- `Api/Endpoints/CachedAnalyticsEndpoints.cs` already computes backend confidence, reliability and warning signals that can be exposed as a structured breakdown.



- `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx` still presents confidence as a compact label and needs a richer deterministic breakdown view.



- `docs/qa/DECISION_BOARD_CANDIDATE_CONTRACT_AUDIT.md` shows the downstream Decision Board should keep consuming the same canonical semantics.







### Scope







- `Api/Endpoints/CachedAnalyticsEndpoints.cs`



- `Api.Tests/ProductDecisionCenterBuilderIntegrationTests.cs`



- `Api.Tests/AnalyticsProductDecisionConfidenceTests.cs`



- `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx`



- `Klijent/clientapp/src/pages/__tests__/ProductDecisionCenterPage.confidence.spec.tsx`



- `Klijent/clientapp/src/types/analytics.ts`







### Read first







- DEX05 output



- `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md`



- `docs/Analytics/DECISION_CONFIDENCE_CONTRACT.md`



- `docs/qa/PRODUCT_DECISION_CONFIDENCE_AUDIT.md`







### Do







1. Expose confidence contributors from backend as deterministic fields or structured breakdown items.



2. Render confidence contributors in Product Decision Center without recreating business logic locally.



3. Preserve missing, stale and insufficient-data semantics explicitly.



4. Keep the downstream Decision Board semantics unchanged.







### Tests







- backend coverage confirms the confidence breakdown is stable and traceable;



- frontend coverage shows the structured confidence breakdown and keeps missing values explicit;



- API failure and empty-state regressions still avoid fake zero or fake confidence;



- downstream Decision Board compatibility does not regress.







### Acceptance







- Product Decision Center can explain confidence with a structured deterministic breakdown;



- no frontend-local heuristic invents confidence contributors;



- no Decision Board regression was introduced.







### Dependencies







- DEX05 DONE.







### Completion note







- Date: 2026-08-11



- Agent: Codex



- Commit SHA: 63300cb



- Changed files:



  - `Api/Endpoints/CachedAnalyticsEndpoints.cs`



  - `Api/Endpoints/InventoryEndpoints.cs`



  - `Api.Tests/AnalyticsProductDecisionConfidenceTests.cs`



  - `Api.Tests/ProductDecisionCenterBuilderIntegrationTests.cs`



  - `Klijent/clientapp/src/pages/ProductDecisionCenterPage.css`



  - `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx`



  - `Klijent/clientapp/src/pages/__tests__/ProductDecisionCenterPage.confidence.spec.tsx`



  - `Klijent/clientapp/src/types/analytics.ts`



  - `docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md`



- Checks:



  - `dotnet test .\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~ProductDecisionCenterBuilderIntegrationTests|FullyQualifiedName~AnalyticsProductDecisionConfidenceTests"` - pass



  - `npm run test -- --run src/pages/__tests__/ProductDecisionCenterPage.confidence.spec.tsx src/pages/__tests__/ProductDecisionCenterPage.actionStatusFallback.spec.tsx src/pages/__tests__/ProductDecisionCenterPage.queueStatus.spec.tsx` - pass



  - `dotnet build` - pass



  - `npm run check:analytics-guardrails` - pass



  - `npm run build` - pass



  - `node scripts/check-prompt-queues.mjs` - pass



  - `node scripts/check-planning-architecture.mjs` - fail (existing unrelated MASTER_ROADMAP gaps remain)



  - `git diff --check -- docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md .ai/task-locks/DEX06-codex.lock.md Api/Endpoints/CachedAnalyticsEndpoints.cs Api/Endpoints/InventoryEndpoints.cs Api.Tests/AnalyticsProductDecisionConfidenceTests.cs Api.Tests/ProductDecisionCenterBuilderIntegrationTests.cs Klijent/clientapp/src/types/analytics.ts Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx Klijent/clientapp/src/pages/ProductDecisionCenterPage.css Klijent/clientapp/src/pages/__tests__/ProductDecisionCenterPage.confidence.spec.tsx` - pass



- Remaining risk:



  - `node scripts/check-planning-architecture.mjs` still reports unrelated `MASTER_ROADMAP.md` link gaps outside this task.



  - The client build still emits the existing chunk-size warning for `recharts`.



## DEX05 - Implement Product Decision Center evidence chain drill-down contract







Status: DONE



Priority: future-high-value / implementation



Feature family: decision-explainability-product-decision-center-phase2



Parallel-safe: no, coupled backend/frontend contract



Owner: unassigned



Local lock: removed after DONE







### Problem







DEX04 aligned Product Decision Center on deterministic confidence, impact, warning and freshness fields, but the Why/drill-down view still needs a structured evidence chain that traces each explanation back to concrete backend signals instead of a plain text summary.







### Evidence







- `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md` sequences decision explainability from evidence chain through confidence breakdown, alternatives, drill-down and why panel.



- `docs/architecture/DECISION_GRAPH_CONTRACT.md` captures the decision graph vocabulary that should back the drill-down view.



- `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx` still shows explanation text and reason codes without a structured evidence chain panel.



- `docs/qa/DECISION_BOARD_CANDIDATE_CONTRACT_AUDIT.md` shows the downstream Decision Board should keep consuming the same canonical semantics.







### Scope







- `Api/Endpoints/CachedAnalyticsEndpoints.cs`



- `Api.Tests/ProductDecisionCenterBuilderIntegrationTests.cs`



- `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx`



- `Klijent/clientapp/src/pages/__tests__/ProductDecisionCenterPage.confidence.spec.tsx`



- `Klijent/clientapp/src/pages/__tests__/ProductDecisionCenterPage.queueStatus.spec.tsx`



- `Klijent/clientapp/src/pages/__tests__/AnalyticsSalesReadinessRegression.spec.tsx` if the new drill-down data path changes error or empty-state behavior







### Read first







- DEX04 output



- `docs/architecture/DECISION_GRAPH_CONTRACT.md`



- `docs/Analytics/DECISION_CONFIDENCE_CONTRACT.md`



- `docs/qa/DECISION_BOARD_CANDIDATE_CONTRACT_AUDIT.md`







### Do







1. Keep evidence items backend-led and deterministic.



2. Render the evidence chain in the Product Decision Center Why/drill-down experience without reconstructing business truth locally.



3. Preserve null and missing-evidence behavior explicitly.



4. Keep the downstream Decision Board semantics unchanged.







### Tests







- backend coverage confirms the evidence chain payload is stable and traceable;



- frontend drill-down coverage shows concrete evidence nodes, not only free-form text;



- API failure and empty-state regressions still avoid fake zero or fake evidence;



- downstream Decision Board compatibility does not regress.







### Acceptance







- Product Decision Center can explain a recommendation with a structured evidence chain that maps to backend signals;



- no frontend-local heuristic invents evidence nodes;



- no Decision Board regression was introduced.







### Dependencies







- DEX04 DONE.







### Completion note







- Date: 2026-08-11



- Agent: Codex



- Changed files:



  - `Api/Endpoints/CachedAnalyticsEndpoints.cs`



  - `Api.Tests/AnalyticsProductDecisionConfidenceTests.cs`



  - `Api.Tests/ProductDecisionCenterBuilderIntegrationTests.cs`



  - `Klijent/clientapp/src/pages/ProductDecisionCenterPage.css`



  - `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx`



  - `Klijent/clientapp/src/pages/__tests__/ProductDecisionCenterPage.confidence.spec.tsx`



  - `Klijent/clientapp/src/types/analytics.ts`



  - `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md`



  - `docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md`



- Checks:



  - `dotnet test .\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~ProductDecisionCenterBuilderIntegrationTests|FullyQualifiedName~AnalyticsProductDecisionConfidenceTests"` - pass



- `npm run test -- --run src/pages/__tests__/ProductDecisionCenterPage.confidence.spec.tsx src/pages/__tests__/ProductDecisionCenterPage.actionStatusFallback.spec.tsx src/pages/__tests__/ProductDecisionCenterPage.queueStatus.spec.tsx` - pass



  - `npm run check:analytics-guardrails` - pass



  - `npm run build` - pass



  - `node scripts/check-prompt-queues.mjs` - pass



  - `node scripts/check-planning-architecture.mjs` - pass



  - `git diff --check -- docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md` - pass



- Remaining risk:



  - The client app build still emits the existing chunk-size warning for `recharts`.



- Next:



  - `DEX06 - Implement Product Decision Center confidence breakdown contract`







## DEX04 - Implement Product Decision Center deterministic explainability contract







Status: DONE



Priority: future-high-value / implementation



Feature family: decision-explainability-product-decision-center-phase1



Parallel-safe: no, coupled backend/frontend contract



Owner: unassigned



Local lock: removed after DONE







### Problem







Product Decision Center already has the deterministic backend explainability profile, but the page still carries local confidence, impact and freshness derivations. The first executable implementation prompt needs to align backend, frontend and regression tests around the backend as source of truth without drifting into Decision Board refactoring.







### Evidence







- `docs/Analytics/DECISION_CONFIDENCE_CONTRACT.md` already names Product Decision Center as the Phase 1 reference module.



- `Api.Tests/ProductDecisionCenterBuilderIntegrationTests.cs` verifies backend recommendation, confidence and impact behavior.



- `Api.Tests/AnalyticsProductDecisionConfidenceTests.cs` verifies the backend confidence profile for high-confidence and insufficient-data rows.



- `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx` still derives confidence, impact, warning and freshness presentation locally.



- `Klijent/clientapp/src/pages/__tests__/ProductDecisionCenterPage.confidence.spec.tsx` and `ProductDecisionCenterPage.actionStatusFallback.spec.tsx` already cover the user-facing contract that must remain honest.



- `docs/qa/PRODUCT_DECISION_CONFIDENCE_AUDIT.md` and `docs/qa/DECISION_BOARD_CANDIDATE_CONTRACT_AUDIT.md` show Product Decision Center is the right first bounded family and Decision Board is the downstream consumer.







### Scope







- `Api/Endpoints/CachedAnalyticsEndpoints.cs`



- `Api.Tests/ProductDecisionCenterBuilderIntegrationTests.cs`



- `Api.Tests/AnalyticsProductDecisionConfidenceTests.cs`



- `Klijent/clientapp/src/types/analytics.ts`



- `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx`



- `Klijent/clientapp/src/pages/__tests__/ProductDecisionCenterPage.confidence.spec.tsx`



- `Klijent/clientapp/src/pages/__tests__/ProductDecisionCenterPage.actionStatusFallback.spec.tsx`



- `Klijent/clientapp/src/pages/__tests__/AnalyticsSalesReadinessRegression.spec.tsx`



- `Klijent/clientapp/src/pages/__tests__/ProductDecisionCenterPage.queueStatus.spec.tsx` if the action metadata snapshot contract changes







### Read first







- DEX01 output



- DEX02 output



- DEX03 output



- `docs/Analytics/DECISION_CONFIDENCE_CONTRACT.md`



- `docs/Analytics/ANALYTICS_DECISION_OS_ROADMAP.md`



- `docs/qa/PRODUCT_DECISION_CONFIDENCE_AUDIT.md`



- `docs/qa/DECISION_BOARD_CANDIDATE_CONTRACT_AUDIT.md`







### Do







1. Keep Product Decision Center rows and response metadata backend-led for recommendation identity, confidence, drivers, warnings, expected impact, impact window, risk and freshness.



2. Remove or narrow frontend heuristics so the page formats backend fields instead of inventing confidence, impact or freshness substitutes.



3. Preserve null, missing and insufficient-data semantics, including explicit stale/critical cases and no fake zero.



4. Keep the action queue metadata snapshot aligned with the authoritative backend explainability fields.



5. Leave Decision Board behavior unchanged except for regression coverage that proves the shared contract still holds downstream.







### Tests







- backend integration covers a high-confidence replenish row and an insufficient-data row with honest nullable impact;



- backend unit coverage confirms the explainability profile includes stable identity, confidence level, drivers, warnings, impact, risk and freshness;



- frontend confidence specs cover strong recommendations, missing impact, insufficient-data and stale freshness;



- API failure and empty-state regressions do not render fake zero or fake confidence;



- downstream Decision Board compatibility does not regress.







### Acceptance







- Product Decision Center render, detail and action metadata are driven from deterministic backend explainability fields;



- null/missing/stale/critical signals remain explicit;



- no frontend-local heuristic invents business truth for confidence or impact;



- no Decision Board regression was introduced.







### Completion note







- Date: 2026-08-11



- Agent: Codex



- Changed files:



  - `Api.Tests/ProductDecisionCenterBuilderIntegrationTests.cs`



  - `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx`



  - `Klijent/clientapp/src/pages/__tests__/ProductDecisionCenterPage.confidence.spec.tsx`



  - `Klijent/clientapp/vite.config.ts`



  - `Klijent/clientapp/vitest.config.ts`



  - `docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md`



  - `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md`



- Checks:



  - `git diff --check -- Api.Tests/ProductDecisionCenterBuilderIntegrationTests.cs Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx Klijent/clientapp/src/pages/__tests__/ProductDecisionCenterPage.confidence.spec.tsx Klijent/clientapp/vite.config.ts Klijent/clientapp/vitest.config.ts docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md` - pass



  - `npm.cmd run check:analytics-guardrails` - pass



  - `dotnet test Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~ProductDecisionCenterBuilderIntegrationTests --no-restore` - pass



  - `npm.cmd run build` - pass



  - `npm.cmd run test -- --run src/pages/__tests__/ProductDecisionCenterPage.confidence.spec.tsx` - pass



- Remaining risk:



  - The client app build still emits the existing chunk-size warning for `recharts`.



- Next:



  - `DEX05 - Implement Product Decision Center evidence chain drill-down contract`







### Dependencies







- DEX03 DONE.







## DEX03 - Prepare Product Decision Center explainability implementation prompt







Status: DONE



Priority: future-high-value / planning



Feature family: decision-explainability-product-decision-center



Parallel-safe: yes, docs/contracts only



Owner: unassigned







### Problem







DEX02 selects Product Decision Center as the first bounded family, but the next implementation prompt still needs a small, reviewable boundary that names the exact backend, frontend and hardening surfaces without expanding into the whole decision system.







### Evidence







- DEX01 defined the deterministic Decision Graph and evidence-chain contract.



- DEX02 selected Product Decision Center as the first-family rollout target.



- `docs/qa/PRODUCT_DECISION_CONFIDENCE_AUDIT.md` shows Product Decision Center already exposes confidence, impact, warnings and freshness semantics.



- `docs/qa/DECISION_BOARD_CANDIDATE_CONTRACT_AUDIT.md` shows Decision Board already consumes Product Decision Center semantics downstream.







### Scope







- docs/planning only;



- one bounded implementation prompt for Product Decision Center explainability;



- enumerate exact backend DTO fields, frontend Why/drill-down inputs, evidence snapshot requirements and regression tests;



- no production code changes in this prompt.







### Read first







- DEX01 output



- DEX02 output



- `docs/architecture/DECISION_GRAPH_CONTRACT.md`



- `docs/qa/PRODUCT_DECISION_CONFIDENCE_AUDIT.md`



- `docs/qa/DECISION_BOARD_CANDIDATE_CONTRACT_AUDIT.md`







### Do







1. Split Product Decision Center work into backend, frontend and hardening slices.



2. Define the first implementation prompt boundary and what stays for later.



3. Keep additive compatibility rules explicit.



4. Call out the fields that must remain backend-led and deterministic.







### Tests







- implementation prompt does not invent confidence, impact or alternatives;



- no AI dependency or runtime graph engine;



- true zero, missing evidence and stale evidence remain explicit;



- no frontend-only decision logic is introduced.







### Acceptance







- one executable Product Decision Center implementation prompt exists with bounded scope and follow-up slices;



- no production behavior change was introduced by this planning prompt.







### Completion note







- Date: 2026-08-11



- Agent: Codex



- Changed files:



  - `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md`



  - `docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md`



- Checks:



  - `git diff --check -- docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md` - pass



  - `node scripts/check-prompt-queues.mjs` - pass



  - `node scripts/check-planning-architecture.mjs` - pass



- Remaining risk:



  - DEX04 still needs runtime execution to land the Product Decision Center explainability contract.



- Next:



  - `DEX04 - Implement Product Decision Center deterministic explainability contract`







### Dependencies







- DEX02 DONE.







---







## DEX02 - Prepare first-family explainability rollout plan







Status: DONE



Priority: future



Feature family: decision-explainability-rollout-plan



Parallel-safe: yes, planning only



Owner: unassigned







### Problem







After DEX01, runtime rollout still needs a bounded first family, exact surface list and compatibility plan before implementation prompts are created.







### Evidence







- DEX01 will define the common contract.



- Existing recommendation families differ in impact/confidence/evidence maturity.







### Scope







- docs/planning only;



- select one family and enumerate API/detail/export/action surfaces;



- no runtime prompt generation beyond a proposed split list.







### Read first







- DEX01 output



- selected family queue/audit/tests



- `docs/planning/FEATURE_LIFECYCLE.md`







### Do







1. Score candidate families by business value, evidence completeness and implementation risk.



2. Select one first family.



3. Define rollout slices: backend contract, frontend Why/drill-down, evidence snapshot, hardening.



4. Identify exact dependencies and stop conditions.







### Tests







- plan maps every affected surface;



- no duplicate RQ/STAB ownership;



- no slice exceeds a reviewable feature family.







### Acceptance







- implementation can later be promoted through separate bounded prompts without duplicating existing queues.







### Completion note







- Date: 2026-08-11



- Agent: Codex



- Changed files:



  - `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md`



  - `docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md`



- Checks:



  - `git diff --check -- docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md` - pass



  - `node scripts/check-prompt-queues.mjs` - pass



  - `node scripts/check-planning-architecture.mjs` - pass



- Remaining risk:



  - rollout plan is documentation-only; no runtime Product Decision Center implementation was added.



- Next:



  - `DEX03 - Prepare Product Decision Center explainability implementation prompt`







### Dependencies







- DEX01 DONE.







---







## DEX01 - Define deterministic Decision Graph and evidence-chain contract







Status: DONE



Priority: future-high-value / planning



Feature family: decision-explainability-contract



Parallel-safe: yes, docs/contracts only



Owner: unassigned



Local lock: removed after DONE







### Problem







Trendplus recommendations already expose reasons, confidence and impact in multiple areas, but there is no single deterministic contract describing how a user can trace a recommendation through evidence, confidence contributors, alternatives and drill-down without relying on AI-generated interpretation.







### Evidence







- The current product already treats backend recommendation/confidence/reason semantics as authoritative.



- Existing analytics reliability work explicitly protects no-fake-zero/no-fake-green behavior.



- Decision Board, Product Decision Center, inventory/supplier analytics and action/outcome workflows already provide candidate inputs, but they are not organized as a reusable Decision Graph contract.



- `docs/architecture/ADRS.md` ADR-001/004/005/006/007 define the governing principles.







### Scope







- `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md`



- one new deterministic Decision Graph contract document under `docs/architecture/` or `docs/product/`



- inventory of existing DTO/reason/confidence/evidence fields needed to map the first recommendation family



- test-plan/contract fixtures only if useful; no production behavior change







### Read first







- `MASTER_ROADMAP.md`



- `docs/product/PRODUCT_VISION.md`



- `docs/architecture/ADRS.md`



- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md`



- current Decision Board / Product Decision Center contracts relevant to the selected first family







### Do







1. Define node types for decision, evidence, confidence contributor, rule/constraint, alternative and action link.



2. Define stable identifiers/correlation expectations without using IDs as authorization.



3. Define evidence fields for value, unit/denominator, source status, freshness, data quality and timestamp.



4. Define confidence breakdown semantics that cannot manufacture certainty from missing evidence.



5. Define alternative-decision contract and ranking/exclusion reason vocabulary.



6. Define deterministic Why-panel rendering inputs.



7. Choose one existing recommendation family as the first mapping example without implementing runtime graph generation.



8. Record explicit compatibility/non-goals and the later implementation split.







### Tests







- contract examples cover positive evidence, true zero, missing evidence, stale evidence and partial/low-confidence evidence;



- alternatives include selected/not-selected reason;



- a Why panel can be rendered entirely from deterministic fields;



- no AI/provider dependency appears in the contract;



- no frontend-only confidence/recommendation rule is introduced.







### Acceptance







- one reusable Decision Graph/evidence-chain contract is documented;



- confidence breakdown and alternative semantics are explicit;



- the first future runtime implementation can be scoped to one recommendation family;



- no runtime implementation was added by this prompt.







### Completion note







- Date: 2026-08-11



- Agent: Codex



- Changed files:



  - `docs/architecture/DECISION_GRAPH_CONTRACT.md`



  - `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md`



  - `docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md`



- Checks:



  - `git diff --check -- .ai/task-locks/DEX01-codex.lock.md docs/architecture/DECISION_GRAPH_CONTRACT.md docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md` - pass



  - `node scripts/check-prompt-queues.mjs` - pass



  - `node scripts/check-planning-architecture.mjs` - pass



- Remaining risk:



  - This is a contract/documentation pass only; no runtime decision graph generation exists yet.



- Next:



  - `DEX02 - Prepare first-family explainability rollout plan`







### Dependencies







- current analytics reliability semantics remain authoritative;



- no dependency on GAI;



- later shared-SaaS evidence persistence must satisfy MT/SEC.







---







## RL04 - Implement recommendation lifecycle capture and outcome eligibility runtime slice







Status: DONE



Priority: future / runtime slice



Feature family: recommendation-lifecycle-eligibility



Parallel-safe: yes, when no overlapping action/outcome RQ runtime task owns the same paths



Owner: Cursor



Local lock: removed after DONE



Promotion note: 2026-08-11 â€” owner-promoted runtime slice after RL01/RL02; does not authorize confidence mutation or calibration.







### Problem







RL01/RL02 froze lifecycle vocabulary and statistics rollout, but runtime surfaces still lacked an explicit, testable capture of issued/accepted/rejected/ignored/executed vs measured-learning eligibility. Acceptance could be confused with success if later statistics read raw outcome fields without eligibility gates.







### Evidence







- `docs/Analytics/RECOMMENDATION_OUTCOME_LEARNING_CONTRACT.md`



- `docs/architecture/RECOMMENDATION_LEARNING_STATISTICS_ROLLOUT_PLAN.md`



- Existing action/outcome fields and Product Decision Center recommendation rows







### Scope







- deterministic lifecycle + outcome-evidence projection helper;



- attach projection to analytics action responses and Product Decision Center issued rows;



- enforce learning eligibility so only executed + measured evidence counts toward later learning;



- focused backend/UI contract updates; no confidence mutation; no schema migration; no opaque scoring.







### Read first







- RL01 contract



- RL02 rollout plan



- Decision Intelligence roadmap



- action/outcome endpoints and Product Decision Center contracts







### Do







1. Implement lifecycle capture (`issued`/`accepted`/`rejected`/`ignored`/`executed`) separate from outcome evidence.



2. Define learning eligibility requiring execution + measured timestamp + evidence source.



3. Expose projection on action and Product Decision Center responses.



4. Keep acceptance from inflating success/learning counts.



5. Add focused tests proving absent evidence stays absent.







### Tests







- acceptance alone is not learning-eligible;



- executed-not-measured is not learning-eligible;



- measured with evidence after execution is learning-eligible;



- Product Decision Center issued rows start as `issued` / not eligible;



- decision recommendation engine status remains orthogonal.







### Acceptance







- recommendation lifecycle states are explicit and testable;



- measured-learning eligibility is deterministic;



- acceptance alone cannot inflate success;



- later statistics/calibration work has a trustworthy runtime source.







### Completion note







- Date: 2026-08-11



- Agent: Cursor



- Changed files:



  - `Application/Analytics/RecommendationLifecycleSemantics.cs`



  - `Domain/Model/Analytics/AnalyticsActionItem.cs`



  - `Domain/Model/Analytics/AnalyticsActionLedgerSnapshot.cs`



  - `Api/Endpoints/AnalyticsActionsEndpoints.cs`



  - `Api/Endpoints/CachedAnalyticsEndpoints.cs`



  - `Api.Tests/RecommendationLifecycleSemanticsTests.cs`



  - `Api.Tests/AnalyticsDecisionRecommendationEngineTests.cs`



  - `Api.Tests/AnalyticsActionsEndpointsTests.cs`



  - `Api.Tests/AnalyticsActionsCriticalWorkflowTests.cs`



  - `Api.Tests/ProductDecisionCenterBuilderIntegrationTests.cs`



  - `Klijent/clientapp/src/types/analytics.ts`



  - `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx`



  - `docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md`



  - `MASTER_ROADMAP.md`



- Checks:



  - focused `dotnet test` RL04/actions/PDC filters - pass



- Risks:



  - existing outcome-summary denominators remain RQ-owned and are intentionally unchanged by this eligibility projection



  - ignored detection depends on `DueAtUtc` for `new` actions



- Next:



  - later RL statistics projection prompt when owner promotes measurement-only cohort counts







### Dependencies







- RL01 DONE.



- RL02 DONE.







---







## RL01 - Define recommendation outcome-learning contract







Status: DONE



Priority: future / planning



Feature family: recommendation-learning-contract



Parallel-safe: yes, docs/contracts only



Owner: Codex



Local lock: removed after DONE







### Problem







Recommendation outcome learning needed a deterministic lifecycle contract before runtime calibration or statistics could be made trustworthy.







### Evidence







- `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md` keeps RL after the deterministic explainability family.



- `docs/Analytics/RECOMMENDATION_OUTCOME_LEARNING_CONTRACT.md` is the authoritative lifecycle contract output from this prompt.



- `docs/architecture/RECOMMENDATION_LEARNING_STATISTICS_ROLLOUT_PLAN.md` depends on the contract before any runtime learning slice.







### Scope







- contract-only updates under `docs/Analytics/`



- rollout plan alignment in `docs/architecture/`



- no runtime calibration or code changes







### Read first







- `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md`



- `docs/Analytics/RECOMMENDATION_OUTCOME_LEARNING_CONTRACT.md`



- `docs/architecture/RECOMMENDATION_LEARNING_STATISTICS_ROLLOUT_PLAN.md`







### Do







1. Define the deterministic recommendation lifecycle vocabulary.



2. Clarify what counts as measured learning evidence vs unsupported inference.



3. Keep acceptance, execution and success denominators separate.



4. Align the rollout plan to the new contract.







### Tests







- the contract stays deterministic and AI-independent;



- lifecycle terms remain explicit and non-overlapping;



- no runtime calibration is introduced by the contract pass.







### Acceptance







- the repository has a stable recommendation outcome-learning contract;



- later runtime slices can rely on explicit lifecycle vocabulary;



- acceptance is not conflated with measured success.







### Dependencies







- existing deterministic recommendation semantics remain authoritative;



- no dependency on GAI;



- later statistics work must preserve the contract vocabulary.







### Completion note







- Date: 2026-08-11



- Agent: Codex



- Commit SHA: 3c7104f



- Changed files:



- `docs/Analytics/RECOMMENDATION_OUTCOME_LEARNING_CONTRACT.md`



- `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md`







## RL03 - Implement recommendation lifecycle capture and outcome eligibility contract follow-up







Status: OBSOLETE



Priority: future / implementation



Feature family: recommendation-learning-lifecycle-capture



Parallel-safe: no, coupled backend/runtime contract



Owner: Codex



Local lock: none



Promotion note: Superseded by owner-promoted `RL04` runtime slice (same acceptance; executed 2026-08-11).







### Problem







The outcome-learning contract and rollout plan exist, but Trendplus still needs the first runtime slice that records recommendation issued/accepted/rejected/ignored states and the outcome-eligibility boundary that decides what can count as measured learning evidence.







### Evidence







- `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md` keeps RL in the deterministic learning lane after DEX/DT.



- `docs/Analytics/RECOMMENDATION_OUTCOME_LEARNING_CONTRACT.md` defines the lifecycle vocabulary that should become executable.



- `docs/architecture/RECOMMENDATION_LEARNING_STATISTICS_ROLLOUT_PLAN.md` requires a measured lifecycle before confidence calibration.



- `Api/Endpoints/CachedAnalyticsEndpoints.cs` and the existing action/outcome surfaces already expose the data that can anchor the first capture slice.







### Scope







- `Api/Endpoints/CachedAnalyticsEndpoints.cs`



- `Api.Tests/AnalyticsDecisionRecommendationEngineTests.cs`



- `Api.Tests/AnalyticsActionsEndpointsTests.cs`



- `Api.Tests/AnalyticsActionsCriticalWorkflowTests.cs`



- `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx`



- `Klijent/clientapp/src/types/analytics.ts`







### Read first







- RL01 output



- RL02 output



- `docs/Analytics/RECOMMENDATION_OUTCOME_LEARNING_CONTRACT.md`



- `docs/architecture/RECOMMENDATION_LEARNING_STATISTICS_ROLLOUT_PLAN.md`







### Do







1. Record recommendation lifecycle states explicitly instead of inferring them from later outcome data.



2. Keep accepted/rejected/ignored semantics deterministic and separate from success/failure outcomes.



3. Define which rows are eligible for measured learning and which remain excluded.



4. Preserve the existing deterministic recommendation semantics and no-fake-success rule.







### Tests







- lifecycle transitions are recorded deterministically;



- outcome-eligibility boundaries are explicit and testable;



- acceptance does not count as success unless outcome evidence exists.







### Acceptance







- the product can distinguish issued, accepted, executed, measured and ignored recommendations;



- learning evidence cannot be faked by acceptance alone;



- later statistics work has a trustworthy lifecycle source.







### Dependencies







- RL01 DONE.



- RL02 DONE.



- Provere:



  - `git diff --check`



- Rizici:



  - contract is docs-only; runtime calibration remains intentionally deferred



- Sledece:



  - `DT01 - Define Decision Timeline event model and success metrics`







### Dependencies







- existing action/outcome correctness semantics;



- DEX optional but compatible;



- OBS later owns operational/business metrics exposure.







---







## RL02 - Prepare deterministic statistics rollout plan







Status: DONE



Priority: future



Feature family: recommendation-learning-statistics-plan



Parallel-safe: yes, planning only



Owner: unassigned



Local lock: removed after DONE



Commit suggestion: `docs(learning): add deterministic statistics rollout plan`



Promotion note: 2026-08-11 â€” `RL01` DONE; planning-only; does not authorize runtime calibration.







### Problem







The learning contract needs a staged rollout that first measures truth before changing decisions.







### Evidence







- RL01 will define lifecycle/denominators.







### Scope







- plan phases for event completeness, outcome statistics, dashboards and only later calibration;



- no runtime implementation prompt beyond proposed slices.







### Read first







- RL01 output



- DT contract if available



- OBS roadmap







### Do







1. Define phase 1 measurement-only statistics.



2. Define minimum sample/evidence gates.



3. Define phase 2 confidence-calibration experiment requirements.



4. Define rollback/audit requirements before any score influence.







### Tests







- plan has a measurement-only stage;



- confidence cannot change before explicit evidence gate;



- statistics preserve tenant/dataScope boundaries.







### Acceptance







- runtime learning remains gated behind measurable staged evidence.







### Completion note







- Date: 2026-08-11



- Agent: Codex



- Commit SHA: e57ac8d



- Changed files:



  - `docs/architecture/RECOMMENDATION_LEARNING_STATISTICS_ROLLOUT_PLAN.md`



  - `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md`



  - `docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md`



  - `MASTER_ROADMAP.md`



- Provere:



  - `git diff --check`



  - `node scripts/check-prompt-queues.mjs`



  - `node scripts/check-planning-architecture.mjs`



- Rizici:



  - planning-only rollout; no runtime learning mutation or schema migration was added



- Sledece:



  - `RL04` runtime lifecycle capture (owner-promoted)







### Dependencies







- RL01 DONE.







---







## DT05 - Implement filtered Decision Timeline Slice-2 runtime projection







Status: DONE



Priority: future / implementation



Feature family: decision-timeline-slice2-filtered-projection



Parallel-safe: no, coupled backend/frontend contract



Owner: Cursor



Local lock: removed after DONE



Promotion note: 2026-08-11 â€” owner-promoted Slice-2 filtered runtime after DT03.







### Problem







The Slice-2 filtered timeline still needs the first executable runtime slice that can narrow by entity, family and time period while staying read-only and evidence-led.







### Evidence







- `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md` places filtered timeline slices after the Slice-1 projection.



- `docs/architecture/DECISION_TIMELINE_CONTRACT.md` already defines the canonical event vocabulary.



- `docs/architecture/DECISION_TIMELINE_ROLLOUT_PLAN.md` describes later UI/API slices beyond the read-only projection.



- `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx` and the timeline consumers still need a first-class filter surface.







### Scope







- `Api/Endpoints/CachedAnalyticsEndpoints.cs`



- `Api.Tests/ProductDecisionCenterBuilderIntegrationTests.cs`



- `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx`



- `Klijent/clientapp/src/pages/__tests__/ProductDecisionCenterPage.confidence.spec.tsx`



- `Klijent/clientapp/src/types/analytics.ts`







### Read first







- DT03 output



- `docs/architecture/DECISION_TIMELINE_CONTRACT.md`



- `docs/architecture/DECISION_TIMELINE_ROLLOUT_PLAN.md`



- `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md`







### Do







1. Add explicit filters for entity, family and time period.



2. Keep the projection read-only and evidence-led.



3. Preserve explicit empty/absent states instead of manufacturing results.



4. Maintain compatibility with the existing decision evidence and why-panel payloads.







### Tests







- filter combinations return deterministic timeline slices;



- empty timeline results remain explicit;



- no inferred history or hidden fallback is introduced.







### Acceptance







- users can narrow the timeline without losing deterministic evidence;



- the projection still behaves read-only;



- the UI can explain the chosen time window and entity scope.







### Completion note







- Date: 2026-08-11



- Agent: Cursor



- Changed files:



  - `Infrastructure/Services/Analytics/AnalyticsActionTimelineFilterProjection.cs`



  - `Api/Endpoints/CachedAnalyticsEndpoints.cs`



  - `Api.Tests/AnalyticsActionTimelineFilterProjectionTests.cs`



  - `Api.Tests/ProductDecisionCenterBuilderIntegrationTests.cs`



  - `Klijent/clientapp/src/services/analyticsApi.ts`



  - `Klijent/clientapp/src/types/analytics.ts`



  - `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx`



  - `Klijent/clientapp/src/pages/__tests__/ProductDecisionCenterPage.confidence.spec.tsx`



  - `docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md`



  - `MASTER_ROADMAP.md`



- Checks:



  - focused backend filter/PDC tests - pass



  - `npm run test -- --run src/pages/__tests__/ProductDecisionCenterPage.confidence.spec.tsx` - pass



- Risks:



  - candidate SQL window is wider than requested period; exact period matching remains in the filter helper



  - family matching uses creation snapshot / recommendationStatus only



- Next:



  - later export/report timeline slice when owner promotes







### Dependencies







- DT03 DONE.







---







## DT01 - Define Decision Timeline event model and success metrics







Status: DONE



Priority: future / planning



Feature family: decision-timeline-contract



Parallel-safe: yes, docs/contracts only



Owner: Codex



Local lock: removed after DONE







### Problem







Recommendation, action, execution and outcome information exists in different records/surfaces. Without a canonical timeline event model, Trendplus cannot provide an auditable historical story or trustworthy decision success metrics.







### Evidence







- existing action/outcome workflows already contain lifecycle timestamps/statuses;



- Decision Intelligence roadmap requires `Recommendation -> Action -> Execution -> Outcome -> Historical timeline -> Success metrics`;



- ADR-007 makes historical decision evidence deterministic and AI-independent.







### Scope







- event types, correlation identifiers, timestamps, evidence links and lifecycle metrics;



- current field inventory/gaps;



- append/history semantics at documentation level;



- no runtime event store or schema migration.







### Read first







- `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md`



- `docs/architecture/ADRS.md`



- action/outcome DTOs/audits



- relevant report/export contracts







### Do







1. Define canonical event types for recommendation issued, action accepted/rejected, execution, measurement started/completed and outcome.



2. Define correlation identifiers and entity/recommendation-family dimensions.



3. Define what historical fields must be snapshot vs live lookup.



4. Define success metrics and exact denominators.



5. Define missing/partial event behavior and timeline gaps.



6. Define later export/report/drill-down requirements.







### Tests







- examples cover full lifecycle, rejected recommendation, executed-but-not-measured, delayed outcome and missing evidence;



- success rate never uses issued count when measured count is the intended denominator;



- timeline order/timestamps are unambiguous;



- historical evidence cannot silently become current rewritten evidence.







### Acceptance







- one canonical event/timeline contract exists;



- success metric vocabulary is explicit;



- no runtime event store/migration was introduced.







### Completion note







- Date: 2026-08-11



- Agent: Codex



- Commit SHA: 1b154e7



- Changed files:



  - `docs/architecture/DECISION_TIMELINE_CONTRACT.md`



  - `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md`



- Provere:



  - `git diff --check`



- Rizici:



  - contract is docs-only; no runtime event store or schema migration was added



- Sledece:



  - `DT02 - Prepare historical timeline rollout plan`







### Dependencies







- existing action/outcome contract;



- later persisted evidence must satisfy MT/SEC in shared SaaS.







---







## DT02 - Prepare historical timeline rollout plan







Status: DONE



Priority: future



Feature family: decision-timeline-rollout-plan



Parallel-safe: yes, planning only



Owner: Codex



Local lock: removed after DONE







### Problem







DT01 needs a bounded persistence/API/UI rollout plan before implementation begins.







### Evidence







- DT01 DONE: `docs/architecture/DECISION_TIMELINE_CONTRACT.md`







### Scope







- docs/planning only;



- proposed slices for storage/API/timeline UI/export/hardening;



- no runtime changes.







### Read first







- DT01 output



- MT roadmap for tenant-owned history



- OBS roadmap for correlation/tracing







### Do







1. Identify current storage reuse vs new persistence needs.



2. Define compatibility/migration approach.



3. Define timeline API and UI slices.



4. Define evidence-retention/export/hardening slices.







### Tests







- plan keeps tenant/correlation/authorization boundaries explicit;



- implementation slices are reviewable and do not mix unrelated analytics fixes.







### Acceptance







- future implementation can be queued safely without architecture guessing.







### Completion note







- Date: 2026-08-11



- Agent: Codex



- Changed files:



  - `docs/architecture/DECISION_TIMELINE_ROLLOUT_PLAN.md`



  - `docs/architecture/DECISION_TIMELINE_CONTRACT.md` (examples 4â€“5 + export/drill-down gap-fill)



  - `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md`



- Provere:



  - `git diff --check`



- Rizici:



  - rollout is docs-only; first runtime slice still needs a queued implementation prompt



- Sledece:



  - Current DT READY: `DT03` (Slice-1 projection)







### Dependencies







- DT01 DONE.







---







## DT04 - Implement filtered Decision Timeline Slice-2 projection follow-up







Status: OBSOLETE



Priority: future / implementation



Feature family: decision-timeline-slice2-filtered-projection



Parallel-safe: no, coupled backend/frontend contract



Owner: Codex



Local lock: none



Promotion note: Superseded by owner-promoted `DT05` filtered runtime slice (same acceptance; executed 2026-08-11).







### Problem







Slice-1 proves the read-only projection, but reviewers still need a filtered historical timeline that can narrow by entity, recommendation family and time period without inventing new history semantics or mutating the read-only contract.







### Evidence







- `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md` places filtered timeline slices after the Slice-1 projection.



- `docs/architecture/DECISION_TIMELINE_CONTRACT.md` already defines the canonical event vocabulary.



- `docs/architecture/DECISION_TIMELINE_ROLLOUT_PLAN.md` describes later UI/API slices beyond the read-only projection.



- `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx` and the timeline consumers still need a first-class filter surface.







### Scope







- `Api/Endpoints/CachedAnalyticsEndpoints.cs`



- `Api.Tests/ProductDecisionCenterBuilderIntegrationTests.cs`



- `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx`



- `Klijent/clientapp/src/pages/__tests__/ProductDecisionCenterPage.confidence.spec.tsx`



- `Klijent/clientapp/src/types/analytics.ts`







### Read first







- DT03 output



- `docs/architecture/DECISION_TIMELINE_CONTRACT.md`



- `docs/architecture/DECISION_TIMELINE_ROLLOUT_PLAN.md`



- `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md`







### Do







1. Add explicit filters for entity, family and time period.



2. Keep the projection read-only and evidence-led.



3. Preserve explicit empty/absent states instead of manufacturing results.



4. Maintain compatibility with the existing decision evidence and why-panel payloads.







### Tests







- filter combinations return deterministic timeline slices;



- empty timeline results remain explicit;



- no inferred history or hidden fallback is introduced.







### Acceptance







- users can narrow the timeline without losing deterministic evidence;



- the projection still behaves read-only;



- the UI can explain the chosen time window and entity scope.







### Dependencies







- DT03 DONE.







## DT03 - Implement Decision Timeline Slice-1 read-only projection







Status: DONE



Priority: future



Feature family: decision-timeline-slice1-projection



Parallel-safe: yes, when no overlapping action/outcome RQ runtime task owns the same paths



Owner: Codex



Local lock: `.ai/task-locks/DT03-codex.lock.md`



Promotion note: 2026-08-11 â€” `DT01`/`DT02` DONE; owner-promoted Slice-1 from `DECISION_TIMELINE_ROLLOUT_PLAN.md`







### Problem







DT02 planned historical timeline slices, but no queued implementation exists for the first read-only projection from existing action/note/snapshot data.







### Evidence







- `docs/architecture/DECISION_TIMELINE_CONTRACT.md`



- `docs/architecture/DECISION_TIMELINE_ROLLOUT_PLAN.md` Slice 1







### Scope







- derive a read-only timeline projection from existing `AnalyticsActionItem` + notes + metadata snapshots;



- preserve correlation identifiers and stage gaps (`no_acceptance_record`, `no_execution_proof`, `no_measurement_evidence`, `legacy_partial_history`);



- keep `done` / `rejected` / `pending` / `not_measured` distinct;



- focused tests for projection/gap behavior;



- no new persistence store; no Slice-2 API/UI productization in this prompt unless required to prove the projection helper.







### Read first







- DT01/DT02 outputs



- action/outcome DTOs and existing notes history



- `docs/ai/ANALYTICS_STANDARDS.md` trust rules







### Do







1. Implement the smallest reviewable projection helper/service from current action data.



2. Emit explicit gap reasons instead of inventing missing events.



3. Add focused unit/integration tests for stage order and gap codes.



4. Stop if stage order cannot be preserved without guessing.







### Tests







- projection never invents acceptance/execution/measurement events;



- fake-zero / fake-measured rules unchanged;



- tenant/correlation IDs are not treated as authorization.







### Acceptance







- Slice-1 projection is testable and citable by later Slice-2 API work without architecture guessing.







### Dependencies







- DT02 DONE.







### Completion note







- Date: 2026-08-11



- Agent: Codex



- Changed files:



  - `Infrastructure/Services/Analytics/AnalyticsActionTimelineProjection.cs`



  - `Api.Tests/AnalyticsActionItemServiceTests.cs`



  - `docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md`



- Contract/runtime behavior changed:



  - Added a read-only timeline projection over `AnalyticsActionItem` + notes + ledger snapshot data.



  - Preserved explicit gap reasons for missing acceptance, execution and measurement evidence.



  - Kept `done`, `rejected`, `pending` and `not_measured` distinct in the projection state.



- Checks run:



  - `dotnet test .\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~ProjectTimeline_"` - pass



- Checks not run:



  - `dotnet build`



  - `npm run check:analytics-guardrails`



  - `npm run build`



  - `node scripts/check-prompt-queues.mjs`



  - `node scripts/check-planning-architecture.mjs`



- Remaining risk:



  - Slice-2 API/UI surfaces are still not implemented; this helper is intentionally read-only and internal to the backend projection layer.



- Next:



  - Promote the first Slice-2 timeline API prompt only after the owner queue schedules it.



- Prompt defect / scope repair:



  - Existing metadata parser required a full creation snapshot, so the projection now uses a raw metadata fallback for partial sourceRecommendationId derivation.



  - Outcome audit notes are ignored for workflow event duplication.







---
