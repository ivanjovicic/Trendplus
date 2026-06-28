# Prompt Queue Protocol

Date: 2026-06-28
Repo: `ivanjovicic/Trendplus`

This protocol applies to:

- `docs/ai/NEXT_PROMPT_QUEUE.md`
- `docs/ai/SQL_ANALYTICS_PROMPT_QUEUE.md`
- any future focused prompt queue under `docs/ai/`

Goal: keep Codex, Cursor and manual edits from implementing the same or similar feature at the same time.

## Status model

Use these statuses exactly:

| Status | Meaning | Agent may start? |
|---|---|---|
| READY | The next runnable prompt. | Yes |
| WAITING | Valid prompt, but blocked by earlier evidence or sequencing. | No |
| IN_PROGRESS | Claimed in the current local workspace or branch. | No, unless same owner continues |
| BLOCKED | Cannot continue without missing dependency, decision or evidence. | No |
| PARTIAL | Some work landed, but acceptance is not complete. | No, unless follow-up says so |
| DONE | Acceptance met and checks recorded. | No |
| OBSOLETE | Replaced by another prompt. | No |

Rule: only one prompt per feature family should be `READY` unless the queue explicitly says `Parallel-safe: yes`.

## Local lock rule

Before starting a READY prompt, create a local uncommitted lock file:

```text
.ai/task-locks/<task-id>-<agent>.lock.md
```

Example:

```text
.ai/task-locks/Q69-codex.lock.md
```

Suggested lock content:

```md
# Local task lock

Task: Q69
Agent: Codex
Status: IN_PROGRESS
StartedAtUtc: 2026-06-28T10:00:00Z
Branch: local branch name
Feature family: analytics-sql-trust
Exclusive area: docs/tests only
Allowed files:
- docs/qa/ANALYTICS_SQL_QUERY_AUDIT.md
- Api.Tests/SupplierDecisionSchemaSqlTests.cs

Do not commit this lock file.
```

The lock is intentionally local. It tells the current workspace that the task is in progress without polluting the shared queue with IN_PROGRESS-only commits.

## Claim workflow

1. Pull the latest `main` or rebase your branch.
2. Read `.github/copilot-instructions.md`, `AGENTS.md`, this protocol and the prompt's `Read first` files.
3. Pick the first prompt with `Status: READY`.
4. Confirm that no local lock exists for the same task or same feature family.
5. Create `.ai/task-locks/<task-id>-<agent>.lock.md` locally.
6. Work only inside `Scope only` / `Allowed files`.
7. If the task needs extra scope, stop and mark the task `PARTIAL` or `BLOCKED`; create a new follow-up prompt instead of expanding silently.
8. When done, update the queue entry with:
   - final status
   - changed files
   - checks and results
   - remaining risk
   - next task
9. Delete the local lock file before final commit.

## Collision rules

Do not start a prompt if any of these are true:

- Another local lock exists for the same task.
- Another local lock exists for the same feature family.
- The prompt is `WAITING`, `BLOCKED`, `PARTIAL`, `DONE` or `OBSOLETE`.
- The task would touch files outside its allowed scope.
- The task overlaps another queue item's `Exclusive area`.
- The task requires production deploy, DB migration or auth/security decisions not listed in `Read first`.

## Queue entry template

```md
## QXX - Short task title

Status: READY
Priority: P0
Type: docs/tests/backend/frontend
Feature family: analytics-sql-trust
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/QXX-<agent>.lock.md`
Commit suggestion: `docs(scope): concise message`

### Why

One paragraph.

### Scope only

- exact files or folders

### Do not touch

- files/folders/features that would overlap with other prompts

### Read first

- `.github/copilot-instructions.md`
- `AGENTS.md`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`

### Do

1. Small, ordered steps.

### Checks

- exact commands

### Acceptance

- observable outcomes

### Notes

- Date:
- Changed files:
- Checks:
- Risk:
- Next:
```

## Ready prompt rule

A queue should clearly expose the current ready prompt near the top:

```md
Current READY prompt: Q69
```

If no prompt is ready, write:

```md
Current READY prompt: none
Reason: waiting for <dependency/evidence/decision>
```

Agents must not infer readiness from priority alone.

## Commit hygiene

- One prompt per branch/commit unless a task explicitly permits more.
- Commit message should match the prompt's suggestion as closely as possible.
- Do not commit `.ai/task-locks/*`.
- Do not mark production/live smoke as DONE from local-only checks.
- Do not mark SQL semantics as safe without tests and, when relevant, DB/EXPLAIN evidence.
