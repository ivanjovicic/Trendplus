# Trendplus Agent Run Evidence Standard

Repo: `ivanjovicic/Trendplus`
Status: canonical completion-evidence standard

## Purpose

A task is not complete merely because code or documentation changed. Future agents need durable, truthful evidence of what changed, what was validated, what was skipped, what reached `main`, and what remains risky.

This document owns completion-evidence semantics. Queue execution statuses remain owned by `docs/ai/PROMPT_QUEUE_PROTOCOL.md`.

The canonical durable run-log template is:

```text
.ai/RUN_LOG_TEMPLATE.md
```

Preferred run-log location:

```text
.ai/runs/<yyyy-mm-dd>-<task-id>-evidence.md
```

## Status and evidence are separate

Do not create an extra queue status for missing evidence.

Queue statuses are exactly those defined by `PROMPT_QUEUE_PROTOCOL.md`:

```text
READY | WAITING | IN_PROGRESS | BLOCKED | PARTIAL | DONE | OBSOLETE
```

Evidence synchronization is a separate field:

```text
Evidence state: synchronized | pending | fallback <reason>
```

Rules:
- use `DONE` only when required proof and delivery evidence are synchronized;
- use `PARTIAL` when useful work exists but validation, delivery verification, run evidence or another completion requirement is still missing;
- use `BLOCKED` when an external dependency/authority prevents safe completion;
- never use `NEEDS_EVIDENCE_SYNC` as a live queue status;
- old historical notes containing retired/free-form statuses may remain historical unless the task explicitly refreshes them.

## Durable evidence requirement

For every non-trivial file-changing run, create a durable run log using `.ai/RUN_LOG_TEMPLATE.md` whenever the repository can safely be changed.

If a tool/session genuinely cannot create a durable log, record:

```text
Evidence state: fallback <reason>
Run log: fallback <reason>
```

in the queue completion note when applicable and in the final response. Do not claim high-confidence completion while required evidence is unavailable.

## Completion note

For a queue task, use this minimum shape:

```text
### Completion note

- Date: YYYY-MM-DD
- Status: DONE | PARTIAL | BLOCKED
- Completion: <concise outcome or percentage when useful>
- Changed files:
- Checks run:
- Checks not run:
- Run log: .ai/runs/<yyyy-mm-dd>-<task-id>-evidence.md OR fallback <reason>
- Evidence state: synchronized | pending | fallback <reason>
- Delivery mode: pull-request | direct-main | connector-write | none
- Main commit SHA: <full sha or pending>
- Main verification: <exact evidence or skipped reason>
- Missed: <unfinished work or none known>
- Follow-up: <prompt/task/owner or none>
- Residual risk: <one sentence or none known>
- Prompt defect / scope repair: <note or none>
```

All new or actively refreshed completion notes use the current template. Do not rely on a date gate to decide which evidence schema applies.

## Delivery truth

Local diff, local commit, pushed branch, open PR or green branch CI are transport states.

File-changing work is a `DONE` candidate only after:
- required proof is honest;
- the exact delivered SHA is known;
- fresh current `main` is verified to contain that SHA;
- queue/evidence state is synchronized when the task uses formal queue routing.

Minimum delivery fields:

```text
Delivery mode: pull-request | direct-main | connector-write | none
Main commit SHA: <full sha or pending>
Main verification: <exact git/GitHub evidence or skipped reason>
```

If the implementation reached a branch/PR but not `main`, use `PARTIAL` unless a more specific blocker applies.

## Completion gate

Before `DONE`, evidence must identify:
- actual files changed;
- validation that executed, with pass/fail outcome;
- validation intentionally not run, with reason;
- run log or explicit fallback reason;
- delivery mode;
- exact implementation SHA delivered to `main`;
- fresh verification that current `main` contains it;
- missed work or `none known`;
- residual risk or `none known`;
- next task/owner or `none`;
- prompt defect/scope repair when one occurred.

Missing required completion evidence means `PARTIAL` or `BLOCKED`, not a new status.

Do not claim 100% when residual risk says required tests, CI, delivery verification or target evidence are missing.

## Docs-only runs

Docs-only work does not require runtime build/test proof, but it still requires honest documentation/governance validation appropriate to the changed paths.

If local commands are unavailable, state that clearly, for example:

```text
Validation not run:
- local repository scripts/build/tests -> not run - connector-only session
```

Do not imply runtime behavior changed when the task only changed documentation/governance.

## GitHub connector runs

When work is performed through the GitHub connector:
- record connector-returned commit/PR/merge identifiers;
- inspect current branch/PR state before claiming delivery;
- verify the exact delivered SHA against a fresh current-`main` lookup before `DONE`;
- mark local shell/build/test commands `not run` unless they actually executed in a repository checkout;
- inspect relevant GitHub checks once when they are part of acceptance;
- `queued` is not passing proof;
- preserve a PR and report `PARTIAL`/`BLOCKED` when required proof cannot complete safely.

## Retrospective check

Before closure, verify:
- evidence describes what actually landed, not the plan;
- no diagnostics/temp files or local locks were committed accidentally;
- skipped checks name a reason and residual risk;
- scope repairs/prompt defects are recorded;
- final status matches validation and delivery strength;
- the reported next step does not contradict the status.

## Final response compact format

```text
Changed:
- ...

Validation:
- ...

Delivery:
- ...

Not done:
- ...

Risk:
- ...

Next:
- ...
```
