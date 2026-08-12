# Performance Cold-Start Investigation Plan (PERF-COLD-01)

Status: authoritative PERF06 contract  
Date: 2026-08-12  
Backlog ID: `PERF-COLD-01`  
Benchmark: `B8` — `GET /api/analytics/cached/dashboard/bootstrap`  
Evidence anchors:

- S-tier: `.ai/runs/2026-08-11-PERF02-evidence.md` (cold p95 **55.4 s**)
- M-tier: `.ai/runs/2026-08-12-PERF05-evidence.md` (cold p95 **6.37 s**)
- Implementation: `Api/Endpoints/CachedAnalyticsEndpoints.cs` (`/dashboard/bootstrap`)

## Purpose

Explain **why S-tier and M-tier cold-start numbers diverge** and define the **profiling targets + proof bar** for a future runtime remediation slice — without authorizing optimization in this prompt.

## Problem statement

| Observation | Implication |
|---|---|
| S-tier cold p95 55.4 s vs M-tier 6.37 s on same host | **Protocol/host state dominates** more than row count alone |
| M-tier cold p95 still **borderline** vs 5 s engineering target | Cold-start work remains a demo risk even at pilot scale |
| PERF02 recorded `isPartial=true` / `ANALYTICS_PARTIAL_DATA` | Section failures/timeouts may inflate or distort cold latency |
| PERF05 harness did not parse response meta | Correctness co-assertions must be re-tied before optimization claims |
| Bootstrap composes **many sequential sections** | Primary hypothesis: cold cost = sum of section cache misses + startup overhead |

**Do not** treat M-tier numbers as proof that cold-start is fixed. **Do not** open index/cache/runtime prompts until section-level evidence exists.

---

## Cold-start variance hypotheses (ranked)

### H1 — Measurement protocol mismatch (high confidence)

| Factor | PERF02 (S-tier) | PERF05 (M-tier) | Effect |
|---|---|---|---|
| API startup | `dotnet run` (compile on first start) | `dotnet run --no-build` after pre-build | M-tier excludes compile/link from some samples |
| Database | `trendplus_test` | `trendplus_perf_m` (clone + bulk seed) | Different cache state; not comparable row scale alone |
| OS / Postgres page cache | cold after prior work unknown | warm after seed + repeated restarts | M-tier likely faster |
| Period filter | 2026-05-13 – 2026-08-11 | 2026-02-13 – 2026-08-12 | Different fact volume in window |
| Harness meta parsing | captured partial flags | not parsed (HTTP 200 only) | S-tier correctness richer |

**Required action before any cross-tier claim:** document identical startup command, build/no-build flag, DB name, prewarm/workers flags, and discard first sample if compile included.

### H2 — Sequential bootstrap section fan-out (high confidence, unprofiled)

The bootstrap factory loads sections **sequentially** inside one outer cache miss (`AnalyticsCacheKeys.DashboardBootstrap`). Each section uses nested `cache.GetOrSetAsync` with its own builder.

On cold process + cold cache, first bootstrap request likely pays:

1. Outer bootstrap cache miss
2. Sequential execution of every section builder below
3. In-memory composition (`DecisionActions`, `Executive`) after PDC/Advanced snapshots

**Expected dominant cost on M-tier:** sales aggregates + daily/category/gender/supplier breakdowns + `BuildProductDecisionCenterAsync(top=300)` + validation trio.

### H3 — Development startup side effects (medium confidence)

On `Development` / `Database:AutoMigrate`:

- EF migrations for `TrendplusDbContext` and `AnalyticsDbContext` at startup (`Api/Program.cs`)
- `AnalyticsConnectionDiagnosticsHostedService` may retry up to 8×15 s when supplier decision objects missing (logs only; does not block HTTP, but competes for DB pool)
- `AnalyticsCachePrewarmHostedService` disabled in PERF05 (`AnalyticsPrewarm:Enabled=false`) — good for cold measurement

