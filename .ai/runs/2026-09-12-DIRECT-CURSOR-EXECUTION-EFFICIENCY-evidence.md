# DIRECT-CURSOR-EXECUTION-EFFICIENCY — Evidence

Date: 2026-09-12
Queue: direct-user-request
Run mode: docs-evidence
Commit SHA: self

## Outcome

Add durable guidance that prevents Cursor Auto/subagent orchestration from consuming unnecessary context/tokens on bounded Trendplus tasks while preserving legitimate audit/verification delegation.

## Evidence reviewed

- `AGENTS.md`
- `.github/copilot-instructions.md`
- `docs/ai/AGENT_START_HERE.md`
- `docs/ai/AI_WORKFLOW_AND_TOKEN_BUDGET.md`
- recent Trendplus run logs showing Explorer subagents used for large finite audits
- current Cursor documentation for Rules and Subagents
- repository check confirmed no pre-existing `.cursor` project rules/agents before this change

## Changes

- Added `.cursor/rules/agent-execution-efficiency.mdc` as an always-applied Cursor project rule.
- Updated `docs/ai/AI_WORKFLOW_AND_TOKEN_BUDGET.md` with direct-by-default execution, a bounded delegation gate, one-subagent default maximum, no nested/duplicate research, Auto/model fallback guidance, progress visibility and delegation evidence fields.
- Kept legitimate finite audit use available; the policy targets routine implementation/search/test delegation and repeated `Waiting for subagent` cycles.

## Key policy

- Main agent owns bounded implementation by default.
- Routine search/read/shell/queue/test/docs work stays direct.
- One subagent maximum by default; multiple only for explicitly parallel non-overlapping workstreams.
- Subagent work counts against the same task budget and does not reset context/time.
- Auto/model changes do not justify rereading already proven context.
- If Auto repeatedly enters `Waiting for subagent` or model churn, continue with one selected coding model and the same bounded execution packet.

## Validation

- Repository instruction surfaces and historical subagent evidence were inspected through the GitHub connector.
- Cursor rule format was checked against current Cursor Rules documentation (`.cursor/rules/*.mdc`, YAML frontmatter, `alwaysApply: true`).
- No local Node/.NET checks were run because this change was performed connector-only.
- Runtime/product tests: not run — docs/rules-only change.

Documentation impact: updated `docs/ai/AI_WORKFLOW_AND_TOKEN_BUDGET.md`; added `.cursor/rules/agent-execution-efficiency.mdc` and this evidence log.

## Delivery

- Cursor rule commit: `3492aef9165432c7e9c4702950ead4031ee66630`
- Budget policy commit: `fcf2a390de905b99cac1029ca457fba74ddaaf42`
- Evidence commit: self
- Target: `main`

## Residual risk

Cursor can still make internal orchestration decisions, but the repo now contains an always-applied project rule plus canonical budget guidance that makes unnecessary delegation and duplicated research explicitly out of policy.
