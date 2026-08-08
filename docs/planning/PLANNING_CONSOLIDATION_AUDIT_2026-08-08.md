# Trendplus Planning Consolidation Audit — 2026-08-08

Repository: `ivanjovicic/Trendplus`  
Scope: documentation, roadmap, queue routing and governance only  
Result: **PASS for consolidated planning architecture, with historical-pointer cleanup explicitly classified as non-authoritative evidence rather than deleted history**

## Purpose

This audit records the repository evidence used to consolidate Trendplus planning without rediscovering already accepted product conclusions, replacing existing programs, or creating competing implementation queues.

The canonical entry point after this consolidation is `MASTER_ROADMAP.md`.

## Accepted findings verified against current main

The following accepted planning architecture remains valid and was preserved:

- Analytics Reliability (`RQ`)
- Backend CI Repair (`BCI`)
- Stabilization / Release / current pilot Security (`STAB`)
- Data Connector (`QDB`)
- Multi-Tenant (`MT`)
- GenAI (`GAI`)

Existing planning already covers Decision Board, Product Decision Center, recommendation behavior, data quality, freshness, confidence, reason codes, inventory/supplier analytics, executive surfaces, workers and reporting. This consolidation did not recreate those implementation plans.

## Current repository evidence that changed the originally supplied task pointers

The user-supplied program priority was preserved, but the named historical task IDs were not resurrected as READY because current repository evidence is newer:

| Program | Current evidence on 2026-08-08 | Consolidation treatment |
|---|---|---|
| BCI | queue header says current READY `none`; `BCI01` remains `PARTIAL` because bootstrap is fixed but the full backend test gate is not green | preserve BCI as highest existing program priority; do not falsely mark BCI01 READY |
| STAB | queue header says current READY `none`; STAB08 is complete, but current release evidence still says core pilot is not ready and GenAI remains blocked | preserve STAB priority and current blocker truth |
| RQ | main reliability queue says current READY `none (queue complete)`; priority router says no global analytics READY | preserve RQ priority; remaining addenda stay owner-gated/WAITING until explicitly promoted |
| QDB | `QDB01` is current READY | preserve as first connector task, still below unresolved higher-priority work |
| MT | `MT01` is current READY | preserve; dedicated one-customer deployment remains safe fallback until MT shared-SaaS gate |
| GAI | current queue is dormant/blocked by current release evidence | do not promote AI work |

This distinction is the main reason `MASTER_ROADMAP.md` routes by **program + current queue status**, not by copying historical task IDs from an earlier review.

## New canonical planning documents

### Product/process

- `MASTER_ROADMAP.md`
- `docs/product/PRODUCT_VISION.md`
- `docs/planning/FEATURE_LIFECYCLE.md`
- `docs/roadmaps/BUSINESS_ROADMAP.md`

### Decision Intelligence

- `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md`
- `docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md`

Decision Intelligence is explicitly separate from analytics and deterministic before AI. Its roadmap owns Decision Graph, evidence chain, confidence breakdown, alternatives, drill-down/Why/Decision Tree, recommendation outcome learning and historical Decision Timeline.

### Platform evolution

- `docs/roadmaps/PERFORMANCE_ROADMAP.md`
- `docs/roadmaps/OBSERVABILITY_ROADMAP.md`
- `docs/roadmaps/SECURITY_EVOLUTION_ROADMAP.md`
- `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md`

The SEC roadmap was added because a new SEC queue without a roadmap would violate the requested roadmap/queue symmetry. Its ownership is deliberately post-STAB/cross-cutting and does not replace STAB, MT, QDB or GAI security work.

### Architecture governance

- `docs/architecture/ADRS.md`

Initial accepted ADRs record:

1. Backend is source of truth.
2. One customer equals one deployment until shared-SaaS isolation is proven.
3. Data connectors are read-only sources feeding the existing PostgreSQL Trendplus core.
4. Recommendations are deterministic, evidence-backed and auditable.
5. No fake zero.
6. No fake green.
7. Decision Intelligence is deterministic before AI.

## New queue invariants

The new planning families are deliberately small and planning-only:

| Program | READY | Later prompt |
|---|---|---|
| DEX | `DEX01` | `DEX02` WAITING |
| RL | `RL01` | `RL02` WAITING |
| DT | `DT01` | `DT02` WAITING |
| PERF | `PERF01` | `PERF02` WAITING |
| OBS | `OBS01` | `OBS02` WAITING |
| SEC | `SEC01` | `SEC02` WAITING |

Every new prompt contains the required sections:

- Problem
- Evidence
- Scope
- Read first
- Do
- Tests
- Acceptance
- Dependencies

The first READY tasks are contract/baseline/audit tasks only. No production runtime implementation was introduced by this consolidation.

## Routing changes

`docs/ai/AGENT_START_HERE.md` now routes agents through `MASTER_ROADMAP.md` before an owner queue.

