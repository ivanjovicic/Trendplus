# Trendplus Run Log Template

Use this for every non-trivial file-changing run and save it as:

```text
.ai/runs/<yyyy-mm-dd>-<task-id>-evidence.md
```

If a tool/session genuinely cannot create a durable run log, record an explicit fallback reason in the final response and in the queue completion note when applicable.

## Required header

```text
Task ID:
Queue: <queue path | direct-user-request>
Date:
Agent/tool:
Delivery target: main | none
Working branch / PR:
Main commit SHA:
Main verification:
Evidence state: synchronized | pending | fallback <reason>
```

## Required sections

Use each exactly once:

```text
## What was done
## Files changed
## Validation run
## Validation not run
## Documentation impact
## What was missed
## Risks
## Next
```

## Minimum content expectations

- `What was done` records actual landed work, not the plan.
- `Files changed` lists durable repository paths and excludes local lock/temp files.
- `Validation run` lists exact commands/checks that actually executed with `pass | fail`.
- `Validation not run` lists skipped checks with a real reason, or `- none`.
- `Documentation impact` says what durable owner docs changed, or why none were needed.
- `What was missed` records unfinished scope/known omissions, or `- none known`.
- `Risks` records residual correctness, CI, security, tenant, performance or delivery risk, or `- none known`.
- `Next` records the next prompt/task/follow-up owner, or `- none`.
- `Main commit SHA` is the implementation/delivery SHA that current `main` was freshly verified to contain; use `pending` until that proof exists.
- `Evidence state` is separate from queue status and never creates a new queue status.

## Queue completion-note backlink

When a queue prompt is moved to `DONE`, `PARTIAL` or `BLOCKED`, the completion note should include:

```text
Run log: .ai/runs/<yyyy-mm-dd>-<task-id>-evidence.md
Evidence state: synchronized | pending | fallback <reason>
```

If a durable run log could not be created safely, keep the same keys and use `fallback <reason>`.

`NEEDS_EVIDENCE_SYNC` is not a live queue status. If evidence or delivery verification is incomplete, use `PARTIAL` (or `BLOCKED` for a real external blocker) and describe the evidence state explicitly.

## Minimal example

```text
Task ID: SEC04
Queue: docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md
Date: 2026-08-15
Agent/tool: Codex
Delivery target: main
Working branch / PR: agent/sec04 / #123
Main commit SHA: <full implementation sha>
Main verification: passed - origin/main contains <full implementation sha>
Evidence state: synchronized

## What was done
- Added supply-chain assurance policy documentation.
- Updated the owning queue and roadmap pointers.

## Files changed
- docs/architecture/SUPPLY_CHAIN_ASSURANCE_POLICY.md
- docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md
- docs/roadmaps/SECURITY_EVOLUTION_ROADMAP.md

## Validation run
- git diff --check -> pass
- node scripts/check-prompt-queues.mjs -> pass

## Validation not run
- dotnet test -> not run - docs-only change

## Documentation impact
- updated the owning policy/queue/roadmap documents listed above

## What was missed
- No CI wiring was added in this task.

## Risks
- Policy exists before automation wiring; scan execution remains follow-up work.

## Next
- SEC05 - Data protection and retention assurance plan
```
