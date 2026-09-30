Task ID: supplier-analytics-deep-audit-prompts-2026-09-30
Queue: direct-user-request
Date: 2026-09-30
Agent/tool: Grok Bot (executor subagent; read-only GitHub raw/API plus a box clone of `origin/main`; the user machine was offline)
Delivery target: main (direct-main docs commit)
Working branch / PR: detached temporary worktree on `origin/main`, pushed as `HEAD:main` on explicit user instruction ("push na main"); no branch or PR; the user's main checkout was not touched.
Main commit SHA: pending
Main verification: `git ls-remote origin main` after push (see the executing agent's report); audit base `278d37b93356aa7f2011ae5d02bf7e7cf1f801ed` (supplier files identical on `8b4e1fc1`)
Evidence state: pending

## What was done
- Phase A (read-only):
  - Checked the fix status of the earlier supplier findings (C5-C7, C15-C22, C24, C27-C33, L5-L10) and of PS06/07/08/11/12/13/14/15/17/18.
  - Confirmed the 09-25 prompts were registered on 2026-09-28 (`RQ469`-`RQ476`, `RQ483`-`RQ488`).
- Deep audit of the three Supplier tabs, the shell, services and backend:
  - `AllEndpoints.cs` supplier-sales-stats and vendor-sales-nivelacija;
  - `SupplierDecisionHubEndpoints.cs`;
  - migrations 013/014/016/018/029 and Analytics 013/014/015;
  - `DatabaseInitializer.cs` startup ordering;
  - the recommendation engine and policies.
- Traced the 2026-09-30 live UI findings to code:
  - scorecard `MISSING_SCHEMA`: `information_schema.columns` cannot see materialized views (high confidence);
  - Asortiman "contract missing": the 014 Fix/Analytics startup order with CASCADE (hypothesis);
  - overview 503: query cost (hypothesis; `RQ474`/`RQ487`).
- Phase B:
  - wrote 17 prompts grouped FIX / PROVE / IMPROVE / ENHANCE (`SA-F1`-`SA-F7`, `SA-P1`-`SA-P5`, `SA-I1`-`SA-I2`, `SA-E1`-`SA-E3`; 6 × P1, 9 × P2, 2 × P3), plus deltas for the open `RQ474`/`RQ475`/`RQ487`, 38 new findings (N01-N38) and a coverage table;
  - corrected the outdated `RQ441`/`RQ442` "uncommitted" notes and the queue-registration note in the 09-25 audit doc.
- Not registered in the queue; RQ numbers are assigned at registration. A concurrent Daily Sales prompt took the next free RQ number while this audit was written, so the prompts use neutral IDs `SA-F*`/`SA-P*`/`SA-I*`/`SA-E*`.

## Files changed
- `docs/ai/SUPPLIER_ANALYTICS_DEEP_AUDIT_PROMPTS_2026-09-30.md` (new)
- `.ai/runs/2026-09-30-supplier-analytics-deep-audit-prompts-evidence.md` (new)
- `docs/ai/PRODUCTS_SUPPLIER_AUDIT_PROMPTS_2026-09-25.md` (note corrections)

## Validation run
- Run before the push (results in the executing agent's report): validator results (`check-agent-instructions`, `check-prompt-queues`, `check-planning-architecture`) and `git diff --cached --check`.

## Validation not run
- Builds and tests: not run; docs-only change.
- Live SQL/API/browser verification of the hypotheses N02, N13, N18, N20, N25, N26, N27 and the L5 cause: not run; no database or deploy access. `SA-P1` defines the read-only checks.
- Local `git status`/ahead-behind on the user machine: not run; the machine was offline for the whole task.

## Documentation impact
- New dated Serbian audit note with English prompts, a live-to-code map, the old-finding status and a coverage table.
- The canonical queue and `MASTER_ROADMAP.md` were intentionally not changed (a concurrent agent had uncommitted queue/roadmap work); registration is a follow-up.

## What was missed
- `CachedAnalyticsEndpoints.cs` (PDC) was not re-audited; out of scope (supplier only).
- The frontend hub components (`components/supplierDecisionHub/*`) were only partly read; the hub page's summary-error rendering is left as a spec requirement in `SA-F5`/`SA-I1` rather than asserted.

## Risks
- The line references are pinned to `278d37b9`, which is identical for the audited files on `8b4e1fc1`; later edits may shift them.
- Until registered, the prompts are not claimable through the queue; the registering agent must pick the next free RQ numbers.

## Next
- Register the prompts in the canonical queue when it is clean; start with `SA-F1` + `SA-P2`, then `SA-F2` and `SA-P1`.
