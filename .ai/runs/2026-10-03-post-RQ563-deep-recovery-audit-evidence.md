Task ID: direct-user-request / post-RQ563 deep recovery
Queues:
- docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_NIVELACIJA_AUDIT_ADDENDUM.md
- docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_OPERATIONS_ACCURACY_ADDENDUM.md
Date: 2026-10-03
Agent/tool: ChatGPT
Delivery target: main
Starting main: e89f7b5bc808a6ef8b348440d63f483372302929
Evidence state: synchronized

## Why this recovery was needed

RQ563 was correctly delivered, but its final routing statement ("no safe RQ prompt is currently READY") was too narrow. It checked RQ564-RQ566 and cross-program release/owner gates, but did not re-evaluate the unfinished dependency graph inside the Nivelacija addendum.

A fresh repository audit found several repository-local prompts that are runnable without provider, production database, browser, release or owner authority.

## Dependency defects found

### Circular proof/fix pair 1

- RQ542 (NV-F6 fix) depended on NV-P3 / RQ549.
- RQ549 (NV-P3 oracle) depended on NV-F6 / RQ542.

This made both permanently WAITING even though RQ549 is explicitly written to pin current defects and let the fix flip them.

Repair: RQ549 now depends only on DONE RQ527 and is the proof-first owner. RQ542 consumes RQ549 afterward.

### Circular proof/fix pair 2

- RQ541 (NV-F5 split fix) depended on NV-P4 / RQ550.
- RQ550 (NV-P4 oracle) depended on NV-F5 / RQ541.

Repair: RQ550 now depends only on DONE RQ528 and is the proof-first owner. RQ541 consumes RQ550 afterward.

## Additional runnable work found

- RQ548: independent Pre-Nivelacija PostgreSQL oracle; its only stated harness dependency RQ525 is DONE.
- RQ546: residual chunk-load recovery/error-boundary work; no dependencies and disjoint frontend-platform path.
- RQ554: Nivelacija failure-state truth. RQ545 has already delivered the code-side backend states/diagnostics; its remaining deployed acceptance under RQ535/STAB16 is not a start gate for repository-local frontend propagation.
- RQ537 and RQ540 were already DONE but their registration-map rows were stale and were reconciled.

## Routing decision

Primary READY:
- RQ548 (P1) — independent Pre-Nivelacija oracle/golden fixture.

Additional READY:
- RQ546 (P1) — route-level chunk recovery / explicit refresh failure UX.
- RQ554 (P1) — truthful Nivelacija failure-state UI with code/correlation id.
- RQ549 (P2) — proof-first DiD/control/mapper PostgreSQL oracle.
- RQ550 (P2) — proof-first split/cross-surface parity oracle.

Held WAITING intentionally:
- RQ538/RQ539: capture the RQ548 pre-change baseline before changing event/new-stock semantics.
- RQ541: after RQ550.
- RQ543/RQ542: after RQ549; RQ543 should establish direction/maturity/overlap fields before RQ542 consumes them.
- RQ544: remains collision-sensitive with canonical 014 view work and can follow the proof baselines.
- RQ564: remains a later runtime-integrity family after semantics/oracles are stable.

No open PR or matching RQ538-RQ550 runtime branch/lock was found in the GitHub checks used for this audit.

## New uncovered gaps registered

### RQ567 — Operations visible trust/readiness

RQ515 delivered backend decisionReadiness and a frontend helper, but GitHub code search found the helper only in its utility/spec, not in real pages. operationsIntegrityStatus likewise exists in response types but is not rendered as one coherent current-Operations trust surface.

RQ567 will wire backend-authoritative readiness + integrity evidence into Inventory, Shoe Type, Daily, Pre/Post, Color and Pre-Nivelacija, rendering missing Nivelacija family proof as explicitly unverified until RQ564 rather than silently omitting it.

### RQ568 — Pre/Post certification schema parity

RQ561 is DONE for its declared safe contract but its run log explicitly records Pre/Post as UNVERIFIED because the Testcontainers schema does not install the canonical compatibility view and no maturity/overlap oracle is in that route harness.

RQ568 will install the real repository compatibility view/bootstrap path after RQ550/RQ541/RQ543 semantics stabilize and will turn Pre/Post green only when canonical SQL + oracle + endpoint all execute with no unexplained delta.

## Evidence inspected

- latest main commits through RQ560-RQ563
- RQ561 and RQ563 run logs
- Operations Accuracy and Nivelacija audit queues
- RQ525/RQ527/RQ528/RQ537/RQ547 completion evidence
- RQ515 evidence and frontend usage search
- current open PR/branch search

## Validation boundary

This was a documentation/queue architecture recovery. No runtime product code was changed and no local queue validator could be executed through the GitHub connector itself. The previous exact-main RQ563 governance run 37106203415 was successful before this documentation-only recovery. The next executing agent must run prompt/planning/agent validators after syncing these commits.
