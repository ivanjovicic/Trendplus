# AGENTS.md Addendum - Prompt Queue Workflow

Use this content only when an older AGENTS variant needs the current Trendplus queue summary patched in. The live owner remains `docs/ai/PROMPT_QUEUE_PROTOCOL.md`.

## Prompt queue workflow

If the task comes from a live queue:

1. Read `MASTER_ROADMAP.md`.
2. Resolve the owner program and current `READY` prompt.
3. Treat `docs/ai/NEXT_PROMPT_QUEUE.md` as a historical ledger, not a live router.
4. Use only protocol statuses: `READY`, `WAITING`, `IN_PROGRESS`, `BLOCKED`, `PARTIAL`, `DONE`, `OBSOLETE`.
5. If no READY prompt exists, run canonical Idle recovery and Mandatory blocker decomposition before reporting no work.
6. Treat stale dependencies, circular same-prompt prerequisites and external final-proof requirements differently; repair routing when a same-owner repo-local slice can safely proceed.
7. If the first candidate remains truly blocked, check another collision-safe lane in the same program and then the next eligible program.
8. Work one prompt per session/commit unless the prompt explicitly allows a bounded docs consolidation.
9. Before implementation, promote to `READY`, then set `IN_PROGRESS` or create the local lock from `docs/ai/PROMPT_QUEUE_PROTOCOL.md`.
10. After work, record changed files, checks, remaining risk, next step and main verification.
11. Stop as `PARTIAL` or `BLOCKED` only for a genuine unresolved start gate/owner boundary; do not use missing provider/deployed evidence as a blanket blocker when safe repo-local acceptance can be separated.

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
