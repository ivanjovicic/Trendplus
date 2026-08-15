# Direct agent-governance review evidence

Task ID: DIRECT-AGENTS-GOVERNANCE-20260815
Queue: direct-user-request
Date: 2026-08-15
Agent/tool: ChatGPT GitHub connector
Delivery target: main
Working branch / PR: agent/agents-governance-20260815 / #4
Main commit SHA: 4c9925f109e6bc94c8bbe4ad599722338cb1ef6e
Main verification: passed - fresh GitHub compare reports 4c9925f109e6bc94c8bbe4ad599722338cb1ef6e identical to current main
Evidence state: synchronized

## What was done
- Audited root `AGENTS.md` against the current canonical workflow owners before editing.
- Reworked root guidance to be policy-oriented, narrow-read, autonomous and resistant to duplicated workflow mechanics.
- Removed the live `NEEDS_EVIDENCE_SYNC` status conflict by separating queue status from evidence synchronization.
- Aligned `PROMPT_QUEUE_PROTOCOL.md`, `AGENT_RUN_EVIDENCE_STANDARD.md` and `.ai/RUN_LOG_TEMPLATE.md` on one status vocabulary.
- Found a pre-existing planning-governance mismatch: both the DEX owner queue and `MASTER_ROADMAP.md` explicitly declared no current DEX READY prompt while `check-planning-architecture.mjs` required exactly one.
- Changed the canonical READY invariant to zero-or-one, with zero allowed only when both canonical sources explicitly declare `none`.
- Updated `scripts/check-planning-architecture.mjs` and its self-test so explicit zero-READY is accepted and an undeclared zero still fails.
- Delivered the governance/tooling change through PR #4 and verified the squash merge on current `main`.

## Files changed
- `AGENTS.md`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/ai/AGENT_RUN_EVIDENCE_STANDARD.md`
- `.ai/RUN_LOG_TEMPLATE.md`
- `scripts/check-planning-architecture.mjs`
- `.ai/runs/2026-08-15-direct-agents-governance-evidence.md`

## Validation run
- GitHub connector review of current-main canonical owner files -> pass
- Branch changed-file comparison -> pass - governance/tooling scope only
- GitHub Actions `Planning Governance` run #110 on final implementation head -> pass
- Within that workflow, agent-instruction validation, prompt-queue validation and planning-architecture validation completed successfully.
- Fresh GitHub compare `4c9925f109e6bc94c8bbe4ad599722338cb1ef6e...main` -> identical/pass

## Validation not run
- Local repository Node/build/test commands -> not run - connector-only session without a local repository checkout
- Runtime `.NET`/frontend test suites -> not run - no runtime product code changed; unrelated analytics workflows were not used as completion proof

## Documentation impact
- updated root agent policy and the canonical queue/evidence owners; planning validator behavior was aligned to the same current-ready contract

## What was missed
- none known for the requested Trendplus agent-governance scope

## Risks
- low: this changes agent/planning governance and its validator, not product runtime behavior; historical evidence is intentionally left unchanged

## Next
- none for this direct Trendplus governance task
