Task ID: products-supplier-audit-prompts-2026-09-25
Queue: direct-user-request
Date: 2026-09-25
Agent/tool: Grok Bot (executor subagent, local machine via Shell/Read; no cloud agent)
Delivery target: main (direct-main docs commit, local only)
Working branch / PR: `main`; no branch or PR; not pushed (user instruction: commit locally only)
Main commit SHA: pending
Main verification: not run - the commit is local only and was not pushed by user instruction; audit base `d8d3771f`
Evidence state: pending

## What was done
- Read-only code audit of `/analytics/products` (Product Decision Center) and `/analytics/supplier` (Supplier consolidated page: overview, scorecard, assortment) from page to API client, endpoint, builder/SQL, decision engine and cache policy.
- Merged 33 code findings (C1-C33) with 10 live UI findings (L1-L10) from a separate browser audit of https://trendplus.vercel.app and traced the live symptoms to code: L1 confirmed (1,200 status items > 1,000-item cap in `AnalyticsActionsEndpoints.cs:235-236`); L5 (503 branches at `AllEndpoints.cs:2035-2070`), L6 (`MISSING_SCHEMA` for the 90d decision-score MV), L7 (missing `change_percent_revenue_semantic` view column) and L10 (duplicate store labels) are recorded as hypotheses.
- Deduplicated against the canonical RQ queue: `RQ200` (DONE) is regressed by C3; `RQ373`/`RQ233` (DONE) bound the supplier denominator prompt; related open items `RQ128`, `RQ140`, `RQ143`, `RQ146`, `RQ148`, `RQ325`, `RQ439`, `RQ440` referenced; `RQ441`/`RQ442` noted as uncommitted work of another agent.
- Wrote 18 self-contained fix prompts `PS01`-`PS18` (8 × P1, 7 × P2, 3 × P3) in queue-section format inside the Serbian audit note `docs/ai/PRODUCTS_SUPPLIER_AUDIT_PROMPTS_2026-09-25.md`.

## Files changed
- `docs/ai/PRODUCTS_SUPPLIER_AUDIT_PROMPTS_2026-09-25.md` (new)
- `.ai/runs/2026-09-25-products-supplier-audit-prompts-evidence.md` (new)

## Validation run
- `node scripts/check-agent-instructions.mjs` -> pass (12 canonical files)
- `node scripts/check-prompt-queues.mjs` -> pass (563 tasks; includes another agent's uncommitted queue edits in the working tree)
- `node scripts/check-planning-architecture.mjs` -> pass (78 planning tasks)
- `git diff --cached --check` on the two staged files -> pass
- Byte check: the audit note is UTF-8 without BOM, LF in the index (repository `* text=auto`)

## Validation not run
- Frontend/backend builds and tests -> not run; docs-only change and the audit was read-only.
- Live browser/API/database verification of the hypotheses (L5, L6, L7, L10, `NabavnaCena` currency, `DatumProdaje` storage basis) -> not run; no database, API log or deploy metadata access.
- `--self-test` modes of the check scripts -> not run; they create and delete temporary fixture roots and are not needed for a docs-only change.
- Remote CI -> not applicable; nothing was pushed.

## Documentation impact
- New dated Serbian audit note with 18 ready-to-paste prompts and a finding-to-prompt map.
- Canonical RQ queue and `MASTER_ROADMAP.md` intentionally not changed: at the time of writing both had uncommitted edits by a concurrent agent (`RQ441`, `RQ442`), so staging them would have committed foreign work.

## What was missed
- Prompts are not registered as RQ items in `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`; the precedent (`fb956bac`) does register them. Registration is deferred until the queue file has no foreign uncommitted edits.
- `SupplierDecisionHubPage.tsx` and `SupplierFootwearAnalyticsPage.tsx` were only checked for the live L6/L7 paths, not audited in depth.
- No runtime fix was implemented.

## Risks
- Line numbers refer to `d8d3771f`; concurrent agents are committing on `main`, so implementers must re-verify.
- C2 (duplicate header block) is unconditional in code on `origin/main` since `b5280aaa`, but the live tester did not report it; verify the deployed DOM before fixing.
- `PS02`, `PS04`, `PS07` and `PS13` need owner/business decisions before implementation.
- `RQ441`/`RQ442` references depend on another agent's uncommitted queue edits and may be renumbered.

## Next
- Register `PS01`-`PS18` as the next free RQ numbers in the canonical queue (sections + status rows, no changes to existing items) once the queue file is clean.
- Start with `PS01` and `PS05` (independent P1 frontend fixes), `PS06` and `PS08` diagnosis; collect owner decisions for `PS02`, `PS07`.
- After delivery to `origin/main`, synchronize `Main commit SHA`, `Main verification` and `Evidence state` in this run log.
