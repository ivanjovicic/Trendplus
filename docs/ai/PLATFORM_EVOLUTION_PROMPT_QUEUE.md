# Trendplus Platform Evolution Planning Queue

Created: 2026-08-08  
Roadmaps:
- `docs/roadmaps/PERFORMANCE_ROADMAP.md`
- `docs/roadmaps/OBSERVABILITY_ROADMAP.md`
- `docs/roadmaps/SECURITY_EVOLUTION_ROADMAP.md`

Purpose: planning/contracts and measurement preparation. Runtime work requires later explicit promotion through the owning program.

## Current READY by program

| Program | Current READY | Execution class |
|---|---|---|
| PERF - Performance | `none` (`PERF01` DONE; `PERF02` WAITING on measurements) | baseline/measurement plan |
| OBS - Observability | `OBS03` | first instrumentation slice (API/process evidence) |
| SEC - Security Evolution | `SEC04` | supply-chain assurance policy (docs) |

Only one prompt per program may be READY. These planning tasks never outrank higher-priority runtime gates in `MASTER_ROADMAP.md`.

---

## PERF01 - Establish performance baseline, dataset tiers and budgets

Status: DONE
Priority: future / planning
Feature family: performance-baseline-contract
Parallel-safe: yes, measurement/docs only
Owner: Cursor
Local lock: none

### Problem

Trendplus has known query, worker, cold-start and dataset-scale risks, but optimization without a repeatable baseline can create speculative complexity or accidentally change analytics semantics.

### Evidence

- analytics/import workers and dashboards already include potentially expensive aggregation paths;
- current planning mentions timeouts/cold-start/worker concerns in multiple places;
- no canonical performance roadmap/budget previously owned SQL profiling, indexes, cache, large datasets, memory, worker throughput and cold start together.

### Scope

- benchmark inventory and dataset-tier definitions;
- identify existing benchmark/performance tests/scripts and gaps;
- initial budget proposal as measurable targets, not product SLA promises;
- no production query/index/cache changes.

### Read first

- `MASTER_ROADMAP.md`
- `docs/roadmaps/PERFORMANCE_ROADMAP.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md`
- import/worker architecture docs and current performance-related tests/scripts

### Do

1. Inventory critical route/query/worker/import/frontend flows worth benchmarking.
2. Define small/medium/large representative dataset tiers and how they can be reproduced safely.
3. Define cold vs warm measurement rules and environment metadata.
4. Propose initial p50/p95/throughput/memory budgets as engineering budgets, clearly separate from contractual SLA.
5. Map each baseline to the owning future PERF phase.
6. Identify correctness assertions that must run alongside performance measurements.

### Tests

- every proposed benchmark has reproducible inputs and output correctness checks;
- cold/warm state is explicit;
- dataset tier is explicit;
- no budget is presented as measured fact until measured;
- no runtime optimization is introduced.

### Acceptance

- one reproducible performance-baseline plan exists;
- benchmark priorities cover SQL, cache, large datasets, memory, workers and cold start;
- later optimization prompts can require before/after evidence.

### Completion note

- Date: 2026-08-11
- Agent: Cursor
- Changed files:
  - `docs/architecture/PERFORMANCE_BASELINE_CONTRACT.md`
  - `docs/roadmaps/PERFORMANCE_ROADMAP.md`
  - `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md`
  - `MASTER_ROADMAP.md`
- Checks:
  - `git diff --check` - pass
  - no runtime optimization introduced
- Risks:
  - budgets remain target-only until S-tier measurement pack is recorded
  - no BenchmarkDotNet/k6 harness yet (explicit gap)
- Next:
  - `PERF02` stays WAITING until usable measurements exist
  - Current READY in this queue: `OBS01`

### Dependencies

- RQ/STAB semantics remain authoritative;
- OBS may later provide continuous telemetry but is not required to define the baseline.

---

## PERF02 - Prepare SQL/index/cache optimization backlog from measurements

Status: WAITING  
Priority: future  
Feature family: performance-measured-backlog  
Parallel-safe: yes, planning only  
Owner: unassigned

### Problem

Optimization tasks should be created only after baseline evidence identifies the slowest/highest-value paths.

### Evidence

- PERF01 will produce baseline methodology and measurements/gaps.

### Scope

- measured findings -> prioritized future prompt plan;
- no production optimization in this task.

### Read first

- PERF01 output
- relevant SQL/index/cache code/tests for measured candidates

### Do

1. Rank candidates by user/business impact and measured cost.
2. Separate SQL/index, cache, memory, worker and cold-start families.
3. Define before/after evidence requirements for each.
4. Exclude speculative changes without measured evidence.

