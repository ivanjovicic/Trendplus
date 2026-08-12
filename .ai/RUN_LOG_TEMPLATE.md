# Trendplus Run Log Template

Use this for every non-trivial file-changing run and save it as:

```text
.ai/runs/<yyyy-mm-dd>-<task-id>-evidence.md
```

If a tool/session cannot safely create a durable run log, record the fallback reason in the final response and in the queue/prompt completion note.

## Required header

```text
Task ID:
Queue:
Date:
Agent/tool:
Model:
Delivery target: main | none
Main commit SHA:
Main verification:
```

## Required sections

Use each exactly once:

```text
## What was done
## Files changed
## Validation run
## Validation not run
## What was missed
## Risks
## Next
```

## Minimum content expectations

- `What was done` records the actual landed work, not the plan.
- `Files changed` lists durable repo paths; exclude uncommitted local lock files.
- `Validation run` lists exact commands and `pass | fail`.
- `Validation not run` lists skipped commands with a real reason, or `- none`.
- `What was missed` records unfinished scope, known omissions, or `- none known`.
- `Risks` records residual correctness, CI, security, tenant, perf or delivery risk, or `- none known`.
- `Next` records the next prompt/task/follow-up owner, or `- none`.

## Minimal example

```text
Task ID: SEC04
Queue: docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md
Date: 2026-08-12
Agent/tool: Codex
Model: unknown-not-exposed
Delivery target: main
Main commit SHA: <full sha>
Main verification: git rev-parse origin/main -> <full sha>

## What was done
- Added supply-chain assurance policy doc.
- Updated the queue and roadmap pointers.

## Files changed
- docs/architecture/SUPPLY_CHAIN_ASSURANCE_POLICY.md
- docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md
- docs/roadmaps/SECURITY_EVOLUTION_ROADMAP.md

## Validation run
- git diff --check -> pass
- node scripts/check-prompt-queues.mjs -> pass

## Validation not run
- dotnet test -> not run - docs-only change

## What was missed
- No CI wiring was added in this prompt.

## Risks
- Policy exists before automation wiring; scan execution is still a follow-up.

## Next
- SEC05 - Data protection and retention assurance plan
```
