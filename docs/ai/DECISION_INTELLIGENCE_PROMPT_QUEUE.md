# Trendplus Decision Intelligence Planning Queue

Created: 2026-08-08
Roadmap: `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md`
Purpose: planning/contracts only until later roadmap gates explicitly authorize runtime work.

## Current READY by program

| Program | Current READY | Execution class |
|---|---|---|
| DEX - Decision Explainability | `DEX08` | docs/contracts/tests-plan only |
| RL - Recommendation Learning | `RL01` | docs/contracts/data-model inventory only |
| DT - Decision Timeline | `DT01` | docs/contracts/event-model only |

Only one prompt per program may be READY. A READY prompt in this file does not outrank the existing BCI/STAB/RQ/QDB/MT/GAI execution priority from `MASTER_ROADMAP.md` and does not authorize broad runtime implementation.

---

## DEX08 - Implement Product Decision Center deterministic Why-panel contract

Status: DONE
Priority: future-high-value / implementation
Feature family: decision-explainability-product-decision-center-phase5
Parallel-safe: no, coupled backend/frontend contract
Owner: Codex
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
  - `RL01 - Recommendation Learning`

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

## RL01 - Define recommendation outcome-learning contract

Status: READY  
Priority: future / planning  
Feature family: recommendation-learning-contract  
Parallel-safe: yes, docs/contracts only  
Owner: unassigned

### Problem

Trendplus can record recommendations/actions/outcomes, but a learning program would be unsafe if acceptance, execution and measured success were treated as the same thing or if historical statistics changed confidence without an explicit evidence contract.

### Evidence

- Existing analytics action/outcome reliability work distinguishes lifecycle/measurement states and denominators.
- ADR-004 requires deterministic, evidence-backed recommendation principles.
- The product vision requires `issued -> accepted -> executed -> measured -> outcome -> learning` rather than click/acceptance learning.

### Scope

- define lifecycle vocabulary, outcome eligibility and statistical dimensions;
- map current action/outcome fields and gaps;
- define confidence-calibration input/output contract at documentation level;
- no runtime learning algorithm, ML model or automatic score change.

### Read first

- `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md`
- `docs/architecture/ADRS.md`
- action/outcome reliability queue/audits
- current action/outcome DTOs and tests

### Do

1. Define issued/accepted/rejected/ignored/executed/measured/not-measured outcome states.
2. Define eligibility rules for measured success/failure/insufficient evidence.
3. Define denominator vocabulary for acceptance, execution, measurement and success rates.
4. Define attribution/window metadata needed before outcome statistics are trustworthy.
5. Define segmentation rules and minimum-evidence principles for statistics.
6. Define a future deterministic calibration interface while explicitly leaving runtime behavior unchanged.

### Tests

- examples distinguish accepted from executed and executed from measured;
- no-measurement cannot count as success/failure;
- zero-denominator behavior is explicit;
- low-sample statistics remain low-confidence/insufficient;
- no acceptance-only "learning" path exists.

### Acceptance

- one authoritative learning lifecycle/statistics contract exists;
- later implementations can compute statistics without changing recommendation confidence yet;
- no ML/AI/runtime learning was added.

### Dependencies

- existing action/outcome correctness semantics;
- DEX optional but compatible;
- OBS later owns operational/business metrics exposure.

---

## RL02 - Prepare deterministic statistics rollout plan

Status: WAITING  
Priority: future  
Feature family: recommendation-learning-statistics-plan  
Parallel-safe: yes, planning only  
Owner: unassigned

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

### Dependencies

- RL01 DONE.

---

## DT01 - Define Decision Timeline event model and success metrics

Status: READY  
Priority: future / planning  
Feature family: decision-timeline-contract  
Parallel-safe: yes, docs/contracts only  
Owner: unassigned

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

### Dependencies

- existing action/outcome contract;
- later persisted evidence must satisfy MT/SEC in shared SaaS.

---

## DT02 - Prepare historical timeline rollout plan

Status: WAITING  
Priority: future  
Feature family: decision-timeline-rollout-plan  
Parallel-safe: yes, planning only  
Owner: unassigned

### Problem

DT01 needs a bounded persistence/API/UI rollout plan before implementation begins.

### Evidence

- DT01 will define the event model.

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

### Dependencies

- DT01 DONE.
