Task ID: analytics-accuracy-audit-2026-10-05
Queue: direct-user-request (ultra-deep Analytics accuracy audit)
Date: 2026-10-05
Agent/tool: Grok Bot (executor, `/workspace/rh`)
Delivery target: main
Base SHA at start: `4ed7012dfa2ed768f835797db40079e7a8a842bc`
Main commit SHA: pending

## What was done
- Fetched `origin/main` (`4ed7012`, newer than expected `c9c4167`).
- Read AGENTS.md, queue header (Current READY RQ588 IN_PROGRESS — not claimed; audit is direct-user-request).
- Adversarial code/test review across accuracy domains (period, filter, revenue, margin, null, inventory, nivelacija, aggregation, unknown, previous-only, returns, shares, SQL/EF, decimal, FE-derived, DTO, cache, freshness, DQ, oracles, boundary/metamorphic/golden, export, pagination, startup, docs/RQ) plus red-team null→0 pass.
- Fixed two safe deterministic residuals (see below). Added focused regression tests.
- Did **not** steal RQ588 claim; did not invent new RQ for fixed items.

## Files changed (product)
- `Api/Endpoints/CachedAnalyticsEndpoints.cs` — Product Decision alternative scores/reason codes ignore null margin/trend; QuickInsights `BestDayRevenue` nullable.
- `Klijent/clientapp/src/types/analytics.ts` — `bestDayRevenue: number | null`.
- `Api.Tests/ProductDecisionAlternativeScoreNullabilityTests.cs` (new)
- `Api.Tests/QuickInsightsNullBestDayContractTests.cs` (new)

## Validation run
- `dotnet test --filter ProductDecisionAlternativeScoreNullabilityTests|QuickInsightsNullBestDayContractTests` → **4/4 passed**

## Validation not run
- Full API/FE suites; live production probe; metamorphic property suite expansion; golden snapshot regeneration.

## Documentation impact
- `docs/qa/ANALYTICS_ACCURACY_AUDIT_2026-10-05.md` (this audit)
- This evidence file

## Residuals (not fixed here)
- PDC summary `LostSalesEstimate`/`SlowStockCapital` still sum `estimate ?? 0m` across analyzed rows (row-level fields stay nullable). Needs coverage-aware total contract — track as residual, prefer fix over new RQ in a focused follow-up.
- Nivelacija article DTO still projects immature/missing windows to `0` with `HasPre/PostSalesEvidence` flags (intentional dual contract; FE must keep using flags).
- Insight Studio quarantined surfaces (RQ582) retain cosmetic comment mojibake.
- Live Render schema/init truth remains UNPROVEN-RUNTIME (RQ587/STAB16).
- RQ588 IN_PROGRESS on main tip for EF discovery guard — out of scope for this audit claim.

## Risks
- QuickInsights type widen to `decimal?` is backward compatible for ASP.NET JSON; FE already used `fmtRsd(..., "Nije dostupno")`.

## Next
- Owner: optional follow-up to make PDC money summary totals coverage-aware.
