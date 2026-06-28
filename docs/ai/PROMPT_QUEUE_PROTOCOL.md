# Prompt Queue Protocol

Date: 2026-06-28
Repo: `ivanjovicic/Trendplus`

This protocol applies to:

- `docs/ai/NEXT_PROMPT_QUEUE.md`
- `docs/ai/SQL_ANALYTICS_PROMPT_QUEUE.md`
- any future focused prompt queue under `docs/ai/`

Goal: keep Codex, Cursor and manual edits from implementing the same or similar feature at the same time while preserving analytics trust: no fake zero, no fake green, no fake freshness, no fake recommendation and no hidden fallback.

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

## Prompt quality rules for analytics reliability

Use these rules for analytics, reporting, dashboard, table, chart, export and action-queue prompts.

### 1. Evidence-first rule

A reliability prompt must name the evidence already found before asking for a fix.

Include:

- file/function names inspected
- observed current behavior
- risk classification: `confirmed`, `likely`, `suspicious`, or `contract gap`
- whether the prompt changes runtime behavior or only adds docs/tests

Do not write prompts that only say “improve analytics reliability” without naming the exact failure mode.

### 2. Contract-before-code rule

If the correct behavior is ambiguous, the prompt must explicitly say whether the agent may decide the product contract.

Allowed patterns:

- `Contract is fixed: implement this exact behavior.`
- `Contract decision required: add tests/docs and stop as BLOCKED if owner decision is needed.`
- `Default contract: use this behavior unless tests prove existing consumers require compatibility.`

Never let the implementation silently choose between two business meanings, such as revenue vs cost, line count vs receipt count, ratio vs percent, or true zero vs unknown.

### 3. No fake confidence rule

Every prompt touching analytics values must check whether missing evidence can become a trusted-looking value.

Forbidden without explicit metadata:

- missing evidence becoming `0`
- missing baseline becoming `0%` or `100%`
- unavailable data becoming `good`, `healthy`, `maintain`, `fresh`, `measured`, or `normal`
- low-coverage estimates ranking as high-confidence recommendations
- signal-review actions carrying confirmed expected impact

If a prompt changes a fake-confidence behavior, tests must cover both true-zero and unknown/unavailable cases.

### 4. Unit/denominator rule

Every prompt touching percentages, ratios, shares, averages or counts must specify:

- unit: ratio `0.35` or percent unit `35`
- numerator
- denominator
- whether denominator is visible rows, returned rows, all analyzed rows, filtered rows, pair-eligible rows, distinct receipts, sale lines, or units
- zero-denominator behavior
- no-baseline behavior

The same unit must be preserved across API, UI, detail, chart and export unless the prompt explicitly introduces a conversion layer.

### 5. Surface parity rule

A prompt must list all user-visible surfaces that need parity.

Typical surfaces:

- API response DTO
- frontend TypeScript type
- table cell
- chart value/tooltip
- detail drawer/snapshot
- CSV/XLSX/PDF/export payload
- report print preview
- action queue payload

If a fix affects a value that can be exported, the prompt must include export/detail verification or explicitly state why export is out of scope.

### 6. Date/time range rule

Prompts touching dates must define exact range semantics.

Required decisions:

- exact timestamp range vs date-only range
- inclusive end vs half-open end
- timezone source
- how selected `toDate` includes the whole local/UTC day
- previous-period length and boundary overlap rules

Preferred default for UI date-only filters: half-open ranges, `>= fromDate.Date` and `< toDate.Date.AddDays(1)`, unless the endpoint is explicitly timestamp-based.

### 7. Test matrix rule

A prompt must include a minimum useful test matrix.

For backend analytics:

- normal positive evidence
- true zero evidence
- missing/unavailable evidence
- no-baseline case
- low coverage/fallback case
- boundary date case when relevant

For frontend/reporting:

- visual table value
- detail snapshot value
- chart/tooltip value when relevant
- export payload/document value when relevant
- empty/loading/error state when relevant

For SQL:

- test current behavior before changing runtime SQL
- prove column names/order/compatibility when changing views
- record when DB/EXPLAIN evidence is missing

### 8. Scope minimization rule

Each prompt should fix one reliability contract family.

Do not mix:

- SQL semantics and frontend labels
- backend formulas and export renderer typing
- date-boundary fixes and margin/cost fixes
- action write behavior and dashboard chart polish
- runtime SQL changes and deploy proof

If two tasks must share a contract, create a short contract doc or mark one task as dependent. Do not implement two prompt families independently with slightly different terms.

### 9. Compatibility rule

Any fix that can change business output must document before/after behavior.

Required when changing output semantics:

- old value and old meaning
- new value and new meaning
- compatibility field, migration note or explicit breaking-change note
- downstream consumers checked

Prefer additive fields like `baselineStatus`, `sourceStatus`, `profitReliable`, `costMissing`, `dataQualityStatus`, `emptyReason`, `unit`, or `denominatorScope` when changing existing numeric fields would be risky.

### 10. Stop conditions

The agent must stop and mark `BLOCKED` or `PARTIAL` if:

- correct business contract is unclear
- required evidence cannot be produced with available tests
- fix requires files outside scope
- two queues define overlapping but inconsistent contracts
- a real DB/production dataset is required but unavailable
- implementation would hide an unknown as zero/good/maintain/fresh/measured

When blocked, record the smallest missing decision and propose the next prompt.

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

### Evidence already found

- Files/functions:
- Current behavior:
- Risk class: confirmed/likely/suspicious/contract gap

### Contract

- Fixed/default contract:
- Unit/denominator, if relevant:
- No-data/no-baseline behavior, if relevant:

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

### Test matrix

- normal evidence:
- true zero:
- missing/unavailable evidence:
- no baseline:
- low coverage/fallback:
- UI/export/detail parity, if relevant:

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

## Global prioritization rule

When multiple queue files exist, local `P0` is not enough to decide the next task. Use the global priority review if present:

- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md`

If no global review exists, choose by this order:

1. wrong actionable recommendation/expected impact
2. wrong displayed/exported numeric value
3. fake zero/fake green/fake freshness/fake measured state
4. date/period boundary correctness
5. dataScope/store/filter lineage
6. trust metadata/export preservation
7. chart interpretation/copy polish

Do not start a lower-risk addendum prompt just because it is local `P0` if a higher-risk active queue prompt is still READY.

## Commit hygiene

- One prompt per branch/commit unless a task explicitly permits more.
- Commit message should match the prompt's suggestion as closely as possible.
- Do not commit `.ai/task-locks/*`.
- Do not mark production/live smoke as DONE from local-only checks.
- Do not mark SQL semantics as safe without tests and, when relevant, DB/EXPLAIN evidence.
- If checks were not run, state that explicitly in queue notes and final response.