**Profiling note:** separate **process ready** (`/health` 200) from **first analytics request**; migrations may fall between them on cold process.

### H4 — Partial section failures / timeouts (medium confidence on S-tier)

`TrySectionAsync` / `TryListSectionAsync` swallow section errors into `response.Errors` and set `isPartial=true`. Timeout/cancel paths return fallback DTO with error meta.

S-tier `ANALYTICS_PARTIAL_DATA` suggests at least one section failed or timed out while others succeeded — total wall time may reflect **timeout waits**, not just successful SQL duration.

### H5 — Nested cache key explosion (lower priority until profiled)

Bootstrap uses per-section cache keys (sales summary, daily sales, category, gender, supplier, weekday, hour, payment, quick insights, transaction stats, advanced, PDC, top advanced, validation*). Cold first hit populates all keys; warm second hit on bootstrap outer key should be ~tens of ms (observed M-tier warm p95 **52 ms**).

**PERF-CACHE-01** is a separate family — do not tune TTLs until B8 section profile identifies safe subset.

---

## Bootstrap sections to profile (M-tier, mandatory order)

Record **wall time per section** on cold outer cache miss using the same period/store/supplier scope as PERF05.

| # | Response field | Builder / source | Cache key family | Profiling priority |
|---:|---|---|---|---|
| 1 | `Summary` | `BuildSalesSummarySnapshotAsync` | `SalesSummary` | P0 |
| 2 | `Inventory` | `BuildInventoryStatusSnapshotAsync` | `Inventory` | P0 |
| 3 | `DailySales` | `BuildDailySalesSnapshotAsync` | `DailySales` | P0 |
| 4 | `CategoryData` | `BuildCategoryDataSnapshotAsync` | `CategoryData` | P1 |
| 5 | `GenderData` | `BuildGenderDataSnapshotAsync` | `GenderData` | P1 |
| 6 | `SupplierData` | `BuildSupplierDataSnapshotAsync` | `SupplierData` | P1 |
| 7 | `SupplierOptions` | `BuildSupplierFilterOptionsAsync` | `SupplierFilters` | P2 |
| 8 | `WeekdayData` | `BuildWeekdayDataSnapshotAsync` | `ByWeekday` | P1 |
| 9 | `HourData` | `BuildHourDataSnapshotAsync` | `ByHour` | P1 |
| 10 | `PaymentData` | `BuildPaymentDataSnapshotAsync` | `ByPayment` | P1 |
| 11 | `QuickInsights` | `BuildQuickInsightsSnapshotAsync` | `QuickInsights` | P1 |
| 12 | `TransactionStats` | `BuildTransactionStatsSnapshotAsync` | `TransactionStats` | P1 |
| 13 | `Advanced` | `BuildAdvancedDashboardSnapshotAsync` | `DashboardAdvanced` | P0 |
| 14 | `ProductDecisionCenter` (internal) | `BuildProductDecisionCenterAsync(top=300)` | `ProductDecisionCenter` | P0 |
| 15 | `DecisionActions` | `BuildDashboardDecisionActions` (CPU) | n/a | P2 |
| 16 | `Executive` | `BuildExecutiveDashboardSnapshot` (CPU) | n/a | P2 |
| 17 | `TopAdvanced` | `GetTopProductsAdvancedSnapshotAsync(10)` | `TopProductsAdvanced` | P1 |
| 18 | `ValidationCompleteness` | `BuildCompletenessValidationAsync` | `ValidationCompleteness` | P2 |
| 19 | `ValidationFreshness` | `BuildFreshnessValidationAsync` | `ValidationFreshness` | P2 |
| 20 | `ValidationLostSales` | `BuildLostSalesValidationAsync` | `ValidationLostSales` | P2 |

**P0** sections must have duration evidence before any runtime optimization prompt is promoted.

### Recommended profiling methods (future execution prompt)

