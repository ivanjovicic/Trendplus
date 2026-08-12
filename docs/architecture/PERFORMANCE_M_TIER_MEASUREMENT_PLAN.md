# Performance M-Tier Baseline Measurement Plan

Status: authoritative PERF04 contract  
Date: 2026-08-12  
Roadmap: `docs/roadmaps/PERFORMANCE_ROADMAP.md`  
Backlog anchor: `PERF-MEASURE-01` in `docs/architecture/PERFORMANCE_MEASURED_OPTIMIZATION_BACKLOG.md`  
S-tier anchor: `.ai/runs/2026-08-11-PERF02-evidence.md`  
Baseline methodology: `docs/architecture/PERFORMANCE_BASELINE_CONTRACT.md`

## Purpose

Define a **pilot-like M-tier** measurement pack so performance claims and optimization prompts are not extrapolated from the 15-product S-tier smoke dataset.

This document is **planning only**. It does not authorize runtime optimization and does **not** invent M-tier timings.

## Why M-tier is required

PERF03 ranked S-tier findings:

| Backlog ID | S-tier conclusion |
|---|---|
| `PERF-COLD-01` | B8 cold-start breaches demo target (p95 55.4 s vs 5 s target) — must re-measure on M-tier before pilot claims |
| `PERF-CACHE-01` | Bootstrap warm-process/cold-cache first hit 1060 ms — re-check at scale |
| `PERF-MONITOR-01` | Warm B1 paths fast on S-tier — **defer** index work until M-tier |
| `PERF-MEASURE-01` | Decision Board, PDC, import, workers, frontend routes have **no** S-tier baseline |

No optimization prompt may cite pilot readiness until the **mandatory M-tier families** below are recorded with evidence files.

---

## M-tier seed recipe (`M-PERF-01`)

Engineering fixture for disposable Postgres DB only (never production customer data).

### Target parameters

| Parameter | M-tier target | Acceptance band (record actual) |
|---|---|---|
| Stores | 5 | 3–10 |
| Suppliers | 8 | 5–15 |
| Products (`Artikli`) | 12,000 | 8,000–50,000 |
| Sales headers | 45,000 | 20,000–120,000 |
| Sales lines | 180,000 | 80,000–500,000 |
| Inventory snapshot rows | 12,000 | matches product count ±10% |
| Analytics actions (ledger) | 500 | 200–2,000 |
| History window | 180 days | 90–180 days ending at seed `asOfUtc` |
| Approximate fact rows | ≤1M | document actual after seed |

### Seed implementation (future execution prompt)

1. **Database:** dedicated name e.g. `trendplus_perf_m` on same Postgres major version as S-tier runs.
2. **Recipe ID:** `M-PERF-01`.
3. **Generation approach (pick one, document in evidence):**
   - **Preferred:** SQL/script generator under `Database/Perf/` (to be added in execution prompt) with deterministic RNG seed `M-PERF-01-2026`.
   - **Acceptable:** scaled extension of `Infrastructure/Seed/TrendplusDbSeeder.cs` with explicit `--tier M` parameters.
   - **Forbidden:** copying production dumps; mixing S-tier and M-tier in one DB without truncate.
4. **Post-seed verification SQL:** record counts for `Artikli`, `ProdajaZaglavlja`, `ProdajaStavke`, `Dobavljaci`, stores (if modeled), `AnalyticsActionItems` (if present).
5. **Period filter:** compute `fromDate` / `toDate` from seed metadata; use the **same** period for all benchmarks in one pack.

### Tier metadata (required on every M-tier run)

Use the template in `PERFORMANCE_BASELINE_CONTRACT.md` with `datasetTier: M` and `seedRecipeId: M-PERF-01`.

---

## Mandatory benchmark families (M-tier)

Record **before** pilot performance claims or promoting backlog items `PERF-COLD-01`, `PERF-CACHE-01`, `PERF-SQL-01` to runtime.

| Priority | Family ID | Surfaces / requests | Backlog link | S-tier already measured? |
|---:|---|---|---|---|
| 1 | **B8** | `GET /api/analytics/cached/dashboard/bootstrap` — cold process, cold cache (N≥5) | `PERF-COLD-01` | Yes (S only) — **repeat on M** |
| 2 | **B2** | `GET /api/analytics/decision-board?dataScope=all` | `PERF-MEASURE-01` | No |
| 3 | **B2** | `GET /api/analytics/cached/products/decision-center` (representative period/store) | `PERF-MEASURE-01` | No |
| 4 | **B2** | `GET /api/analytics/suppliers/decision-hub/ranking` or `/api/analytics/advanced/supplier-scorecard` | `PERF-MEASURE-01` | No |
| 5 | **B1** | `GET /api/analytics/cached/sales/summary` | `PERF-MONITOR-01` | Yes (S) — **repeat on M** |
| 6 | **B1** | `GET /api/analytics/cached/inventory/status` | `PERF-MONITOR-01` | Yes (S) — **repeat on M** |
| 7 | **B3** | Same as B8/B1: warm process + cold cache first hit, then N≥20 warm samples | `PERF-CACHE-01`, `PERF-CACHE-02` | Partial (S bootstrap/sales) |
| 8 | **B4** | `POST /api/access-import/.../preview` with fixed M-tier `.accdb` fixture (or documented dry-run equivalent) | `PERF-MEASURE-01` | No |
| 9 | **B5** | One full `AnalyticsAggregationWorker` cycle OR documented manual refresh job; `GET /api/workers/configuration` for status | `PERF-MEASURE-01` | No |
| 10 | **B7** | Frontend routes (warm API): `/analytics`, `/analytics/decision-board`, `/analytics/products`, `/analytics/inventory` — time to data-ready | `PERF-MEASURE-01` | No |
| 11 | **B6** | Optional in first M pack: large inventory list or supplier export if operator pain expected | defer | No |

