# Trendplus Agent Run Evidence Standard

Date: 2026-07-01
Repo: `ivanjovicic/Trendplus`
Status: completion evidence standard
Adapted from: MathLearning prompt evidence template and AgentsWatch run evidence rules

## Purpose

A prompt is not complete just because code or docs changed. Future agents need durable evidence: what changed, what was validated, what was skipped, and what remains risky.

This standard applies to all non-trivial Trendplus queue work.

## Default evidence location

Preferred durable run log:

```text
.ai/runs/<yyyy-mm-dd>-<prompt-id>-evidence.md
```

If a tool/session cannot safely create a `.ai/runs` file, record fallback evidence in:

- the queue prompt `Notes` section, or
- the final response, and
- any relevant audit/status document.

Do not mark a prompt high-confidence `DONE` if there is no run log and no explicit fallback reason.

## Minimum local claim note

Before implementation files are edited, the prompt should be locally claimed by status or lock:

```text
IN_PROGRESS (YYYY-MM-DD, <agent/lane>, local claim)
```

Rules:

- Use the existing `.ai/task-locks/<task-id>-<agent>.lock.md` local lock workflow from `PROMPT_QUEUE_PROTOCOL.md`.
- Do not commit lock files.
- Do not push claim-only commits.
- If a prompt is visibly `IN_PROGRESS` on main, another agent must not work on it unless reassigned.

## Minimum completion note

Use this format in queue notes or run log:

```text
Done <percent>% (YYYY-MM-DD, commit <short-sha> on main)
Model: <provider/model or unknown-not-exposed> via <client/tool>
Validation: <exact command(s), test files, or skipped reason>
Run log: .ai/runs/<yyyy-mm-dd>-<prompt-id>-evidence.md OR fallback <reason>
Waste: <categories or none recorded>
Missed: <what was not completed or none known>
Follow-up: <prompt ID or none>
Residual risk: <one sentence or none known>
```

Use `NEEDS_EVIDENCE_SYNC` instead of `DONE` when commit SHA, validation, missed work, model/client metadata or run evidence cannot be verified.

## Delivery truth

Local diff, local commit, pushed branch, open PR or green branch CI are transport states. File-changing work becomes a `DONE` candidate only after the exact delivered SHA is verified on current `main`, whether delivery used direct-main or a pull request.

Minimum delivery fields for file-changing work:

```text
Delivery mode: direct-main | pull-request | connector-write | none
Main commit SHA: <full sha or pending>
Main verification: <exact git/GitHub evidence or skipped reason>
```

## Hard completion gate

Before marking `DONE`, evidence must include:

- completion percentage
- changed files
- commit SHA or explicit no-commit reason
- main verification evidence or explicit no-delivery reason
- validation command(s) or skipped-validation reason
- model/client metadata or `unknown-not-exposed`
- missed work or `none known`
- follow-up prompt or `none`
- residual risk or `none known`
- token/waste note

If any are missing, use `PARTIAL`, `BLOCKED`, or `NEEDS_EVIDENCE_SYNC`.

## Completion percentage guide

| Score | Meaning |
|---|---|
| 95-100% | Prompt completed, targeted tests/evidence strong, no meaningful follow-up needed. |
| 80-94% | Useful completion with minor gaps or residual risk. |
| 60-79% | Runtime/docs landed, but verification, CI, evidence or parity is incomplete. |
| 40-59% | Partial implementation; important scope moved to follow-up. |
| <40% | Mostly analysis/docs or blocked attempt; not complete. |

Score caps:

| Situation | Maximum score |
|---|---:|
| Docs-only change, no `git diff --check` or path verification run | 85% |
| Runtime change but targeted tests not run | 79% |
| Queue status updated but no commit SHA/run evidence | 70% |
| Model/timing metadata missing and not marked `unknown-*` | 65% |
| Prompt asked for validation but only reports it is missing | 60% |
| Production/live smoke claimed from local-only checks | 50% |

Do not claim 100% if residual risk says tests, CI, deploy smoke or target evidence are missing.

## Prompt retrospective checklist

Before closing a prompt, answer:

- Did completion % match actual validation strength?
- Were diagnostics/temp logs/probe code removed?
- Were checks run in dependency order?
- Are skipped checks named with reason and risk?
- Did the run stay within token/file budget?
- Did any repeated mistake require a docs/rule update?
- Is there a follow-up prompt for missed work?
- Does residual risk contradict the score?

## Evidence for docs-only runs

Docs-only runs may use:

```text
Validation: none (docs-only); path references reviewed; git diff --check not run because <reason>
```

But they must not claim runtime behavior changed or tests passed.

## Evidence for GitHub connector runs

When work is done through the GitHub connector:

- record commit SHA returned by the connector;
- still verify that SHA on fresh current `main` before claiming `DONE`;
- say tests/checks were not run unless another tool actually ran them;
- cite changed docs/files in the final response when possible;
- do not claim local build/test success.

## Final response compact format

```text
Changed:
- ...

Validation:
- ...

Not done:
- ...

Completion:
- ...%

Risk:
- ...

Next:
- ...
```
