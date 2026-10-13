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

Delivery/evidence facts do not grant queue ownership. An implementation SHA on `main`, green validation or synchronized evidence does not release another owner's `IN_PROGRESS` claim; claim lifecycle and takeover remain exclusively governed by `PROMPT_QUEUE_PROTOCOL.md`.

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
- Post-close routing: <promoted task id | Zero-READY proof in run log | not applicable for direct-user-request>
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

### Main-first delivery, with mandatory PostgreSQL CI exception

Unless the task explicitly names another target branch, agents must land file-changing work on `main` before `DONE`.

- merge/push to `main` after focused local validation;
- verify fresh `origin/main` contains the implementation SHA;
- do **not** wait for CI to finish for changes with complete local required proof; if PostgreSQL/Testcontainers is required but cannot run locally, **wait for focused PostgreSQL and relevant backend checks to pass on the exact PR head SHA before merge and DONE**;
- record CI status separately under `Checks not run`, `Residual risk` or an explicit CI note (`queued`, `running`, `not inspected`, `green`, `red`);
- use `PARTIAL` when main delivery is unsafe **or the mandatory PostgreSQL PR CI is red, skipped, queued or in progress**; ordinary optional queued CI alone does not prevent DONE.

## Lessons learned 2026-10-13 (mandatory checks)

These rules come from defects found on 2026-10-13 (`docs/qa/STATE_REAUDIT_2026-10-13.md`, RQ635 reopen, PR #137). Apply them before claiming any proof.

1. **A local PostgreSQL `Passed` without Docker counts as `not run`.** Tests that need `PostgresContainerFixture` must use `[PostgresRequiredFact]` or `[PostgresRequiredTheory]` (`Api.Tests/PostgresRequiredFactAttribute.cs`). They report **Skipped** unless `CI=true` or `TRENDPLUS_REQUIRE_POSTGRES_TESTS=true`, and when required, an unavailable fixture fails. `PostgresRequiredAttributeConventionTests` enforces this. A completion note must quote the executed count from the exact CI run (for example "6 executed, 0 skipped in run N"), never a local "N/N" for PostgreSQL classes.
2. **Fixtures must provision the real dependency chain.** Do not point `DefaultConnection` and `AnalyticsConnection` at one database in supplier/analytics initializer tests: `EnsureAnalyticsSupplierDecisionSchemaAsync` then takes its unified-database branch and silently skips SQL 011-018 and `__StartupSqlScriptHistory`. This was the RQ635 root cause.
3. **Test hosts choose the readiness gate explicitly.** CI runs `ASPNETCORE_ENVIRONMENT=Testing`, where `StartupReadiness:GateApiTraffic` defaults to on, and `/api` answers 503 before routing. Every `WebApplicationFactory` host sets the gate, `false` unless readiness is under test (enforced by RQ640's convention test). A route test asserting only `!= 404` passes vacuously behind the gate.
4. **DONE requires green proof on the exact SHA.** False closes on 2026-10-13 included RQ635 (closed while every main run failed 4 of its tests), RQ638/RQ642 completion notes missing strict fields (Planning Governance red), and RQ641, which was implemented while it was still WAITING on an owner decision. Before DONE: check the status of the CI run for the delivered SHA, run all three validators, and confirm that the `Ready after` owner gate is satisfied.
5. **Temp-index commits.** After `git update-ref HEAD <new> <old>`, run `git read-tree HEAD`, or the real index shows the whole change reversed (`MM`). Use `git rev-parse` with one revision per call.
6. **CRLF and encoding.** Edit repository text with a script that preserves the file's existing EOL (Node `fs`, normalising to the original `\r\n`). PowerShell `Set-Content` and `[regex]::Replace` with a script block can change encoding or EOL and produce a whole-file diff. Always check `git diff --stat` before committing: a one-line change must not show hundreds of lines.
7. **Main CI is not cancelled for pushes to `main`** (RQ641, owner-confirmed). A cancelled run therefore means a PR/branch run or an outage, and is never proof.

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
- for formal queue work, **post-close routing recovery from the post-delivery `origin/main` SHA**;
- when next is `none`, a durable **Zero-READY proof** listing active queue/addendum files scanned, plausible non-terminal candidates, blocker class/start-gate classification, **unblock action attempted and result**, safe/disjoint-slice result, why no safe split exists and exact unblock event;
- prompt defect/scope repair when one occurred.
- when ownership changed during the task, an **Ownership transfer** record naming the previous owner, authoritative handoff/release source, evidence location/date, new owner/workspace and treatment of existing branch/PR/worktree artifacts.

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
- merge/push to `main` when permitted; do not stop at branch/PR-only state;
- do not wait for CI completion before delivering to `main` for locally certified changes; the 2026-10-13 PostgreSQL/Testcontainers owner decision is itself a named mandatory remote check when local PostgreSQL was not executable;
- mark local shell/build/test commands `not run` unless they actually executed in a repository checkout;
- inspect relevant GitHub checks when they are part of acceptance, but treat them as follow-up/residual risk unless the prompt made them mandatory;
- `queued` is not passing proof;
- preserve a PR and report `PARTIAL`/`BLOCKED` when required proof cannot complete safely.

## Retrospective check

Before closure, verify:
- evidence describes what actually landed, not the plan;
- no diagnostics/temp files or local locks were committed accidentally;
- skipped checks name a reason and residual risk;
- scope repairs/prompt defects are recorded;
- final status matches validation and delivery strength;
- for queue tasks, any older `Next: none` was invalidated and recomputed after delivery;
- the reported next step comes from the current **Post-close dependency cascade**; if it is `none`, the run contains a complete **Zero-READY proof** after the protocol's Mandatory no-READY action ladder and does not rely on stale queue prose.

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
