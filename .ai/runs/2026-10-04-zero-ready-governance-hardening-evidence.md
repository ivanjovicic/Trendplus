# Zero-READY governance hardening — 2026-10-04

Task ID: direct-user-request / zero-ready-governance-hardening
Queue: direct-user-request
Date: 2026-10-04
Agent/tool: ChatGPT / GitHub connector
Delivery target: main
Working branch / PR: direct-main / none
Main commit SHA: 86752a5e8eb0f99c7e8d27851aa97e7c58daf01e
Main verification: GitHub current-main commit search returned 86752a5e8eb0f99c7e8d27851aa97e7c58daf01e as the latest governance commit before this evidence write; all earlier hardening commits are ancestors in the direct-main sequence.
Evidence state: synchronized

## What was done

- Made a zero-READY conclusion a positive claim that must be recomputed from current post-delivery `origin/main`, never inherited from an older queue header, run log, completion note or previous agent.
- Added the mandatory `Post-close dependency cascade`: after every terminal queue transition or dependency/evidence change, agents must search the full active owner queue/addendum set for dependents of changed task IDs, re-evaluate those dependents, then scan all non-terminal prompts for stale/circular/external-only blockers.
- Declared explicit invalidation triggers for an older `none`: DONE/PARTIAL/BLOCKED/OBSOLETE transition, dependency/evidence update, new prompt/oracle/baseline/owner decision, main delivery, or blocker reclassification.
- Required agents to promote any newly dependency-complete collision-safe prompt in the same recovery run instead of leaving it WAITING behind stale prose.
- Defined the durable `Zero-READY proof`: post-delivery recovery-base SHA, full active queue/addendum files scanned, plausible candidates checked, blocker class, start-gate-vs-final-proof classification, safe repo-local slice result and exact unblock event.
- Prohibited shortcuts such as accepting `Current READY: none`, old `Next: none`, the previous agent's conclusion, one blocked P0, pending CI, or missing deployed/final proof as sufficient evidence.
- Required agents that cannot inspect the full active queue/addendum set to report recovery as incomplete rather than claim that no READY work exists.
- Propagated the rule through root AGENTS, agent entrypoint, Copilot instructions, Codex checklist, queue status template, evidence standard, run-log template, feature lifecycle and the legacy queue addendum.
- Extended `check-agent-instructions.mjs` so canonical files must retain the new `Post-close dependency cascade`, `Zero-READY proof` and post-delivery markers.
- Added a validator self-test that deliberately removes `Zero-READY proof` and requires the governance validator to reject the modified instruction set.
- Recorded the cross-program governance change in `MASTER_ROADMAP.md`.

## Files changed

- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `AGENTS.md`
- `docs/ai/AGENT_START_HERE.md`
- `.github/copilot-instructions.md`
- `docs/ai/CODEX_TASK_CHECKLIST.md`
- `docs/ai/QUEUE_STATUS_TEMPLATE.md`
- `docs/ai/AGENT_RUN_EVIDENCE_STANDARD.md`
- `.ai/RUN_LOG_TEMPLATE.md`
- `docs/planning/FEATURE_LIFECYCLE.md`
- `docs/ai/AGENTS_QUEUE_ADDENDUM.md`
- `scripts/check-agent-instructions.mjs`
- `MASTER_ROADMAP.md`
- `.ai/runs/2026-10-04-zero-ready-governance-hardening-evidence.md`

## Validation run

- GitHub connector re-fetched all modified canonical instruction/governance files from current main.
- Static marker verification passed for every modified canonical file: `Post-close dependency cascade` or `Post-close routing recovery`, `Zero-READY proof`, and post-delivery semantics are present where required.
- Latest main lookup before evidence write returned governance commit `86752a5e8eb0f99c7e8d27851aa97e7c58daf01e`.
- GitHub workflow lookup for `86752a5e8eb0f99c7e8d27851aa97e7c58daf01e` returned no workflow runs.

## Validation not run

- `node scripts/check-agent-instructions.mjs --self-test` — not run; this was a GitHub-connector-only session without a local checkout/runtime.
- `node scripts/check-agent-instructions.mjs` — not run for the same reason.
- `node scripts/check-prompt-queues.mjs` and planning validator — not run locally; runtime queue state itself was not changed by this task.
- Product builds/tests — not run; product runtime code was not changed.

## Documentation impact

- Queue governance now treats `none` as an expiring conclusion, not persistent state.
- Completion evidence and the run-log template now require post-close routing recovery for formal queue tasks.
- Cross-program roadmap records the same hard rule and points back to the canonical queue protocol.

## What was missed

- No dynamic parser was added to infer arbitrary dependency completeness automatically from prose; that would be brittle and could invent readiness. The enforcement is instead procedural + durable evidence + canonical instruction validator markers.
- Local execution of governance scripts remains for the next agent with a repository checkout.

## Risks

- An agent can still violate instructions deliberately, but it can no longer do so while following the canonical queue/evidence templates: a zero-READY conclusion now requires explicit current-main evidence, and future removal of the guard text is covered by the instruction validator/self-test contract.

## Post-close routing recovery

- not applicable — this was a direct user-requested governance hardening task, not execution/closure of a formal queue prompt.
- Current analytics routing remains owned by the already-updated Operations/Nivelacija queues; this task did not alter RQ564/RQ553/RQ569 status.

## Next

- Formal queue agents must use the new post-close cascade immediately on their next closure.
- The next local-capable agent that touches governance should run `check-agent-instructions.mjs --self-test`, `check-agent-instructions.mjs`, prompt-queue validation and planning validation before claiming governance closure.
