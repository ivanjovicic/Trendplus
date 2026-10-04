# AGENTS.md Addendum - Prompt Queue Workflow

Use this content only when an older AGENTS variant needs the current Trendplus queue summary patched in. The live owner remains `docs/ai/PROMPT_QUEUE_PROTOCOL.md`.

## Prompt queue workflow

If the task comes from a live queue:

1. Read `MASTER_ROADMAP.md`.
2. Resolve the owner program and current `READY` prompt.
3. Treat `docs/ai/NEXT_PROMPT_QUEUE.md` as a historical ledger, not a live router.
4. Use only protocol statuses: `READY`, `WAITING`, `IN_PROGRESS`, `BLOCKED`, `PARTIAL`, `DONE`, `OBSOLETE`.
5. If no READY prompt exists, run canonical Idle recovery and Mandatory blocker decomposition before reporting no work.
6. Treat every older `Current READY: none` / `Next: none` as invalid after any terminal prompt transition, dependency/evidence change or owner decision.
7. After closure reaches `main`, refresh the post-delivery `origin/main` SHA and run the canonical **Post-close dependency cascade** across the entire active owner queue/addendum set; search changed task IDs, re-evaluate all dependents, then all non-terminal prompts.
8. If a dependency-complete collision-safe prompt exists, promote it in the same recovery run. Do not preserve WAITING because older prose called the queue empty.
9. A final `no READY` result requires a durable **Zero-READY proof** with recovery-base SHA, files scanned, candidate/blocker matrix, start-gate-vs-final-proof classification, safe-slice result and exact unblock event. If the full active set was not inspected, report recovery incomplete instead.
10. Treat stale dependencies, circular same-prompt prerequisites and external final-proof requirements differently; repair routing when a same-owner repo-local slice can safely proceed.
11. If the first candidate remains truly blocked, check another collision-safe lane in the same program and then the next eligible program.
12. Work one prompt per session/commit unless the prompt explicitly allows a bounded docs consolidation.
13. Before implementation, promote to `READY`, then set `IN_PROGRESS` or create the local lock from `docs/ai/PROMPT_QUEUE_PROTOCOL.md`.
14. After work, record changed files, checks, remaining risk, post-close routing proof, next step and main verification.
15. Stop as `PARTIAL` or `BLOCKED` only for a genuine unresolved start gate/owner boundary; do not use missing provider/deployed evidence as a blanket blocker when safe repo-local acceptance can be separated.

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
