# Agent governance consolidation audit — 2026-10-07

Queue: direct-user-request
Initial audit base: `ce60d3120e9d4b1e456d10e0ceeb8820f5ce8756`
Final audited head before this evidence file: `1e2cf4849af875bb295dc29157af74cf73f194a9`
Ownership transfer: none
Evidence state: synchronized

## Goal

Re-audit the repository's agent/queue instructions after two repeated failure modes:

1. a second agent treated implementation already present on `main` plus no visible lock/branch/PR as permission to continue another owner's active prompt;
2. agents treated a well-described list of WAITING/BLOCKED/PARTIAL tasks as sufficient reason to stop with “no READY”, without first exhausting safe unblock, stale-gate repair, disjoint-split or next-program work.

The audit also looked for duplicated or obsolete queue algorithms that could override the canonical protocol in practice.

## Findings

### 1. Queue authority and repository evidence were conflated

`REPO_AI_README.md` previously put current code/tests at the top of one global precedence list. That is correct for implementation facts but unsafe for ownership/routing authority. A runtime SHA, green test or absent remote branch can prove repository state but cannot release another owner's `IN_PROGRESS` claim.

Repair: split implementation facts from queue/routing authority. `MASTER_ROADMAP.md` owns cross-program routing data, the current owner queue owns task status/owner/dependencies/READY set, and `PROMPT_QUEUE_PROTOCOL.md` alone owns mechanics.

### 2. Helper documents duplicated queue mechanics

The following helpers contained or risked becoming alternate selector/takeover/session workflows:

- `docs/ai/CODEX_QUEUE_RUNNER.md`
- `docs/ai/AGENTS_QUEUE_ADDENDUM.md`
- `AGENTS.md`
- `docs/ai/AGENT_START_HERE.md`
- `.github/copilot-instructions.md`
- `docs/planning/FEATURE_LIFECYCLE.md`

Repair:
- Codex runner is now a thin compatibility launcher.
- AGENTS queue addendum is now a deprecated compatibility pointer.
- Root/entrypoint/Copilot/lifecycle docs contain only fail-safe invariants and point to the canonical protocol instead of maintaining a second algorithm.

### 3. Active owner queues still contained stale selector/session rules

A scan of all 20 active queue/addendum files found six live header conflicts:

- Backend CI: `One prompt per commit/session`
- SQL: `Only the prompt marked READY may be started`
- Data Source Connector: hard `Current READY: none -> do not claim WAITING`, blanket sequentiality and `one prompt per branch/commit`
- Multi-Tenancy: local branch/commit/claim mechanics duplicated the protocol
- Stabilization: `One task per session and commit`
- GenAI: `Take only the first READY`, manual `IN_PROGRESS`, and one-task-per-session routing; its live gate also still referenced historical STAB08 instead of current master/release truth

Repair: each queue now keeps only domain-specific gates/safety rules and explicitly delegates selection, claim, lock, concurrency, promotion, no-READY and close-out mechanics to `PROMPT_QUEUE_PROTOCOL.md`. GenAI gate truth now points to current `MASTER_ROADMAP.md` / STAB evidence rather than the dated STAB08 snapshot.

### 4. Active claim close-out was underspecified

The protocol already said not to steal a live claim, but did not explicitly state that ownership survives implementation delivery while the original agent is still validating/synchronizing evidence/status.

Repair:
- implementation/runtime commit reaching `main` does not release the claim;
- missing local lock/branch/PR is not stale proof;
- another agent cannot open same-task PR/lock, finalize evidence/status or describe itself as continuing the claim;
- read-only review/handoff is allowed;
- same-task ownership stays exclusive through close-out.

### 5. “Explicit handoff/release” lacked a concrete authority contract

Repair: the protocol now defines valid ownership release/handoff sources. Silence, inactivity, commit-on-main, missing lock/branch/PR, green CI and another agent's “looks stale” assertion are not release evidence. A real transfer records previous owner, authority source/evidence/date, new owner/workspace and treatment of existing transport/worktree artifacts.

The run-log/status/evidence templates now expose an `Ownership transfer` field/record when a transfer actually occurs.

### 6. Zero-READY recovery was too passive

Repair: added a mandatory no-READY action ladder before a no-work conclusion:
1. repair stale routing;
2. execute authorized repository-local unblock/proof work;
3. narrow/resolve collisions and split a meaningful disjoint same-owner child when safe;
4. add the smallest missing owner work item after de-duplication;
5. try the next eligible program;
6. only then write Zero-READY proof.

