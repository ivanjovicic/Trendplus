Task ID: nivelacija-analytics-audit-prompts-2026-10-01
Queue: direct-user-request
Date: 2026-10-01
Agent/tool: Grok Bot source audit + ChatGPT/GitHub connector integration
Delivery target: main (direct-main docs commit)
Working branch / PR: `main` direct through GitHub Contents API; no PR
Main commit SHA: 44df3d21555a5809ce1bea0614c47991ff345d8b
Registration/roadmap SHA: 44d0aa617a9aad5a3e52488fde8cb7c0e74dbc66
Main verification: GitHub main contains code stack through `f2c047b45f405d7dcbebbc19fb1cf4a0a199a9f5`, registration/roadmap SHA `44d0aa617a9aad5a3e52488fde8cb7c0e74dbc66`, and governance wiring SHA `44df3d21555a5809ce1bea0614c47991ff345d8b`; audit base `cb3eb7e`, rechecked on current main during integration
Evidence state: synchronized

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
- Integration pass registered all 23 prompts as RQ537–RQ559 without promoting a new READY owner.

## Files changed
- `Api/Endpoints/AllEndpoints.cs`
- `Api.Tests/VendorSalesNivelacijaNullabilityContractTests.cs`
- `Klijent/clientapp/src/utils/chunkLoadRecovery.ts`
- `Klijent/clientapp/src/utils/__tests__/chunkLoadRecovery.spec.ts`
- `Klijent/clientapp/src/pages/NivelacijaRepairPage.tsx`
- `docs/ai/NIVELACIJA_ANALYTICS_AUDIT_PROMPTS_2026-10-01.md` (new)
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_NIVELACIJA_AUDIT_ADDENDUM.md` (new)
- `scripts/check-prompt-queues.mjs`
- `MASTER_ROADMAP.md`
- `.ai/runs/2026-10-01-nivelacija-analytics-audit-prompts-evidence.md` (new)

## Validation run
- Source-audit worktree report: `node scripts/check-agent-instructions.mjs`, `node scripts/check-prompt-queues.mjs`, `node scripts/check-planning-architecture.mjs`, `git diff --cached --check`.
- GitHub Planning Governance run `36852171285` on governance-wiring SHA `44df3d21`: PASS. Agent instructions, prompt queues (including the new nivelacija addendum), planning architecture and analytics execution-plan validation all completed successfully.
- Connector reconciliation: new addendum contains exactly 23 RQ headings and 23 exact `Status: WAITING` entries; current READY ownership was not changed.

## Validation not run
- Local build/test commands were not available through the GitHub connector. Focused frontend/backend regression tests were committed; their push-triggered GitHub workflows were still pending/in progress at this evidence sync and are not claimed green here.
- Live SQL/API/browser verification of hypotheses NV-N09 (scope), N12 (scope), N14, N15, N16, N19 and N20: not run; no database or deploy access. NV-P1 defines the read-only checks.
- The live UI findings come from the parallel audit report; I did not reproduce them. The exact Prioriteti backend error code (L2), the reason the live column is missing (L1) and the L11 trigger were not reproduced.

## Documentation impact
- Added the dated Serbian audit note with English executable prompts, status deltas, event-definition summary, findings and coverage tables.
- Registered RQ537–RQ559 in a dedicated active addendum, wired that addendum into `check-prompt-queues.mjs`, and updated `MASTER_ROADMAP.md` without changing existing READY ownership.

## What was missed
- The intent of the highlight multiplier (NV-N01) is still not confirmed from git history; RQ537 must prove semantics before changing recommendation arithmetic.
- Live PostgreSQL/schema/provider/browser proof was not rerun in this connector integration; RQ545/RQ547/RQ549 and STAB16 retain those evidence boundaries.
- Full backend/frontend CI was not complete at evidence sync and is not represented as passed.

## Risks
- Most source `file:line` references remain pinned to the original audit base `cb3eb7e`; use symbols/sections, not raw line numbers, when executing the registered prompts.
- Q83 has a separately active SQL residual; RQ545 must not duplicate that owner. Live contract/application proof remains next to RQ535/STAB16.

## Next
- Preserve current READY ownership. When collision/dependency recovery permits this addendum, start with proof-heavy RQ547 (NV-P1) and residual RQ545 (NV-F9), then RQ540/RQ548 → RQ537/RQ539, RQ538 and RQ541/RQ550.
- RQ546 is now residual only: the proven preload suppression bug is fixed; route-level retry/error-boundary UX and Vercel/config hardening remain.

## Integration reconciliation 2026-10-01
- Current code stack verified through `f2c047b45f405d7dcbebbc19fb1cf4a0a199a9f5`.
- Direct commits: `e1da86fa` chunk recovery, `87e1982c` regression test, `c9b0ccf4` relation capability, `18bf77af` contract proof, `f2c047b4` repair copy.
- Registered NV-F1..F10 / NV-P1..P4 / NV-I1..I5 / NV-E1..E4 as RQ537..RQ559 without changing current READY ownership.
- Re-read omitted areas: repair service/page, `prePostNivelacijaTrust`, `preNivelacijaDecision`, and 018 stock proxy.
- Repair preflight public-schema parity and the conditional stock/return double-adjust hypothesis are carried into residual proof scope.
- Business-policy arithmetic, migrations, DiD/control definitions and production data were not mutated.
- Local runtime/build/test commands were not available through the GitHub connector; regression tests/contracts were added and remote CI is inspected after delivery.