### Tests

- every backlog item links a measurement;
- correctness/rollback/invalidation checks are specified;
- no item mixes unrelated performance families.

### Acceptance

- future runtime optimization prompts are evidence-backed and reviewable.

### Dependencies

- PERF01 DONE with usable measurements.

---

## OBS01 - Define business/technical SLI, SLA and correlation catalog

Status: DONE
Priority: future / planning
Feature family: observability-sli-contract
Parallel-safe: yes, docs/contracts only
Owner: Cursor
Local lock: none

### Problem

Health, import status, freshness, worker state and latency exist across several surfaces, but there is no single catalog saying what should be measured, which component owns it, and how import/analytics/worker SLA evidence differs from product/business status.

### Evidence

- current health/readiness and analytics freshness/import provenance are already important release evidence;
- STAB work established that unknown operational evidence must stay visible;
- future business milestones require import, analytics and worker operability.

### Scope

- metric/SLI inventory and owner map;
- SLA/SLO vocabulary and proposed measurement points;
- correlation-ID lifecycle plan;
- tracing/dashboard gap inventory;
- no paid vendor choice and no broad runtime instrumentation.

### Read first

- `MASTER_ROADMAP.md`
- `docs/roadmaps/OBSERVABILITY_ROADMAP.md`
- current health/readiness, import status, analytics refresh and worker docs/code
- STAB release evidence

### Do

1. Define authoritative business and technical SLI families.
2. Define latency, error, freshness and throughput dimensions.
3. Define Import SLA, Analytics SLA and Worker SLA measurement boundaries without inventing contractual target numbers.
4. Define correlation-ID propagation expectations across HTTP, import, workers, refresh, report and future decision timeline.
5. Inventory existing logs/metrics/traces and name gaps.
6. Propose dashboard layers and unknown/non-green behavior.

### Tests

- every metric has source/owner/unit/dimensions;
- unknown telemetry is explicitly non-green;
- no correlation ID is treated as tenant/auth identity;
- sensitive source/customer payloads are excluded;
- SLA vocabulary distinguishes measured SLI from target/contract.

### Acceptance

- one SLI/SLA/correlation catalog exists;
- import, analytics and worker evidence boundaries are explicit;
- later instrumentation can be split by component.

### Completion note

- Date: 2026-08-11
- Agent: Cursor
- Changed files:
  - `docs/architecture/OBSERVABILITY_SLI_CATALOG.md`
  - `docs/roadmaps/OBSERVABILITY_ROADMAP.md`
  - `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md`
  - `MASTER_ROADMAP.md`
- Checks:
  - `git diff --check` - pass
  - no runtime instrumentation / vendor choice
- Risks:
  - queue-depth/DLQ and cache-hit SLIs remain gaps until OBS02 instrumentation
  - HTTP correlation vs RequestLogContext TraceId still dual-named
- Next:
  - `OBS02` stays WAITING until owner promotes instrumentation rollout
  - Current READY in this queue: `SEC01`

### Dependencies

- STAB security/privacy rules;
- MT before tenant labels/dimensions are treated as trusted shared-SaaS scope.

---

## OBS02 - Prepare observability instrumentation rollout plan

Status: DONE
Priority: future
Feature family: observability-rollout-plan
Parallel-safe: yes, planning only
Owner: Cursor
Local lock: none

### Problem

OBS01 must be translated into bounded instrumentation slices without a broad telemetry rewrite.

### Evidence

- OBS01 DONE: `docs/architecture/OBSERVABILITY_SLI_CATALOG.md`

### Scope

- docs/planning only;
- separate API/import/analytics/worker/dashboard/tracing slices;
- no vendor lock-in or runtime changes.

### Read first

- OBS01 output
- PERF roadmap
- existing Serilog/health/metrics code

### Do

1. Rank gaps by pilot/customer support value.
2. Define minimal first instrumentation slice.
3. Separate metrics, tracing, dashboards and alerts.
4. Specify validation and privacy checks.

### Tests

- each slice has source-of-truth and failure behavior;
- no absent metric defaults to healthy;
- no slice leaks secrets/customer payloads.

### Acceptance

- future OBS runtime prompts can be created without duplicating STAB or PERF.

### Completion note

- Date: 2026-08-11
- Agent: Cursor
- Changed files:
  - `docs/architecture/OBSERVABILITY_INSTRUMENTATION_ROLLOUT_PLAN.md`
  - `docs/roadmaps/OBSERVABILITY_ROADMAP.md`
  - `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md`
  - `MASTER_ROADMAP.md`
- Checks:
  - `git diff --check` - pass
  - no runtime instrumentation
