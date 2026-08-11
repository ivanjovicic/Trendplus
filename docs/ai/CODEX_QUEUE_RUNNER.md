# Codex Queue Runner Instructions

Updated: 2026-08-11

Use this guide when Codex is explicitly asked to execute live Trendplus queue work.

## Important

Codex should not try to finish the whole queue in one run. Work one prompt per session and keep the change set scoped to that prompt unless the prompt explicitly allows a bounded docs consolidation.

## Start prompt for Codex

```text
Repo: ivanjovicic/Trendplus

Before work, read:
- .github/copilot-instructions.md
- AGENTS.md
- docs/ai/AGENT_START_HERE.md
- MASTER_ROADMAP.md
- docs/ai/PROMPT_QUEUE_PROTOCOL.md
- the current owner queue named by MASTER_ROADMAP.md

Task:
Execute only the current READY prompt for the owning program.

Rules:
1. Resolve the owner program from MASTER_ROADMAP.md.
2. Start only a prompt that is currently READY and whose dependencies are satisfied.
3. Treat docs/ai/NEXT_PROMPT_QUEUE.md as a historical ledger, not a live router.
4. Do not run more than one queue prompt in the same session/commit.
5. Before edits, set the prompt to IN_PROGRESS or create the local lock from docs/ai/PROMPT_QUEUE_PROTOCOL.md.
6. Make the smallest change that satisfies the prompt acceptance.
7. Run the exact prompt checks; if a check stalls or fails twice without new evidence, stop as PARTIAL/BLOCKED.
8. Record status, changed files, checks, residual risk, next step and main verification evidence.
9. Use only protocol statuses: READY, WAITING, IN_PROGRESS, BLOCKED, PARTIAL, DONE, OBSOLETE.
10. Do not change unrelated files or another program's queue.

Finish with:
- completed prompt
- changed files
- checks
- risks
- main verification
- next owner-queue item if known
```

## When Codex should stop

Stop if:
- the prompt is not the current READY item for its owner program
- source of truth, tenant authority or business contract is unclear
- the task spills into another program or a broad rewrite
- secrets, production access or unresolved security decisions are required
- required proof cannot be produced honestly

## Manual continuation

After each commit:
1. Re-open `MASTER_ROADMAP.md`.
2. Re-check the current owner queue header/current READY pointer.
3. Continue only if the same prompt still owns the next action.
4. Do not jump to an older historical `TODO` or `OPEN` entry.
