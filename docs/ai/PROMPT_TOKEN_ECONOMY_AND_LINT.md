# Trendplus Prompt Token Economy and Lint Rules

Date: 2026-07-01
Repo: `ivanjovicic/Trendplus`
Status: agent prompt-quality rulebook
Adapted from: AgentsWatch prompt token economy / lint docs, simplified for Trendplus

## Purpose

This document keeps Codex/Cursor/ChatGPT runs small, scoped and cheap. It applies to all non-trivial Trendplus prompts, especially analytics reliability, SQL, frontend/export, worker, deployment and docs queue work.

## Default rule

Use this compact rulebook for normal runs. Do not make every agent read every queue, audit and standard before coding.

A prompt is not runnable unless it has:

- one repository
- one prompt ID or explicit task name
- one primary run mode
- token budget
- owned paths or discovery limit
- avoid paths or non-goals
- validation command or blocked reason
- stop rules
- expected final evidence

If any item is missing, rewrite/split the prompt before implementation.

## Run modes

Use exactly one primary mode:

| Mode | Use when |
|---|---|
| `validation-only` | run/build/test/smoke without code changes |
| `investigation-only` | root cause unknown; produce findings and next prompt |
| `implementation` | one scoped runtime/docs change with tests/checks |
| `tests` | add or repair tests only |
| `docs/evidence` | queue/status/run evidence/docs only |
| `review-only` | inspect docs/code and report, no patch |
| `diff-only review` | review a known diff/commit/PR only |

Split the prompt if it needs more than one primary mode.

## Token budgets

### Low budget

Use for one bug, one formatter, one queue update, one docs/evidence patch, one validation-only task.

Limits:

```text
Max docs before first action: 3
Max broad searches: 2
Max files inspected: 8
Max files edited: 3
Max validation commands: 3
Max commits: 1 small commit
```

### Medium budget

Use for one feature slice, one analytics contract family, one backend endpoint + tests, one frontend page + tests.

Limits:

```text
Max docs before first action: 5
Max broad searches: 4
Max files inspected: 15
Max files edited: 6
Max validation commands: 5
Must summarize before continuing if scope expands
```

### High budget

Use only for architecture/doc audit, release evidence, broad review without implementation, or queue refactor.

Limits:

```text
Max docs before first action: 8
Implementation edits: forbidden unless explicitly scoped
Summarize every 10 files inspected
Stop if a smaller prompt can continue the work
```

## Default read set

Most Trendplus implementation prompts should read only:

1. `docs/ai/AGENT_START_HERE.md`
2. one routing/index document, e.g. `docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md`
3. the target prompt section
4. one relevant contract/safety doc
5. source/test files in `Scope only`

Do not read every audit/addendum unless the routing/index says the target prompt has a read-together dependency.

## Prompt lint checklist

A reusable prompt passes lint only if:

- repository is named
- prompt ID/queue/lane is named
- exactly one run mode is selected
- token budget is low/medium/high
- owned paths are listed
- avoid paths/non-goals are listed
- scope limiter is present
- validation command or blocked reason is named
- stop rules are explicit
- final evidence format is named
- prompt does not require long chat history
- prompt does not ask for whole-repo work
- prompt does not mix SQL, frontend, docs, deployment and runtime in one task unless explicitly an audit

## Automatic fail conditions

Reject or rewrite prompts containing:

- `analyze the whole repo`
- `fix everything`
- `make production-ready` without a gate
- `validation optional`
- `skip tests`
- `do as much as possible`
- `continue from chat history`
- `mark done if it looks okay`
- runtime deploy proof mixed with SQL/formula changes
- dashboard polish before correctness gates

## Trendplus-specific prompt gates

### Analytics reliability

Also run `docs/ai/ANALYTICS_AGENT_SAFETY_GATE.md`.

### SQL analytics

Do not mark SQL semantics safe without tests and, when relevant, DB/EXPLAIN evidence.

### Deployment/Render/Fly

Do not mark live smoke or production health as done from local-only checks.

### Frontend export/reporting

If table value changes, check detail/export/report/action payload parity or explicitly state why out of scope.

## Discovery discipline

Low-budget discovery:

```text
max searches: 2
max files opened after search: 5
max unrelated results inspected: 0
```

Medium-budget discovery:

```text
max searches: 4
max files opened after search: 12
max unrelated results inspected: 2
```

If the target is not found within budget, stop and create a better investigation prompt.

## Final answer requirements

Low-budget final format:

```text
Prompt ID:
Files changed:
Validation:
Result:
Missed:
Next:
Risk:
Token waste avoided:
```

Medium/high-budget final format:

```text
Prompt ID:
Run mode:
Token budget:
Files inspected:
Files changed:
Validation run:
Validation not run:
Commit SHA:
Completion %:
Missed:
Follow-up:
Residual risk:
Token waste avoided:
```

If any field is unknown, write `unknown` and explain briefly. Never claim validation passed unless it actually ran or CI evidence is cited.
