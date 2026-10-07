# Codex Queue Runner

Updated: 2026-10-07

This file is a **thin compatibility launcher** for Codex queue execution. It is not a queue-policy owner and must not redefine selection, claim, takeover, lock, no-READY recovery, delivery or close-out semantics.

Canonical owner: `docs/ai/PROMPT_QUEUE_PROTOCOL.md`.

## Start packet

Before queue work, read only what is needed:

1. `AGENTS.md`
2. `.github/copilot-instructions.md`
3. `docs/ai/AGENT_START_HERE.md`
4. `MASTER_ROADMAP.md`
5. `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
6. the current owner queue and the selected prompt

Then follow the protocol exactly.

## Non-negotiable launcher invariants

- `Current READY` is the primary/default pointer, not a global mutex. A different READY prompt may be selected only when the canonical protocol proves it dependency-complete and collision-safe.
- One agent/workspace owns one claimed prompt at a time. Different top-level agents may work different collision-safe READY prompts.
- **Same-task ownership is exclusive through close-out.** Another owner's `IN_PROGRESS` task stays theirs through focused validation, evidence/status synchronization, CI classification and post-close routing even when its implementation SHA is already on `main`.
- Missing local lock, branch or PR is not release evidence.
- If no READY prompt exists, run the protocol's **Mandatory no-READY action ladder**. Do not stop at `WAITING/BLOCKED`; attempt stale-gate repair, bounded repo-local unblock/proof work, meaningful disjoint split when permitted, and the next eligible program before a Zero-READY result.
- A queue task starts only after the protocol's current READY/dependency/owner/path checks pass. Do not invent a second claim mechanism here.
- Deliver and close through the protocol/evidence standard. If the user's instruction is to continue queue execution, post-close routing may select the next safe prompt; this file does not impose a one-prompt-per-session rule.

## Suggested invocation

```text
Repo: ivanjovicic/Trendplus

Execute queue work using MASTER_ROADMAP.md + the current owner queue and
docs/ai/PROMPT_QUEUE_PROTOCOL.md as the sole queue-mechanics authority.

Do not take over another owner's IN_PROGRESS task.
If there is no READY prompt, run the Mandatory no-READY action ladder.
Use the smallest collision-safe scope, validate honestly, deliver to main when permitted,
synchronize evidence, then run the post-close dependency cascade.
```

For status names, local-lock format, takeover proof, blocker decomposition, Zero-READY proof and exact close path, read the canonical protocol rather than this launcher.