Unknown uncommitted edits block only their exact scope; agents must re-check them, preserve them, and look for disjoint work rather than parking the whole program.

Zero-READY templates now require the unblock attempt/result, safe/disjoint-split analysis, why no safe split exists and exact unblock event.

### 7. A concurrent P-UI-54 completion left stale READY prose

While this audit was running, P-UI-54 completed. The current P-UI queue/status summary already moved to DONE, but top-level roadmap prose and the prior routing-repair evidence still said P-UI-54 was READY.

Repair:
- live master/roadmap wording now says P-UI-54 DONE;
- P-UI-50 no longer points to P-UI-54 as a READY lane;
- the prior routing-repair log is explicitly marked as a historical routing snapshot rather than live routing truth.

## Canonical document architecture after cleanup

### Policy/data owners

- `AGENTS.md` — repository-wide behavior and safety invariants
- `MASTER_ROADMAP.md` — cross-program priority/current routing data
- current owner queue — task status, named owner, dependencies and READY set
- **`docs/ai/PROMPT_QUEUE_PROTOCOL.md` — sole queue-mechanics owner**
- `docs/ai/AGENT_RUN_EVIDENCE_STANDARD.md` / `.ai/RUN_LOG_TEMPLATE.md` — completion/evidence contract
- `docs/ai/VALIDATION_SELECTOR.md` — validation selection

### Helpers retained intentionally

- `docs/ai/CODEX_QUEUE_RUNNER.md` — thin launcher only; retained because live/historical references exist
- `docs/ai/CODEX_TASK_CHECKLIST.md` — checklist only
- `docs/ai/QUEUE_STATUS_TEMPLATE.md` — formatting/evidence template only
- `docs/ai/PROMPT_BATCH_REVIEW_POLICY.md` — review procedure only
- `.cursor/rules/agent-execution-efficiency.mdc` — Cursor execution topology only
- `docs/ai/AGENTS_QUEUE_ADDENDUM.md` — deprecated compatibility pointer; retained so historical references fail safely instead of becoming broken links
- `docs/ai/NEXT_PROMPT_QUEUE.md` — historical ledger only

The deprecated addendum was intentionally **not deleted** because old evidence/docs reference it. Replacing it with a fail-safe stub is safer than creating broken historical links.

## Automatic regression protection

### `scripts/check-agent-instructions.mjs`

Now verifies:
- one canonical queue-mechanics owner;
- evidence does not grant claim authority;
- active-claim close-out invariant;
- explicit ownership handoff contract;
- Mandatory no-READY action ladder;
- helper-document role classification/deprecation;
- Zero-READY unblock/split evidence fields;
- stale Codex runner/addendum rules cannot be reintroduced;
- common-failure playbook contains claim-steal and passive-zero-READY failures.

Its self-test deliberately injects a stale Codex runner selector phrase and proves rejection.

### `scripts/check-prompt-queues.mjs`

Now scans the **live header/routing/rules portion** of all active queue/addendum files (before the first task section) and rejects stale duplicated mechanics such as:
- one prompt/task per session;
- execute only current READY;
- first READY only;
- Current READY none -> never promote WAITING;
- blanket queue sequentiality;
- one prompt per branch/commit as a routing rule.

Historical task completion/evidence prose is not scanned by this rule, so history remains immutable while live routing is protected.

Its self-test seeds a stale live BCI header rule and proves deterministic failure.

## Validation

- Direct fresh scan of all 20 active queue/addendum headers with the same forbidden-rule set: **0 findings**.
- Planning Governance run `37618667181` on `3cb4449898580cdbb7fe3549d5e8dd4d98d39230`: **success** after active-queue header normalization and the new queue-validator self-test.
- Earlier Planning Governance run `37617420941` on `95f9f13ac00e86e97a6485a91e6b986c0e5c3b1c`: **success** after instruction/helper consolidation.
- Subsequent changes only strengthen explicit helper roles/handoff evidence and are subject to the same Planning Governance workflow.

## Runtime/product impact

None. This audit changes agent governance, queue/document routing and governance validators only. It does not change analytics formulas, APIs, database behavior, frontend business behavior or deployment configuration.

## Residual policy

Historical run logs and dated audit documents may still contain old statements such as “one prompt per session” or an old READY pointer. They are historical evidence and are not rewritten wholesale. Live routing must come from `MASTER_ROADMAP.md` + the current owner queue, and live mechanics only from `PROMPT_QUEUE_PROTOCOL.md`.

If a historical document is likely to be mistaken for live routing, mark it explicitly historical/superseded rather than silently rewriting the past.