`docs/ai/PROMPT_QUEUE_PROTOCOL.md` now makes these rules explicit:

- master roadmap owns cross-program routing;
- at most one READY prompt per program;
- future planning READY does not outrank the existing execution priority;
- later prompts remain WAITING;
- new prompts require the eight standard sections;
- historical/current conflicts are resolved through the current canonical layer rather than deletion of evidence.

## Historical/current cleanup

The repository contains historical and addendum documents that still include snapshot prose such as:

- `Main queue READY prompt: RQ01`
- old completion notes saying `Next: ... READY`
- dated readiness/status statements that were correct when written but are no longer current routing truth.

These are not deleted because they are evidence of prior planning and execution. Instead:

- `MASTER_ROADMAP.md` is authoritative for cross-program current routing;
- current owner-queue headers are authoritative for task status;
- `AGENT_START_HERE.md` explicitly classifies conflicting old addendum/audit pointers as historical;
- `NEXT_PROMPT_QUEUE.md` remains a historical ledger.

This removes the operational ambiguity without erasing history.

A later mechanical archival/annotation pass may move clearly historical snapshots under an archive folder or add explicit `Historical snapshot` banners, but it must not change task history or be used to invent new READY work.

## Validation

### Existing queue validator

The existing `scripts/check-prompt-queues.mjs` remains the validator for the legacy/current execution queues it already inventories. The STAB02 completion evidence in the repository records both its self-test and queue scan as passing at that reconciliation point.

This consolidation did not edit the legacy queue files parsed by that script; it updated routing/protocol documents and added separate new queue files. Therefore a separate planning validator was added rather than silently extending legacy regex/queue assumptions through a large unrelated rewrite.

### New planning validator

Added `scripts/check-planning-architecture.mjs`.

It validates:

- required canonical planning paths exist;
- each program has a declared roadmap and owner queue;
- DEX/RL/DT/PERF/OBS/SEC tasks have valid statuses and an Owner;
- every new prompt has all eight required sections;
- exactly one READY prompt exists per new program;
- later prompts are not READY;
- `MASTER_ROADMAP.md` contains every program route and queue link;
- `AGENT_START_HERE.md` routes through the master and both new queue owners.

Validator self-test result during this consolidation: **PASS**. The self-test also proves that a duplicate READY in a program is rejected.

### Current-main static consistency inspection

Current-main files were fetched after their commits and inspected for the same invariants:

- `MASTER_ROADMAP.md` contains BCI/STAB/RQ/QDB/MT/GAI/DEX/RL/DT/PERF/OBS/SEC routing rows;
- Decision Intelligence queue has DEX01/RL01/DT01 READY and DEX02/RL02/DT02 WAITING;
- Platform Evolution queue has PERF01/OBS01/SEC01 READY and PERF02/OBS02/SEC02 WAITING;
- existing QDB, MT and GAI roadmap/queue paths exist;
- current QDB and MT READY pointers remain QDB01 and MT01;
- GAI remains blocked rather than being activated by this documentation work.

No orphan was found among the programs listed in the master roadmap.

## Implementation impact

Runtime/application behavior changed: **no**.

No production code, database schema, analytics formula, recommendation implementation, connector runtime, tenant runtime, AI runtime or deployment configuration was changed by this consolidation.

## Files intentionally not deleted

- historical queue ledgers;
- dated QA/release evidence;
- analytics addenda containing old snapshot pointers;
- completed queue entries and completion notes.

They remain evidence. Current routing now has a higher-level canonical source so they cannot safely be interpreted as current work when they conflict.

## Remaining recommendations

1. **Mechanical historical annotation/archive pass** — add `Historical snapshot` banners or move clearly historical planning snapshots into an archive structure, but only as a no-semantics-change documentation task.
2. **Run both validators in CI for planning-file changes** — once the desired CI trigger/path policy is chosen, add `check-prompt-queues.mjs` and `check-planning-architecture.mjs` to a lightweight documentation/governance gate.
3. **Avoid growing combined new queues indefinitely** — when DEX/RL/DT or PERF/OBS/SEC become implementation-heavy, preserve the same program IDs but split files only if token/ownership pressure justifies it; keep `MASTER_ROADMAP.md` stable as the router.
4. **Refresh master current-ready pointers only from owner queues** — never manually advance a program from roadmap priority alone.

## Future roadmap ideas, not approved implementation work

- Customer onboarding/provisioning lifecycle roadmap after QDB/MT evidence matures.
- Data retention/offboarding roadmap for SaaS customer lifecycle.
- Experimentation/causal measurement roadmap after RL outcome evidence is reliable enough to distinguish correlation from attributable effect.
- Cost/FinOps roadmap once customer count and cloud usage justify explicit per-customer resource economics.
- Support/incident operations roadmap after OBS establishes authoritative SLI/correlation vocabulary.

These are ideas only and should not become READY queues without passing the Feature Lifecycle and master ownership checks.
