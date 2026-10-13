Task ID: analytics-screen-bug-review (registers RQ651-RQ654)
Queue: direct-user-request
Date: 2026-10-13
Agent/tool: Cursor agent
Delivery target: main
Working branch / PR: direct to main
Main commit SHA: pending
Main verification: pending
Evidence state: pending
Ownership transfer: none

## What was done
- The user asked to pull main, analyze the analytics screens for possible bugs, and register them as queue prompts on main.
- RQ645 (the earlier user-approved implementation) was not started. It is IN_PROGRESS for Codex on `codex/rq645-supplier-contracts-20261013` / PR #145 (latest commit 2026-10-13 17:19 CEST, CI pending), so takeover is not permitted.
- Mapped all 18 analytics routes to page components and searched them for the AGENTS.md bug classes: fake zero, error vs empty, stale or out-of-order responses, UTC/local day shift, and frontend decision recreation.
- Confirmed 4 new findings by reading code, schemas and formatters. Each was registered as a READY prompt (RQ651-RQ654). Duplicates and unproven candidates were recorded but not queued.
- RQ650 was skipped because open PR #146 reserves it.

## Files changed
- docs/qa/ANALYTICS_SCREEN_BUG_REVIEW_2026-10-13.md (new)
- docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_POST_IMPORT_VALUE_ADDENDUM.md (registration note + RQ651-RQ654 sections)
- MASTER_ROADMAP.md (registration line)
- .ai/runs/2026-10-13-analytics-screen-bug-review-evidence.md

## Validation run
- node scripts/check-prompt-queues.mjs -> see delivery commit
- git diff --check -> see delivery commit

## Validation not run
- Frontend tests/build: not run because this is a docs-only change; each prompt requires a failing-first test when implemented.
- Browser verification against live data: not run. The review was static and the findings are code-path proofs.

## Documentation impact
- New QA review document plus queue/roadmap registration. Edits avoid the header and RQ-row lines changed by PR #145 to prevent merge conflicts.

## What was missed
- Static review only. Visual/layout issues and runtime-only defects were not in scope.
- InsightStudio error swallowing is noted but not queued (experimental route).

## Risks
- PR #146 also appends RQ650 at the end of the addendum, so whichever merges second needs a trivial rebase.
- RQ654 may resolve to a documented UTC decision if the backend business day is UTC by contract.

## Post-close routing recovery
- not applicable (direct-user-request; RQ645 remains primary and owned by Codex)

## Next
- RQ651-RQ653 (P2) are claimable now by any agent; RQ654 (P3) after them.
