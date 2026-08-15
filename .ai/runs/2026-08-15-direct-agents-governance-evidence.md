# Direct agent-governance review evidence

Task ID: DIRECT-AGENTS-GOVERNANCE-20260815
Queue: direct-user-request
Date: 2026-08-15
Agent/tool: ChatGPT GitHub connector
Delivery target: main
Working branch / PR: agent/agents-governance-20260815 / pending
Main commit SHA: pending
Main verification: not run - delivery has not reached main
Evidence state: pending

## What was done
- Audited the root `AGENTS.md` against the canonical workflow owners before editing.
- Reworked root guidance to be policy-oriented, narrow-read, autonomous and resistant to duplicated workflow mechanics.
- Removed the live `NEEDS_EVIDENCE_SYNC` status conflict by separating queue status from evidence synchronization.
- Aligned `PROMPT_QUEUE_PROTOCOL.md`, `AGENT_RUN_EVIDENCE_STANDARD.md` and `.ai/RUN_LOG_TEMPLATE.md` on one status vocabulary.

## Files changed
- `AGENTS.md`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/ai/AGENT_RUN_EVIDENCE_STANDARD.md`
- `.ai/RUN_LOG_TEMPLATE.md`
- `.ai/runs/2026-08-15-direct-agents-governance-evidence.md`

## Validation run
- GitHub connector reads of current `main` canonical owner files -> pass
- Cross-document status/evidence contract review -> pass

## Validation not run
- `node scripts/check-agent-instructions.mjs --self-test` -> not run - connector-only session without repository checkout
- `node scripts/check-agent-instructions.mjs` -> not run - connector-only session without repository checkout
- `node scripts/check-prompt-queues.mjs --self-test` -> not run - connector-only session without repository checkout
- `node scripts/check-prompt-queues.mjs` -> not run - connector-only session without repository checkout
- `node scripts/check-planning-architecture.mjs --self-test` -> not run - connector-only session without repository checkout
- `node scripts/check-planning-architecture.mjs` -> not run - connector-only session without repository checkout

## Documentation impact
- updated the repository root agent policy and the canonical queue/evidence owners listed above

## What was missed
- Local repository governance scripts were unavailable in this connector-only session.
- PR number, delivered SHA and fresh-main verification are pending delivery.

## Risks
- Documentation validators may still reveal a machine-enforced wording/schema dependency not visible from the canonical prose review.

## Next
- Open the focused PR, inspect available GitHub checks once, merge when permitted, then synchronize this evidence with the delivered SHA and fresh-main verification.
