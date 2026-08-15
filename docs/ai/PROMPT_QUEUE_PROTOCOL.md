# Prompt Queue Protocol

Updated: 2026-08-15
Repo: `ivanjovicic/Trendplus`

This protocol defines live prompt-queue governance. Cross-program routing lives in `MASTER_ROADMAP.md`; feature/product lifecycle lives in `docs/planning/FEATURE_LIFECYCLE.md`.

## Active queue families

Existing execution programs:

- `docs/ai/BACKEND_CI_REPAIR_PROMPT_QUEUE.md` + `docs/ai/BACKEND_CI_REPAIR_EVIDENCE_ADDENDUM.md` (`BCI`)
- `docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md` (`STAB`)
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE*.md` and SQL queue (`RQ` / `Q`)
- `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md` (`P-UI`)
- `docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md` (`QDB`)
- `docs/ai/MULTITENANCY_PROMPT_QUEUE.md` (`MT`)
- `docs/ai/GENAI_PRODUCT_PROMPT_QUEUE.md` (`GAI`)

Future planning programs:

- `docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md` (`DEX`, `RL`, `DT`)
- `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md` (`PERF`, `OBS`, `SEC`)

`docs/ai/NEXT_PROMPT_QUEUE.md` is a historical ledger and is not a live router.

## Canonical selection rule

1. Read `MASTER_ROADMAP.md`.
2. Resolve the owning program and its current READY pointer.
3. Preserve the existing global priority before considering lower-priority programs.
4. Treat `P-UI` as a supplemental presentation lane: it may run only when path-safe and it must not displace BCI/STAB/RQ/QDB/MT/GAI priority or repair analytics correctness through frontend invention.
5. Start only a prompt whose status is READY and whose dependencies are satisfied.
6. Do not resurrect a DONE/PARTIAL/WAITING prompt because an older addendum says it was once next.
7. A future planning READY (`DEX/RL/DT/PERF/OBS/SEC`) authorizes only its documented planning/contract scope. It does not authorize runtime implementation or outrank higher-priority gates.

## Status model

Use these statuses exactly:

| Status | Meaning | Agent may start? |
|---|---|---|
| READY | Current runnable prompt in its program. | Yes, subject to master priority/dependencies |
| WAITING | Valid later prompt. | No |
| IN_PROGRESS | Claimed by current owner/workspace. | Only same owner continues |
| BLOCKED | Missing dependency/decision/evidence that prevents safe progress. | No |
| PARTIAL | Useful work exists but acceptance/proof/delivery is incomplete. | No unless an explicit follow-up says so |
| DONE | Acceptance met with synchronized evidence and delivery truth. | No |
| OBSOLETE | Replaced by current evidence/prompt. | No |

`TODO`, `OPEN`, `COMPLETE`, `NEEDS_EVIDENCE_SYNC`, and other free-form live statuses are invalid.

Evidence synchronization is not a queue status. Use the separate evidence field defined by `docs/ai/AGENT_RUN_EVIDENCE_STANDARD.md`:

```text
Evidence state: synchronized | pending | fallback <reason>
```

If implementation is useful but evidence/delivery verification is incomplete, use `PARTIAL`; use `BLOCKED` only when a real blocker prevents safe completion.

## READY invariants

- A program may have zero or one READY prompt; more than one READY in the same program is invalid.
- Zero READY is valid only when the owner queue/current-READY table and `MASTER_ROADMAP.md` explicitly declare `none` (or the equivalent named blocked/complete current truth). Do not infer a valid zero merely because no task happens to be marked READY.
- Multiple programs may each have one READY prompt; global execution priority still comes from `MASTER_ROADMAP.md`.
- Parallel-safe means path/feature-family parallelism is allowed; it never means dependency gates can be skipped.
- Current READY (or explicit `none`) must be declared near the queue top or in the queue's per-program current-READY table.
- All later prompts remain WAITING until dependencies are met or the current pointer is explicitly advanced.
- A follow-up/evidence addendum belongs to the same program as its parent queue; it does not create a second READY allowance.

## Required prompt sections

Every new live prompt must contain:

1. `Problem`
2. `Evidence`
3. `Scope`
4. `Read first`
5. `Do`
6. `Tests`
7. `Acceptance`
8. `Dependencies`

Existing legacy prompts may retain richer historical templates, but new prompt families must not omit these eight sections.

## Mechanical prompt conflicts

A stale file count, old "next READY" sentence, contradictory `Avoid paths` line or similar mechanical defect is not by itself a blocker when the owner program, current `READY` pointer and acceptance outcome are otherwise clear.

When this happens:

1. Keep the owner program, current `READY` pointer and acceptance stronger than stale prose.
2. Take the smallest same-owner repair needed to make the prompt executable.
3. Record the scope repair or prompt defect in the completion note/run evidence.
4. Do not use this exception to cross into another program, tenant authority, schema/API authority, secrets or production-only decisions.

## Local lock rule

Before runtime implementation of a READY prompt, create an uncommitted local lock:

```text
.ai/task-locks/<task-id>-<agent>.lock.md
```

Planning-only prompts may use a lock when several agents are active, but the lock must never be committed.

Suggested content:

```md
# Local task lock
Task: <id>
Agent: <agent>
Status: IN_PROGRESS
StartedAtUtc: <timestamp>
Branch: <branch>
Feature family: <family>
Exclusive area: <paths/contract>
```

## Claim workflow

1. Refresh current `main`/remote state.
2. Read `AGENTS.md`, `.github/copilot-instructions.md`, `docs/ai/AGENT_START_HERE.md`, `MASTER_ROADMAP.md`, this protocol and the target prompt.
3. Verify the queue still declares the task READY.
4. Verify dependencies and global priority.
5. Confirm no lock/branch/PR owns the same feature family/paths where that evidence is available.
6. Create local lock for implementation work.
7. Work only inside Scope.
8. If extra scope crosses an owner/program boundary, stop as PARTIAL/BLOCKED and create a separate follow-up plan. A smallest same-owner mechanical repair allowed above is recorded and may continue.
9. Run exact tests/checks.
10. Record changed files, checks, remaining risk and next status.
11. Delete local lock before commit.

## Collision rules

Do not start when:

- another active owner holds the same task/feature family;
- the prompt is not READY;
- the task overlaps a higher-priority exclusive path;
- required dependency is not DONE/accepted;
- runtime work is being inferred from a planning-only READY;
- the task would duplicate another queue's owner family;
- production/deploy/auth/tenant decisions are required but absent.

## Reliability rules

Prompts affecting analytics/decision/reporting values must specify the relevant contract facts:

- source of truth;
- unit (ratio vs percent unit, currency, quantity, etc.);
- numerator/denominator when relevant;
- true-zero behavior;
- missing/unknown behavior;
- no-baseline behavior;
- freshness/fallback behavior;
- affected API/UI/detail/chart/export/report/action surfaces;
- before/after compatibility when business meaning changes.

Missing evidence must not silently become zero, healthy, fresh, maintain, measured, confident or another trusted-looking default.

## Tenant/security rules

Tenant-sensitive prompts must specify:

- canonical tenant source and membership authority;
- missing/mismatched tenant behavior;
- DB/raw-SQL paths;
- cache keys/invalidation;
- jobs/outbox/imports;
- storage/documents/exports;
- two-tenant negative-test plan;
- dedicated-deployment compatibility.

Caller-provided tenant/store/source/path identity is not authorization by itself.

## Date/count/surface rules

- Date-only filters should normally use explicit whole-day half-open semantics unless a contract says otherwise.
- Returned/visible count must not be labelled total matching count without evidence.
- Table/detail/chart/export/report/action values must preserve the same semantics or explicitly document a conversion.

## Stop conditions

Mark BLOCKED/PARTIAL rather than guessing when:

- business contract is unclear;
- source of truth is unclear;
- tenant/authorization source is unclear;
- required evidence cannot be produced;
- fix needs unrelated files/programs;
- two queues define inconsistent ownership;
- a real dataset/provider decision is required but unavailable;
- implementation would hide unknown as zero/green/fresh/measured;
- performance/security/AI work would weaken correctness or release gates.

## Completion note

A completed or partially completed prompt records at minimum:

```md
### Completion note

