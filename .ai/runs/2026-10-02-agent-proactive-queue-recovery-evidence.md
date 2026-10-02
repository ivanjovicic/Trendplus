# Agent proactive queue recovery evidence

Date: 2026-10-02  
Task: strengthen Trendplus agent instructions so agents proactively find/promote safe work before reporting no claimable task  
Delivery target: main  
Queue: direct-user-request / governance instructions

## What was done

- Strengthened `docs/ai/PROMPT_QUEUE_PROTOCOL.md` as the single canonical owner of queue mechanics.
- Added a mandatory blocker-decomposition step before any "no safe task" result.
- Added explicit distinction between:
  - true start gates and final/deployed acceptance evidence;
  - external/provider proof and independently safe repository-local work;
  - stale dependencies and current truth;
  - active ownership and stale metadata;
  - circular prerequisites and artifacts the prompt itself owns creating.
- Added an explicit circular-gate rule: a baseline/fixture/measurement/report produced by the prompt itself must not be required before that prompt can start.
- Allowed same-owner prompt repair/narrowing before claim when a safe repository-local slice can execute without changing business semantics or weakening final acceptance.
- Added the requirement to try another collision-safe lane in the same program and then the next eligible program before refusing work.
- Narrowed stop conditions so they apply to genuine start/safety gates, not merely missing later production/provider proof.
- Added anti-overblocking examples, including the RQ487 precedent: repository-local performance baseline/query-bounds work may proceed while provider/deployed root-cause proof remains under RQ454/STAB16.
- Mirrored the concise behavior rules into:
  - `AGENTS.md`
  - `.github/copilot-instructions.md`
  - `docs/ai/AGENT_START_HERE.md`
  - `docs/ai/CODEX_TASK_CHECKLIST.md`
  - `docs/ai/QUEUE_STATUS_TEMPLATE.md`
  - `docs/ai/AGENTS_QUEUE_ADDENDUM.md`
  - `docs/planning/FEATURE_LIFECYCLE.md`
  - `docs/ai/REPO_AI_README.md`
- Extended `scripts/check-agent-instructions.mjs` so the proactive-recovery markers are required and its self-test proves removal of the blocker-decomposition marker fails validation.

## Behavioral contract after this change

An agent may report no safe claimable task only after it has:

1. refreshed current routing truth;
2. checked unfinished/undelivered active work;
3. revalidated every relevant WAITING/BLOCKED/PARTIAL dependency;
4. classified each blocker;
5. repaired stale or circular routing defects;
6. separated repository-local execution from external final proof when safe;
7. promoted a dependency-complete same-owner task/slice where justified;
8. checked another collision-safe lane in the same program;
9. checked the next eligible program;
10. documented why every remaining candidate truly requires unavailable authority/access/owner resolution.

The change does **not** authorize:
- inventing product-owner decisions;
- bypassing security/tenant/production-write gates;
- weakening analytics correctness;
- marking production/deployed behavior verified without current evidence;
- starting implementation while a prompt is still WAITING/BLOCKED/PARTIAL.

## Validation

Connector-side structural check after edits:
- all newly required proactive-recovery markers are present in the canonical instruction files;
- the agent-instruction validator includes CODEX checklist and queue-status recovery markers.

GitHub Planning Governance:
- run `37030400964` on `dddb615cd17d4dc1b8eadf78402c0d648712d33e`: **failed** because `QUEUE_STATUS_TEMPLATE.md` referenced `NEXT_PROMPT_QUEUE.md` without the exact required "historical ledger" declaration.
- that validator finding was fixed in commit `86c7a6c96f96f911d0210dedfb5e9530d65fd50b`.
- run `37030653386` on `9418382c323c8aa10b2cbf35af305a971c3d4b11`: **success**.
- Successful steps include:
  - agent instruction validator self-test;
  - agent instruction validation;
  - prompt queue validator self-test;
  - prompt queue validation;
  - planning architecture validator self-test;
  - planning architecture validation;
  - analytics execution plan validator self-test;
  - analytics execution plan current-truth validation.

No product/backend/frontend runtime tests were run because this task changes governance/instruction behavior only.

## Files changed

- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `AGENTS.md`
- `docs/ai/AGENT_START_HERE.md`
- `.github/copilot-instructions.md`
- `docs/ai/CODEX_TASK_CHECKLIST.md`
- `docs/ai/QUEUE_STATUS_TEMPLATE.md`
- `docs/ai/AGENTS_QUEUE_ADDENDUM.md`
- `scripts/check-agent-instructions.mjs`
- `docs/planning/FEATURE_LIFECYCLE.md`
- `docs/ai/REPO_AI_README.md`
- this evidence file

## Residual risk

Agents still need judgment to decide whether a repo-local slice is truly independent of an external gate. The new protocol explicitly prevents resolving that ambiguity by guessing: when implementation safety actually depends on owner/security/tenant/production authority, the task remains blocked.

## Next

Use the new protocol on future idle-recovery runs. A bare `Current READY: none`, a stale `Ready after`, or unavailable provider logs are no longer sufficient reasons by themselves to stop.
