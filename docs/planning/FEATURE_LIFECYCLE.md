# Trendplus Feature Lifecycle

Updated: 2026-08-08  
Status: canonical planning lifecycle

Every meaningful Trendplus feature follows this lifecycle:

`Idea -> Roadmap -> Queue -> Implementation -> Hardening -> Release -> Maintenance`

The purpose is to stop ideas from bypassing ownership, correctness, safety and release evidence.

## 1. Idea

An idea describes a user/business problem and expected value. It is not yet permission to create implementation prompts.

Required questions:

- Who has the problem?
- What decision, workflow or operational pain improves?
- Which existing program most likely owns it?
- Is this genuinely new, or already represented by a roadmap/queue?

Exit condition: an owner program is identified or a clear reason exists to create a new program.

## 2. Roadmap

The roadmap defines direction, boundaries, milestones and dependencies without duplicating implementation detail.

A roadmap must state:

- problem/value;
- architecture/product boundaries;
- milestone sequence;
- dependencies and release gates;
- owning queue;
- explicit non-goals.

No roadmap may exist without an owner queue. No queue family may exist without a roadmap or an explicitly named existing roadmap.

Exit condition: the feature has an accepted place in `MASTER_ROADMAP.md` and an owner queue.

## 3. Queue

The queue turns one roadmap milestone into bounded executable prompts.

Rules:

- at most one READY prompt per program;
- all later prompts WAITING until dependencies are satisfied;
- every prompt contains: Problem, Evidence, Scope, Read first, Do, Tests, Acceptance, Dependencies;
- do not duplicate another queue's feature family;
- do not make a task READY just because it is high priority;
- current READY must be explicitly declared near the queue top.

Exit condition: the first safe, evidence-backed task is READY and the queue validator passes.

## 4. Implementation

Implementation changes the smallest safe runtime/code surface required by one prompt.

Before coding:

- confirm the prompt is still READY on current `main`;
- verify no higher-priority gate owns the same paths;
- apply tenant and analytics safety gates where relevant;
- identify the source of truth and downstream consumers;
- preserve true-zero vs unknown, freshness, confidence, authorization and tenant boundaries.

Exit condition: scoped behavior and tests satisfy the prompt acceptance criteria.

## 5. Hardening

Hardening proves the feature survives non-happy-path behavior and cross-surface use.

Typical hardening includes:

- missing/unknown/partial data;
- boundary dates and zero denominators;
- authorization/tenant negative tests;
- retry/idempotency/concurrency behavior;
- export/detail/action parity;
- performance under representative data volume;
- logging/tracing/metrics;
- rollback and compatibility behavior.

Hardening belongs to the feature's existing program unless the gap clearly belongs to STAB, PERF, OBS or SEC.

Exit condition: known failure modes are either covered or explicitly accepted as visible risk.

## 6. Release

Release is evidence, not a status inferred from merged code.

Required evidence depends on the feature but can include:

- green CI for relevant suites;
- current deployment SHA/bundle;
- live smoke;
- migration/rollback evidence;
- authorization/security checks;
- import/freshness provenance;
- performance/SLA checks;
- customer-visible documentation where needed.

Historical smoke or old readiness documents cannot prove a new release.

Exit condition: the owning release gate records PASS/WARN/BLOCKED/FAIL using current evidence.

## 7. Maintenance

A released feature enters maintenance rather than disappearing from planning.

Maintenance owns:

- production defects;
- drift between API/UI/export/action behavior;
- schema/contract compatibility;
- performance regression;
- observability gaps;
- security findings;
- customer feedback and outcome evidence;
- retirement/deprecation.

New work returns to the roadmap/queue stage when it changes product direction or architecture rather than merely fixing the accepted contract.

## Status and ownership rules

- `MASTER_ROADMAP.md` owns cross-program routing.
- The roadmap owns direction and milestones.
- The queue owns runnable work and status.
- Runtime code owns behavior, never roadmap prose.
- Dated audit/release documents own historical evidence.

When these disagree, verify current code/evidence and update the canonical layer rather than copying another conflicting description.

## Change checklist

Before adding any new prompt:

1. Search the master roadmap for an owner.
2. Search the owner roadmap/queue for the same feature family.
3. If it exists, extend it; do not create a competing queue.
4. If it does not exist, add roadmap ownership first.
5. Keep only the first unblocked task READY.
6. Run both planning and queue validators.