- Date:
- Status: DONE | PARTIAL | BLOCKED
- Completion:
- Changed files:
- Contract/runtime behavior changed:
- Checks run:
- Checks not run:
- Run log:
- Evidence state: synchronized | pending | fallback <reason>
- Delivery mode:
- Main commit SHA:
- Main verification:
- Missed:
- Follow-up:
- Residual risk:
- Next:
- Prompt defect / scope repair:
```

Production/live smoke may be marked complete only from real current deployment evidence.

All new or actively refreshed completion notes use the current evidence contract:

- `Run log:` is mandatory and points to durable `.ai/runs/...` evidence or `fallback <reason>`;
- `Evidence state:` is mandatory and is separate from queue status;
- `Delivery mode:`, `Main commit SHA:` and `Main verification:` are mandatory for file-changing work;
- older completion notes remain historical evidence and are not retroactively normalized unless a task is actively refreshing them.

For every non-trivial file-changing prompt run, also create a durable run log in `.ai/runs/<yyyy-mm-dd>-<task-id>-evidence.md` using `.ai/RUN_LOG_TEMPLATE.md`, or record an explicit fallback reason when a durable log could not be created safely.

Minimum durable run-log sections are owned by `.ai/RUN_LOG_TEMPLATE.md`; do not maintain a second copied section list here.

## Validation

Choose runtime/docs proof through `docs/ai/VALIDATION_SELECTOR.md`.

Run both governance layers when queue/planning governance is changed and the commands are available:

```text
node scripts/check-agent-instructions.mjs --self-test
node scripts/check-agent-instructions.mjs
node scripts/check-prompt-queues.mjs --self-test
node scripts/check-prompt-queues.mjs
node scripts/check-planning-architecture.mjs --self-test
node scripts/check-planning-architecture.mjs
```

`check-prompt-queues.mjs` validates the execution queues it inventories, including the BCI parent queue and BCI evidence addendum. `check-planning-architecture.mjs` validates the master roadmap, owner roadmap/queue symmetry and the DEX/RL/DT/PERF/OBS/SEC planning queues, including explicit zero-READY declarations.

## Commit hygiene

- One prompt per implementation commit unless the prompt explicitly allows a bounded docs consolidation.
- Documentation consolidation may use multiple logical commits by planning family.
- Do not commit `.ai/task-locks/*`.
- Preserve historical evidence; use OBSOLETE/archive/current-pointer language rather than deleting proof.
- If checks were not run, say so explicitly.
