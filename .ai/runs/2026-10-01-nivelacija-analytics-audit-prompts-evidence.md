Task ID: nivelacija-analytics-audit-prompts-2026-10-01
Queue: direct-user-request
Date: 2026-10-01
Agent/tool: Grok Bot (executor subagent; read-only box clone of `origin/main`; push from a temporary worktree on the user's machine)
Delivery target: main (direct-main docs commit)
Working branch / PR: detached temporary worktree on `origin/main`, pushed as `HEAD:main` on explicit user instruction; no branch or PR; the user's main checkout was not touched.
Main implementation SHA: f2c047b45f405d7dcbebbc19fb1cf4a0a199a9f5
Main registration SHA: PENDING_REGISTRATION
Main verification: `git ls-remote origin main` after the push (see the executing agent's report); audit base `cb3eb7e`, rechecked on `c51d7a4` (analysis started on `3b7ed53b`; line references recomputed for `cb3eb7e`)
Evidence state: implementation delivered; registration sync pending

## What was done
- Phase A (read-only):
  - Inventoried every nivelacija screen and route: `navConfig.ts:84,93,159,171-174,254`; `analyticsRouteDefinitions.ts:58-59,70-71`; the Pre/Post, Pre-Nivelacija, operational, overview and repair pages.
  - Covered the nivelacija parts of Supplier Asortiman, Supplier/Footwear-type/Color stats (split policy), the products page links and the scorecard markdown dependency.
- Audited in depth:
  - `PreNivelacijaPriorityEndpoints.cs`, `PreNivelacijaScoringService.cs`;
  - `AllEndpoints.cs` vendor-sales-nivelacija (contract check, cohort, mappers, scoped SQL, error/cache paths), `POST /api/nivelacija`, `GET /api/nivelacije`, and the split callers;
  - `AnalyticsNivelacijaSplitPolicy.cs`; `Database/Analytics/014`, `Migrations/013`, `014_NormalizeNivelacijaEvents`, `016`, `029`;
  - `DatabaseInitializer.cs` lifecycle and column checks; `HybridCacheService` behaviour.
- Cross-referenced the 09-30 SA-* audit against the queue (RQ518–RQ536), Q83 and RQ491:
  - SA-F1/F2/F3/F6 and SA-P2/P4 are DONE;
  - only deltas were written (NV-F7 for view/scorecard maturity, NV-F9 for the live contract, NV-I2 for RQ487, NV-E3/NV-E4 for RQ532).
- Traced the parallel live UI audit (L1–L12) to code:
  - Pre/Post fails on `contract_missing`, and the page whitelist masks it as a generic error;
  - Prioriteti gets backend error meta in under 1 s (likely a cached failure);
  - "Nivelacija" actions are 0 because they come only from Products MARKDOWN rows;
  - the "reading 'default'" crash comes from chunk recovery that always calls `preventDefault()` under a 30 s reload cooldown.
- Phase B: wrote 23 prompts (FIX NV-F1–F10, PROVE NV-P1–P4, IMPROVE NV-I1–I5, ENHANCE NV-E1–E4): 10 × P1, 10 × P2, 3 × P3. The doc also has a finding-to-prompt coverage table (NV-N01–NV-N35, L1–L12).
- Not registered in the queue; RQ numbers are assigned at registration.

## Files changed
- `docs/ai/NIVELACIJA_ANALYTICS_AUDIT_PROMPTS_2026-10-01.md` (new)
- `.ai/runs/2026-10-01-nivelacija-analytics-audit-prompts-evidence.md` (new)

## Validation run
- Before the push (results in the executing agent's report): `node scripts/check-agent-instructions.mjs`, `node scripts/check-prompt-queues.mjs`, `node scripts/check-planning-architecture.mjs`, `git diff --cached --check`.

## Validation not run
- Builds and tests: not run; docs-only change.
- Live SQL/API/browser verification of hypotheses NV-N09 (scope), N12 (scope), N14, N15, N16, N19 and N20: not run; no database or deploy access. NV-P1 defines the read-only checks.
- The live UI findings come from the parallel audit report; I did not reproduce them. The exact Prioriteti backend error code (L2), the reason the live column is missing (L1) and the L11 trigger were not reproduced.

## Documentation impact
- New dated Serbian audit note with English prompts, a status delta for the earlier findings, an event-definition summary, a findings table and a coverage table.
- The canonical queue files and `MASTER_ROADMAP.md` were intentionally not changed.

## What was missed
- `NivelacijaRepairService.cs` and `NivelacijaRepairPage.tsx` were only partially read.
- `prePostNivelacijaTrust.ts` and `preNivelacijaDecision.ts` were not re-read.
- The 018 `stock_before_markdown` definition was not traced.
- The intent of the highlight multiplier (NV-N01) was not confirmed from git history.

## Risks
- Line references are pinned to `cb3eb7e`; concurrent agents are actively editing `AllEndpoints.cs`.
- Q83 is DONE; NV-F9 is the code-side live-contract diagnosis next to RQ535/STAB16.

## Next
- Register the prompts. Start with NV-P1 + NV-F9, then NV-F4, NV-P2 → NV-F1/NV-F3, NV-F2 and NV-F5 + NV-P4.
- Start NV-F10 and NV-I4 early: they make the production failures visible and stop the post-deploy crash.

## Integration reconciliation 2026-10-01
- Current code stack verified through `f2c047b45f405d7dcbebbc19fb1cf4a0a199a9f5`.
- Direct commits: `e1da86fa` chunk recovery, `87e1982c` regression test, `c9b0ccf4` relation capability, `18bf77af` contract proof, `f2c047b4` repair copy.
- Registered NV-F1..F10 / NV-P1..P4 / NV-I1..I5 / NV-E1..E4 as RQ537..RQ559 without changing current READY ownership.
- Re-read omitted areas: repair service/page, `prePostNivelacijaTrust`, `preNivelacijaDecision`, and 018 stock proxy.
- Repair preflight public-schema parity and the conditional stock/return double-adjust hypothesis are carried into residual proof scope.
- Business-policy arithmetic, migrations, DiD/control definitions and production data were not mutated.
- Local runtime/build/test commands were not available through the GitHub connector; regression tests/contracts were added and remote CI is inspected after delivery.