- Risks:
  - O2-1 still needs a queued runtime implementation prompt
  - queue-depth SLIs remain unknown until real queues are instrumented
- Next:
  - Current OBS READY: `OBS03` (Slice 1 API/process evidence)

### Dependencies

- OBS01 DONE.

---

## OBS03 - Implement observability Slice-1 API/process evidence

Status: READY
Priority: future
Feature family: observability-api-process-evidence
Parallel-safe: yes, when paths do not collide with BCI/STAB auth or PERF optimization work
Owner: unassigned
Local lock: `.ai/task-locks/OBS03-<agent>.lock.md`
Promotion note: 2026-08-11 — `OBS01`/`OBS02` DONE; first slice from `OBSERVABILITY_INSTRUMENTATION_ROLLOUT_PLAN.md`

### Problem

OBS02 ranked instrumentation slices, but no queued runtime prompt exists for the first API/process evidence improvements support needs in pilot.

### Evidence

- `docs/architecture/OBSERVABILITY_SLI_CATALOG.md`
- `docs/architecture/OBSERVABILITY_INSTRUMENTATION_ROLLOUT_PLAN.md` Slice 1

### Scope

- make request/availability/latency evidence consistent from existing `/ready`, `/health`, `/health/dependencies`, runtime version and request performance logging;
- preserve unknown ≠ green;
- admin/ops visibility only where privileged; do not broaden public `/health` disclosure;
- focused tests for unknown-when-missing and no fake-zero collapse;
- no paid APM vendor selection; no broad rewrite.

### Read first

- OBS01/OBS02 outputs
- PERF01 baseline contract (targets only; do not optimize queries here)
- STAB public health disclosure constraints

### Do

1. Inventory current fields already available from the named sources of truth.
2. Expose or normalize the minimal Slice-1 evidence surface (prefer reuse over new store).
3. Mark missing probes as unknown/non-green.
4. Add focused tests for unknown and error ≠ zero.

### Tests

- missing probe stays unknown, not healthy;
- error responses do not become fake zero metrics;
- no secrets/customer payloads in telemetry;
- STAB authz fail-closed unchanged for admin-only surfaces.

### Acceptance

- Slice-1 evidence is usable by support without inventing green defaults; later OBS slices can cite it.

### Dependencies

- OBS02 DONE.

---

## SEC01 - Reconcile security ownership and current threat model

Status: DONE
Priority: future / planning
Feature family: security-ownership-threat-model
Parallel-safe: yes, docs/audit only
Owner: Cursor
Local lock: none

### Problem

Security planning is currently distributed across STAB pilot security work, MT tenant isolation, connector credential concerns and GAI security boundaries. A new SEC program would be harmful if it simply duplicated those queues.

### Evidence

- STAB already owns current deploy/auth/admin/edge/release security work;
- MT owns shared-SaaS isolation;
- QDB owns source connector behavior and will need credential ownership;
- GAI has separate security/data-boundary gates;
- `docs/roadmaps/SECURITY_EVOLUTION_ROADMAP.md` defines SEC as post-STAB cross-cutting ownership, not a replacement.

### Scope

- current security ownership matrix and threat-model reconciliation;
- map identities, secrets, privileged operations, tenant boundaries, source connectors, storage/docs/exports, workers and AI surfaces to existing owners;
- identify genuine orphan risks only;
- no runtime security changes.

### Read first

- `MASTER_ROADMAP.md`
- `docs/roadmaps/SECURITY_EVOLUTION_ROADMAP.md`
- `docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md`
- `docs/architecture/MULTITENANCY_ARCHITECTURE_ROADMAP.md`
- `docs/security/TENANT_SAFETY_CHECKLIST.md`
- `docs/security/GENAI_SECURITY_AND_DATA_BOUNDARIES.md`
- QDB roadmap/queue security boundaries

### Do

1. Build a surface/threat/owner matrix.
2. Mark each risk as STAB, MT, QDB, GAI, SEC or accepted/non-applicable.
3. Identify duplicated historical security descriptions and point them to the current owner without deleting evidence.
4. Identify true orphan risks that warrant future SEC prompts.
5. Define criteria for STAB-to-SEC handoff after pilot remediation is stable.

### Tests

- every security surface has exactly one primary owner;
- SEC does not create a duplicate prompt for an active STAB/MT/QDB/GAI family;
- missing runtime evidence stays unknown/BLOCKED rather than PASS;
- no secret/customer payload enters the audit.

### Acceptance

- a current security ownership/threat map exists;
- SEC has only genuinely cross-cutting future work;
- no runtime security change is made.

