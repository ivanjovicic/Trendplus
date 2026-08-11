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
| OBS - Observability | `none` (`OBS01` DONE; `OBS02` WAITING) | SLI/SLA contract/inventory |
| SEC - Security Evolution | `none` (`SEC01` DONE; `SEC02` WAITING) | ownership/threat-model reconciliation |

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

Status: WAITING  
Priority: future  
Feature family: observability-rollout-plan  
Parallel-safe: yes, planning only  
Owner: unassigned

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

### Dependencies

- OBS01 DONE.

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

Status: WAITING  
Priority: future  
Feature family: security-assurance-plan  
Parallel-safe: yes, planning only  
Owner: unassigned

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

### Dependencies

- SEC01 DONE.
