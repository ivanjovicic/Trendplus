# Zero-READY routing repair — 2026-10-07

## Context

The synchronized P-UI-52 close-out correctly recorded a zero-READY state for its recovery SHA: P-UI-50 was blocked by an unresolved uncommitted edit on `ProductDecisionCenterPage.tsx`, while P-UI-38 was modeled as the final gate behind all migrations.

That proof remains historically valid for that SHA. It is **not** a permanent routing conclusion.

A follow-up review applied two existing repository rules more aggressively:

1. one blocked path must not serialize unrelated repository-local work when a meaningful path-disjoint slice exists;
2. P-UI-38 already explicitly allowed its stable regression/theme/a11y ratchet to be split when migrations remain blocked.

## Routing repair

- Registered **P-UI-54 READY** as the disjoint child slice of P-UI-38.
- P-UI-54 owns stable theme/Tailwind/a11y/static regression ratchets and explicitly avoids Product Decision page/spec/CSS paths.
- P-UI-50 remains BLOCKED only on its exact Product Decision collision.
- P-UI-38 remains WAITING as final whole-program closure and consumes P-UI-54 after it is DONE.
- Updated `MASTER_ROADMAP.md` and `docs/roadmaps/ANALYTICS_UI_PREMIUM_ROADMAP.md` so the current actionable P-UI lane is no longer `none`.

## Agent-governance repair

The canonical queue protocol now includes a **Mandatory no-READY action ladder**. Before an agent can finish with “no READY” / “nothing claimable”, it must:

1. repair stale dependency/status truth;
2. execute bounded repository-local proof/unblock work when authorized;
3. resolve/narrow path collisions and split a meaningful disjoint child prompt when one path blocks a broader same-owner prompt;
4. create the smallest missing owner work item when a real repo-local gap is otherwise unowned;
5. try the next eligible program;
6. only then emit a zero-READY proof.

A final zero-READY result must include a blocker matrix with the candidate, blocker class, evidence, unblock attempt performed, why no safe split exists and exact unblock event.

Unknown uncommitted workspace edits are fail-safe for that path only: agents must not stash/reset/delete/overwrite them. When visible, the agent records exact paths/diff summary/owner evidence and re-checks them on each recovery. If the edit is gone/delivered/released, the blocker is repaired immediately.

## Instruction surfaces updated

- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/ai/AGENT_START_HERE.md`
- `.github/copilot-instructions.md`
- `.cursor/rules/agent-execution-efficiency.mdc`
- `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md`
- `MASTER_ROADMAP.md`
- `docs/roadmaps/ANALYTICS_UI_PREMIUM_ROADMAP.md`

## Current routing truth

- **P-UI-54: READY** — claimable after a fresh collision check.
- **P-UI-50: BLOCKED** — only while the unresolved Product Decision checkout edit remains active/unresolved.
- **P-UI-38: WAITING** — final gate after P-UI-54 plus P-UI-50 completion/explicit deferral.
- The historical P-UI-52 zero-READY evidence is superseded for current routing by this repair.

No runtime/business-logic code was changed by this routing/governance repair.
