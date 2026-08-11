# Cross-Repo Agent Docs Adoption

Date: 2026-08-11
Repo: `ivanjovicic/Trendplus`
Status: bridge document for imported/adapted agent practices

## Purpose

This document records which ideas were adapted from AgentsWatch and MathLearning and where they now live in Trendplus.

It exists because the source repos contain useful agent rules, but Trendplus must keep its own repo-specific contracts for analytics, retail decision support, SQL, exports and deployment.

## Sources reviewed

### AgentsWatch

Useful ideas adapted:

- source-of-truth hierarchy
- prompt token economy
- prompt lint checklist
- run modes
- discovery budgets
- zero-waste execution protocol
- batch review after several prompt-system commits
- run evidence and learning loop

Trendplus-local docs created from those ideas:

- `docs/ai/PROMPT_TOKEN_ECONOMY_AND_LINT.md`
- `docs/ai/PROMPT_BATCH_REVIEW_POLICY.md`

### MathLearning / Mathlearning-Mobile-App

Useful ideas adapted:

- stabilization status snapshot with Done/Partial/Not closed sections
- completion evidence template
- completion percentage guide and score caps
- queue refill discipline only after current prompt is honestly closed
- critical test matrix thinking
- explicit residual risk and follow-up prompt requirements
- authority order for conflicting entrypoint/summary docs
- autonomous execution defaults and a higher question threshold
- direct-user-request guidance that does not invent a queue claim
- mechanical prompt-defect repair inside the same owner boundary
- delivery truth: local/branch transport states are not verified completion

Trendplus-local docs created from those ideas:

- `docs/ai/AGENT_RUN_EVIDENCE_STANDARD.md`
- `docs/ai/REPO_AI_README.md`
- `docs/ai/AGENT_START_HERE.md`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/qa/TRENDPLUS_STABILIZATION_STATUS.md`

## Trendplus read order for agent-system work

For prompt-system, queue, reliability or agent-workflow work, read:

1. `docs/ai/AGENT_START_HERE.md`
2. `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
3. `docs/ai/PROMPT_TOKEN_ECONOMY_AND_LINT.md`
4. `docs/ai/AGENT_RUN_EVIDENCE_STANDARD.md`
5. `docs/ai/PROMPT_BATCH_REVIEW_POLICY.md` only when the batch trigger is met
6. target queue/audit/source files

For analytics reliability implementation, keep using:

1. `docs/ai/ANALYTICS_AGENT_SAFETY_GATE.md`
2. `docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md`
3. `docs/ai/ANALYTICS_WAITING_PROMPTS_EXECUTION_PREP.md` only when the index says so

## What was intentionally not copied

Do not copy these source-repo-specific rules into Trendplus:

- AgentsWatch local CLI gate names, SaaS/dashboard restrictions and CLI-specific validation commands.
- MathLearning mobile/offline/cosmetics feature contracts that do not apply to Trendplus.
- Flutter-specific validation commands unless a Trendplus Flutter/mobile module is added.
- Source repo queue IDs or lane prefixes.
- MathLearning remote-claim/worktree machinery that depends on repository-specific scripts Trendplus does not have.
- Mobile/session/economy/offline invariants that do not map to Trendplus .NET, React, worker or analytics contracts.

## Practical effect for Trendplus agents

A Trendplus prompt should now be smaller and easier to run because it has:

- one run mode
- low/medium/high token budget
- owned paths and avoid paths
- discovery limits
- exact validation or blocked reason
- completion percentage
- missed work
- residual risk
- follow-up prompt
- token waste note

## Maintenance rule

After 3-5 important prompt-system commits, run `docs/ai/PROMPT_BATCH_REVIEW_POLICY.md` before adding more queue/rule docs.

## Note

This bridge remains useful as an audit trail, but the live Trendplus owners now already point at the relevant queue, evidence and source-of-truth rules directly.