### Completion note

- Date: 2026-08-11
- Agent: Cursor
- Changed files:
  - `docs/architecture/SECURITY_OWNERSHIP_THREAT_MAP.md`
  - `docs/roadmaps/SECURITY_EVOLUTION_ROADMAP.md`
  - `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md`
  - `MASTER_ROADMAP.md`
- Checks:
  - `git diff --check` - pass
  - no runtime security changes
- Risks:
  - STAB03 follow-up gaps (import/logs/document-header) remain STAB-owned, not closed by this map
  - External IdP still deferred (accepted/n/a until product decision)
- Next:
  - `SEC02` WAITING (post-STAB assurance backlog)
  - Platform Evolution Current READY: none

### Dependencies

- current STAB/MT/QDB/GAI documentation available;
- no dependency on selecting an external identity provider.

---

## SEC02 - Prepare post-STAB security assurance backlog

Status: DONE
Priority: future
Feature family: security-assurance-plan
Parallel-safe: yes, planning only
Owner: Cursor
Local lock: none

### Problem

After SEC01, only orphan/post-STAB risks should become SEC backlog items.

### Evidence

- SEC01 DONE: `docs/architecture/SECURITY_OWNERSHIP_THREAT_MAP.md`

### Scope

- docs/planning only;
- candidate secrets/supply-chain/data-protection/security-observability/assurance slices;
- no runtime implementation.

### Read first

- SEC01 output
- `docs/roadmaps/SECURITY_EVOLUTION_ROADMAP.md`

### Do

1. Prioritize only risks truly owned by SEC.
2. Define evidence/tests needed before runtime changes.
3. Keep MT/GAI/STAB/QDB dependencies explicit.
4. Define future SaaS and AI assurance gates without duplicating their owner queues.

### Tests

- no duplicate active feature family;
- each candidate has a named evidence gap;
- release/tenant/AI ownership remains unchanged.

### Acceptance

- post-STAB security evolution has a bounded, non-duplicative backlog plan.

### Completion note

- Date: 2026-08-11
- Agent: Cursor
- Changed files:
  - `docs/architecture/SECURITY_ASSURANCE_BACKLOG_PLAN.md`
  - `docs/roadmaps/SECURITY_EVOLUTION_ROADMAP.md`
  - `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md`
  - `MASTER_ROADMAP.md`
- Checks:
  - `git diff --check` - pass
  - no runtime security changes
- Risks:
  - STAB watchlist (import/logs/docs) still unqueued — must not be re-homed to SEC
  - SEC runtime still gated by STAB residual acceptance + MT/GAI where claimed
- Next:
  - Platform Evolution Current READY: `SEC03` (S2-1 docs)
  - Alternate candidate remains WAITING: `SEC04` (S2-2 supply-chain) after SEC03
  - Higher priority: BCI05 commit/push → GHA if user authorizes

### Dependencies

- SEC01 DONE.

---

## SEC03 - Privileged secrets and emergency-access assurance (S2-1)

Status: DONE
Priority: future
Feature family: security-privileged-secrets-assurance
Parallel-safe: yes, planning/docs only
Owner: Codex
Local lock: removed after DONE
Promotion note: 2026-08-11 - `SEC02` DONE; slice S2-1 from `SECURITY_ASSURANCE_BACKLOG_PLAN.md`

### Problem

Admin API-key / deployment-secret rotation and emergency-access expectations are an SEC orphan. Without a written assurance inventory, pilot ops can leave key sprawl unowned after STAB authz work.

### Evidence

- `docs/architecture/SECURITY_ASSURANCE_BACKLOG_PLAN.md` slice **S2-1**
- `docs/architecture/SECURITY_OWNERSHIP_THREAT_MAP.md` (S2 / rotation orphan)
- STAB Admin API-key boundary remains primary for authz defects

### Scope

- docs/planning only;
- privileged secret *classes* inventory (names/types, no values);
- rotation / revoke / emergency-access runbook expectations;
- fail-closed checklist linking existing STAB auth tests;
- no runtime authz change, no IdP, no secret values in git.

### Read first

- `docs/architecture/SECURITY_ASSURANCE_BACKLOG_PLAN.md`
- `docs/architecture/SECURITY_OWNERSHIP_THREAT_MAP.md`
- `docs/roadmaps/SECURITY_EVOLUTION_ROADMAP.md` (SEC-2)
- existing Admin access / config key docs if cited by those maps

### Do