1. **Structured section timing** — temporary `ILogger` scopes with `routeName=dashboard.bootstrap.section` + section id (feature-flagged, default off).
2. **Repeatable harness** — extend `tmp/perf05_measure.ps1` to call individual cached endpoints where they exist, or add internal diagnostics endpoint (investigation only).
3. **Postgres evidence** — `pg_stat_statements` top queries during one cold bootstrap on `trendplus_perf_m`; capture `EXPLAIN (ANALYZE, BUFFERS)` for P0 builders only.
4. **Correlation** — tie `meta.correlationId` from bootstrap response to `PerformanceLog` / request logs.

---

## Standardized cold-start measurement protocol (required for before/after)

Use this protocol for **any** future PERF-COLD-01 remediation proof:

| Step | Rule |
|---|---|
| 1 | Fixed `datasetTier`, DB name, seed recipe, commit SHA |
| 2 | Document `dotnet run` vs `--no-build`, `Workers__Enabled`, `AnalyticsPrewarm__Enabled` |
| 3 | Cold process = new OS process; cold cache = no successful bootstrap for same key space |
| 4 | Discard compile-included first sample when using `dotnet run` without `--no-build` |
| 5 | Measure from **first** `GET /api/analytics/cached/dashboard/bootstrap` after `/health` 200 |
| 6 | N≥5 cold samples; report p50/p95 + error/timeout rate separately |
| 7 | Correctness: `meta.success`, `isPartial`, `warningCode`, `summary.totalRevenue>0`, `inventory.totalSkuCount>0` |
| 8 | Do **not** hide `isPartial` or downgrade warnings to pass latency gates |

### Engineering targets (not measured facts)

| Metric | Target |
|---|---|
| Cold p95 bootstrap | &lt; 5 s (`docs/ops/ANALYTICS_PERFORMANCE_BUDGETS.md`) |
| Warm p95 bootstrap | &lt; 2 s |

---

## Before/after proof requirements (future runtime slice)

A promoted runtime prompt for `PERF-COLD-01` must deliver:

1. **Section profile table** — P0/P1 rows with measured ms and % of total cold bootstrap time on M-tier.
2. **Protocol parity** — same harness flags as baseline run; if protocol changes, re-baseline both sides.
3. **B8 repeat** — cold p50/p95 on `trendplus_perf_m` with M-PERF-01 period; compare to PERF05 `6373 ms` p95.
4. **Warm regression check** — bootstrap warm p95 must stay &lt; 2 s (PERF05: **52 ms**).
5. **Correctness diff** — `isPartial` rate must not decrease by silencing sections; revenue/SKU totals within tolerance on fixed seed.
6. **Rollback** — feature flag or revert path documented; cold samples before/after attached in `.ai/runs/`.
7. **Family separation** — no cache TTL-only change in same prompt as SQL/index change (see backlog family table).

### Explicit non-candidates until profiled

- global index migrations on unmeasured builders
- reducing `isPartial` visibility to improve latency scores
- skipping validation sections in production without product approval
- conflating PERF-CACHE-01 prewarm work into PERF-COLD-01 remediation

---

## Promotion gate

| Gate | Requires |
|---|---|
| Open runtime PERF-COLD-01 remediation | This plan + section timings for P0 rows on M-tier |
| Pilot demo cold-start claim | Standardized protocol + cold p95 &lt; 5 s on M-tier with correctness checks |
| PERF-CACHE-01 | B3 hit/miss on bootstrap outer + P0 sections; separate prompt |

---

## Acceptance mapping (PERF06)

| PERF06 acceptance | Satisfied by |
|---|---|
| Investigation plan with profiling targets | Section table + methods above |
| Variance hypotheses documented | H1–H5 |
| Before/after proof for future runtime slice | Protocol + proof requirements |
| PERF-COLD-01 cited | Header + promotion gate |
| No runtime optimization authorized | Non-candidates + gates |

---

## Non-goals (PERF06)

- no query/index/cache/code changes
- no invented section timings
- no claiming cold-start is resolved
- no weakening partial/error semantics
