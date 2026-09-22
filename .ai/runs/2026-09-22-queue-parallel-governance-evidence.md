# Queue parallel governance evidence

Task ID: QUEUE-PARALLEL-GOVERNANCE
Queue: direct user request / repository governance
Date: 2026-09-22
Agent/tool: ChatGPT GPT-5.6 Sol via GitHub connector
Delivery target: main
Working branch / PR: direct collision-safe main delivery; no PR
Main commit SHA: ecbe35576cabbdfc1aaf16276dd10ee90db7c0e7
Main verification: passed — origin/main is exactly ecbe35576cabbdfc1aaf16276dd10ee90db7c0e7
Evidence state: synchronized

## Interpreted outcome

Replace the accidental repository-wide “one READY per program” serialization rule with a multi-agent-safe model: keep one deterministic primary/default `Current READY` pointer, allow additional independent READY lanes, and reject real feature-family/path/gate collisions.

## What was done

- Updated canonical agent instructions, roadmap/lifecycle guidance, Cursor/Codex execution guidance and live planning queue headers so `Current READY` is a routing pointer rather than a global mutex.
- Clarified that “one claimed prompt” and subagent limits apply per agent/workspace/parent task, not to all independent top-level tools working in the repository.
- Updated prompt-queue and planning validators so multiple READY/IN_PROGRESS tasks are allowed across independent feature families.
- Kept same-family concurrency fail-closed: every active task in the same family must explicitly declare `Parallel-safe: yes`.
- Added validator self-test cases for independent multi-READY lanes and same-family collisions.
- Extended the agent-instruction consistency validator so stale program-wide serialization wording cannot silently return in canonical live instructions.
- Historical run logs/audits were intentionally not rewritten; they remain evidence of the rules that existed when those runs were recorded.

## Files changed

- `AGENTS.md`
- `.github/copilot-instructions.md`
- `MASTER_ROADMAP.md`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/planning/FEATURE_LIFECYCLE.md`
- `docs/ai/AGENT_START_HERE.md`
- `docs/ai/REPO_AI_README.md`
- `docs/ai/AI_WORKFLOW_AND_TOKEN_BUDGET.md`
- `docs/ai/CODEX_TASK_CHECKLIST.md`
- `.cursor/rules/agent-execution-efficiency.mdc`
- `docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md`
- `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md`
- `scripts/check-prompt-queues.mjs`
- `scripts/check-planning-architecture.mjs`
- `scripts/check-agent-instructions.mjs`

## Validation run

- Atomic non-force delivery to `main` succeeded for documentation commit `61fcdc24d32f6f776bb13b5e4cb7b2187755926d`.
- Atomic non-force delivery to `main` succeeded for validator commit `ecbe35576cabbdfc1aaf16276dd10ee90db7c0e7`.
- Fresh GitHub ref/compare verification confirmed that current `origin/main` contains the validator implementation commit.
- GitHub Actions Planning Governance run `35703594509` was created for `ecbe35576cabbdfc1aaf16276dd10ee90db7c0e7`; at evidence-write time its validation job was still queued, so it is not claimed green here.

## Validation not run

- Local Node execution of the three validators was not available through the GitHub connector environment.
- Runtime frontend/backend tests were not run because this change only affects repository governance, queue validation and agent instructions.

## Risks

- The new model intentionally permits more concurrency, so task authors must keep `Feature family`, `Parallel-safe`, dependencies and path ownership accurate.
- A queued remote governance job may still expose an implementation mistake; if it turns red, treat the failing validator output as the next correction target rather than reverting to one-READY serialization.

## Next

Use `Current READY` as the default next candidate, but allow another agent to claim a different READY task when dependency, feature-family, path, owner, lock and release-gate checks are clear.