1. Produce `docs/architecture/PRIVILEGED_SECRETS_ASSURANCE.md` (or equivalent under `docs/security/`).
2. List secret classes: Admin key, DB, storage, cache, connector SecretReference - types only.
3. Define rotation/revoke expectations and emergency-access steps (who, blast radius, post-incident revoke).
4. Explicitly mark STAB as owner of authz defects; QDB as owner of connector credential *features*.
5. Name evidence gaps that still block runtime PASS.

### Tests

- document contains no real secrets or connection strings;
- each class has owner + fail-closed note;
- STAB/QDB/MT/GAI boundaries are cited, not redefined;
- missing evidence remains unknown/BLOCKED, never PASS.

### Acceptance

- S2-1 has a durable assurance doc agents can cite;
- no runtime security code changed;
- `SEC04` (supply-chain) remains the natural next SEC READY after this DONE.

### Completion note

- Date: 2026-08-11
- Agent: Codex
- Changed files:
  - `docs/architecture/PRIVILEGED_SECRETS_ASSURANCE.md`
  - `docs/roadmaps/SECURITY_EVOLUTION_ROADMAP.md`
  - `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md`
  - `MASTER_ROADMAP.md`
- Checks:
  - `git diff --check` - pass
  - no runtime security changes
- Risks:
  - rotation/revoke evidence remains docs-only until a future rehearsal or runtime prompt
  - STAB authz defects remain STAB-owned
- Next:
  - Platform Evolution Current READY: `SEC04`
  - `SEC04` is READY

### Dependencies

- SEC02 DONE.

---

## SEC04 - Dependency and supply-chain assurance policy (S2-2)

Status: READY
Ready after: `SEC03` is `DONE`
Priority: future
Feature family: security-supply-chain-policy
Parallel-safe: yes, planning/docs only
Owner: unassigned
Local lock: `.ai/task-locks/SEC04-<agent>.lock.md`
Promotion note: 2026-08-11 — `SEC03` DONE; docs/policy only; BCI collaborates on CI wiring.

### Problem

Vulnerable/abandoned package posture has no queued owner outside SEC-3.

### Evidence

- SEC02 slice **S2-2**
- SEC01 orphan S14

### Scope

- docs/policy only;
- scan frequency/severity fail rules for .NET and npm;
- triage ownership (SEC + BCI for CI wiring);
- abandoned-package handling;
- no broad dependency upgrades in this prompt.

### Read first

- `docs/architecture/SECURITY_ASSURANCE_BACKLOG_PLAN.md`
- BCI queue ownership for CI wiring

### Do

1. Write supply-chain policy doc with ecosystems, severity gates, and accepted-risk template.
2. Name the reproducible scan command(s) or CI job placeholders (BCI wires later).
3. Keep BCI as collaborator for pipeline; SEC owns policy.

### Tests

- no duplicate of BCI repair scope;
- missing scan output cannot claim PASS;
- no secrets in evidence.

### Acceptance

- S2-2 policy exists and points to BCI for CI integration.

### Dependencies

- SEC03 DONE (keeps one READY-at-a-time in SEC).

---

## SEC05 - Data protection and retention assurance plan (S2-3)

Status: WAITING
Ready after: `SEC04` is `DONE` and MT09 tenant lifecycle contracts exist (or dedicated-deploy offboarding is explicitly accepted as interim scope)
Priority: future
Feature family: security-retention-assurance-plan
Parallel-safe: yes, planning/docs only
Owner: unassigned
Local lock: `.ai/task-locks/SEC05-<agent>.lock.md`

### Problem

Cross-cutting retention/classification/offboarding assurance remains an SEC orphan (S15 / S2-3) beyond MT09 product work.

### Evidence

- `docs/architecture/SECURITY_ASSURANCE_BACKLOG_PLAN.md` slice **S2-3**
- SEC01 orphan S15

### Scope

- docs/planning only;
- data-class inventory and retention/deletion expectations;
- dedicated-deploy wipe/restore checklist until MT09;
- AI provider retention remains blocked until GAI policy;
- no runtime delete tooling in this prompt.

### Read first

- SEC02 backlog plan
- MT09 / tenant safety checklist
- `docs/security/GENAI_SECURITY_AND_DATA_BOUNDARIES.md`

### Do

1. Produce retention/classification assurance doc.
2. Name owners per data class (STAB/MT/QDB/GAI collaborators).
3. Fail closed when delete scope is unknown.
4. Keep MT/GAI gates explicit.

### Tests

- no duplicate of MT09 implementation;
- missing evidence cannot claim PASS;
- no secrets/customer payloads in the doc.

### Acceptance

- S2-3 has a durable assurance plan agents can cite.

### Dependencies

- SEC04 DONE; MT09 contracts or accepted interim dedicated-deploy scope.

---