### Cold / warm matrix (required cells)

For each API family in the table above, record at least:

| Process | Cache | Required for |
|---|---|---|
| cold | cold | B8, first-run after deploy |
| warm | cold | B3 cache-miss behavior |
| warm | warm | Steady-state p50/p95 (N≥20) |

Document `prewarmEnabled` explicitly. Do not compare runs with different prewarm flags without labeling.

---

## Engineering targets (not measured facts)

Compare M-tier results to **targets** from:

- `docs/ops/ANALYTICS_PERFORMANCE_BUDGETS.md`
- `docs/architecture/PERFORMANCE_BASELINE_CONTRACT.md` (API warm/cold p95 table)
- `docs/qa/DECISION_BOARD_AGGREGATE_PERFORMANCE_BUDGET.md` (Decision Board conservative limits)

Mark each row in evidence as `status=measured` or `status=target_only`. **Never** copy S-tier numbers into M-tier evidence.

---

## Correctness co-assertions (mandatory per sample)

Reuse PERF02 protocol. A latency sample is **invalid** if correctness fails.

### Global gates

1. **No fake zero:** errors must not surface as trusted `0 RSD` KPIs.
2. **Empty ≠ error:** empty success keeps explicit `emptyReason` / DQ status.
3. **Partial/stale visible:** do not treat `isPartial=true` or warning codes as green.
4. **Timeout ≠ healthy:** cancelled SQL must not become silent success.

### Per-family checks

| Family | Minimum assertions |
|---|---|
| B8 bootstrap | `meta.success` true/false honest; if true with partial, `warningCode` present; `summary.totalRevenue` and `inventory.totalSkuCount` consistent with seed totals order-of-magnitude |
| B2 Decision Board | `meta.success`; section/card counts > 0 when seed includes actions; `warningCodes`/`dataQualityStatus` preserved |
| B2 PDC | `meta.success`; product list non-empty; recommendation fields not invented on error |
| B2 supplier | `meta.success` or explicit error; no fake recommendation when `recommendationAllowed=false` |
| B1 sales | `totalRevenue>0`, `totalTransactions>0` on M-tier seed |
| B1 inventory | `totalSkuCount>0`, `totalOnHand>=0` |
| B4 import | batch status terminal state honest; preview row counts > 0 for fixture |
| B5 workers | job completes or explicit failure recorded; no silent skip when workers disabled — document config |
| B7 frontend | route renders error/empty states per contract; no KPI zeros on API error |

---

## Execution protocol (reuse PERF01/02)

1. Checkout target commit; record SHA in evidence.
2. Create/truncate M-tier DB; run `M-PERF-01` seed; snapshot row counts.
3. Start API with documented env (`Workers:Enabled`, prewarm on/off).
4. Run measurements in order: **B8 cold first** (fresh process), then warm paths, then B4/B5, then B7.
5. Write:
   - `.ai/runs/YYYY-MM-DD-PERF05-evidence.md` (or next execution prompt ID)
   - `.ai/runs/YYYY-MM-DD-PERF05-raw.json` with sample arrays
6. Update `PERFORMANCE_MEASURED_OPTIMIZATION_BACKLOG.md` ranks only with measured M-tier numbers.

### Tooling

- Reuse the pattern from S-tier: PowerShell harness `tmp/perf02_measure.ps1` extended for M-tier routes (new script in execution prompt, not PERF04).
- Frontend B7: Playwright or manual timed run with HAR — document method in evidence.

---

## Promotion gates (after M-tier pack)

| Gate | Requires |
|---|---|
| Pilot dashboard latency claim | B8 + B3 on M-tier recorded |
| Decision Board performance claim | B2 decision-board on M-tier |
| PDC / supplier optimization prompt | B2 PDC + supplier on M-tier |
| SQL/index prompt (`PERF-SQL-01`) | M-tier B1/B8 + execution plans |
| Cache tuning prompt | M-tier B3 hit/miss + freshness checks |
| Import/worker tuning | B4 + B5 on M-tier |

Until then, backlog ranks from PERF03 remain **S-tier scoped** for warm B1 deferrals.

---

## Explicit non-goals (PERF04)

- no M-tier timings invented or estimated from S-tier
- no query/index/cache/worker code changes
- no production or pilot DB measurement
- no weakening of partial/stale/error semantics for speed

---

## Acceptance mapping

| PERF04 acceptance | Satisfied by |
|---|---|
| One citeable M-tier measurement plan | This document |
| `PERF-MEASURE-01` executable from plan | Seed recipe + family table + protocol |
| No runtime optimization authorized | Non-goals + promotion gates |
| Plan cites PERF03 backlog IDs | Tables above |
| Cold/warm + correctness explicit | Matrix + co-assertions |
