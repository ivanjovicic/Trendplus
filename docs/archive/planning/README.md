# Historical Planning Snapshot Archive

> [!WARNING]
> **HISTORICAL SNAPSHOTS ONLY.**
> Files listed here are retained as evidence of what the repository believed at a specific point in time. They are not current routing, current READY state, or current release-readiness proof.

## Current authoritative sources

Use these before acting on any historical snapshot:

1. `MASTER_ROADMAP.md` — cross-program ownership, current READY pointers, blockers, parallel-safe lanes and milestones.
2. `docs/ai/AGENT_START_HERE.md` — agent read order and routing rules.
3. The current owner-queue header — authoritative task status inside a program.
4. `docs/ai/PROMPT_QUEUE_PROTOCOL.md` — queue status and governance rules.

For current release evidence, prefer the latest dated STAB/release evidence explicitly referenced by the master roadmap and current STAB queue.

## Archived in place

These files intentionally remain at their original paths so existing links and historical references do not break. A `HISTORICAL SNAPSHOT` banner at the top prevents them from being mistaken for current truth.

| File | Snapshot date | Why historical | Current authority |
|---|---:|---|---|
| `docs/qa/ANALYTICS_QUEUE_RECONCILIATION.md` | 2026-08-05 | STAB02 reconciliation includes old `Current runnable` / `Next READY` pointers | `MASTER_ROADMAP.md` + current owner queues |
| `docs/Analytics/ANALYTICS_PRODUCTION_READINESS_STATUS.md` | 2026-05-31 | old readiness verdict and route smoke state | `docs/qa/ANALYTICS_PRODUCTION_READINESS_STATUS_2026-08-06.md` + current STAB queue |
| `docs/qa/ANALYTICS_LIVE_SMOKE_RESULT.md` | 2026-06-19 | deployment smoke tied to an old bundle/runtime state | latest dated release evidence + current STAB queue |
| `docs/qa/TRENDPLUS_STABILIZATION_STATUS.md` | 2026-07-01 | old stabilization priority list references RQ tasks that have since moved/completed | `MASTER_ROADMAP.md` + analytics router/current queues |

## Historical ledger retained at canonical path

`docs/ai/NEXT_PROMPT_QUEUE.md` is already explicitly marked as a historical Codex ledger. It remains in place because historical Qxx evidence still links to it and the legacy queue validator currently inventories the file. Do not start work from its old TODO/next-step prose.

## Active addenda are not archived

Analytics reliability addenda may contain old snapshot prose such as `Main queue READY prompt: RQ01`, but the documents also contain still-valid `WAITING` task definitions. They are therefore **not** archived as whole documents.

Rule:

- task definitions/statuses in active addenda remain useful;
- stale cross-program or `Main queue READY` prose is historical routing metadata;
- `MASTER_ROADMAP.md` plus the current owner-router/header wins on conflict.

## Archival rule

A planning/status document should be marked historical when all are true:

1. it records a dated point-in-time status, READY pointer, deployment state, or sprint order;
2. a newer canonical router/evidence document exists;
3. using the old document as current truth could select the wrong task or produce a false readiness claim;
4. the document still has historical value and should not be deleted.

Prefer **archive in place + banner** over moving the file when moving would break links. Do not edit historical bodies merely to make them agree with current state.
