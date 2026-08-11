# AGENTS.md Addendum - Prompt Queue Workflow

Use this content only when an older AGENTS variant needs the current Trendplus queue summary patched in. The live owner remains `docs/ai/PROMPT_QUEUE_PROTOCOL.md`.

## Prompt queue workflow

If the task comes from a live queue:

1. Read `MASTER_ROADMAP.md`.
2. Resolve the owner program and current `READY` prompt.
3. Treat `docs/ai/NEXT_PROMPT_QUEUE.md` as a historical ledger, not a live router.
4. Use only protocol statuses: `READY`, `WAITING`, `IN_PROGRESS`, `BLOCKED`, `PARTIAL`, `DONE`, `OBSOLETE`.
5. Work one prompt per session/commit unless the prompt explicitly allows a bounded docs consolidation.
6. Before implementation, set `IN_PROGRESS` or create the local lock from `docs/ai/PROMPT_QUEUE_PROTOCOL.md`.
7. After work, record changed files, checks, remaining risk, next step and main verification.
8. Stop as `PARTIAL` or `BLOCKED` when scope crosses into another program, required proof is unavailable or the same check fails twice without new evidence.

## Final report

```text
Queue task:
- Qxx title

Status:
- DONE/PARTIAL/BLOCKED

Changed:
- ...

Checks:
- ...

Risks:
- ...

Main verification:
- ...

Next:
- Qyy title
```
