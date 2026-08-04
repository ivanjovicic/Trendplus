# Analytics SQL Query Audit

Date: 2026-06-28
Repo: `ivanjovicic/Trendplus`
Status: documentation-only audit

## Scope

Reviewed analytics SQL and query surfaces that influence supplier, markdown/nivelacija, decision-board and manual verification outputs:

- `Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql`
- `Database/Migrations/018_AddSupplierDecisionHubViews.sql`
- `Database/Migrations/029_AddSupplierDecisionWindowedViews.sql`
- `scripts/check_supplier_sales_stats.sql`
- `/api/analytics/supplier-sales-stats` in `Api/Endpoints/AllEndpoints.cs`
- `NightlyAnalyticsRefreshWorker` and `NightlyAnalyticsRefreshOptions`
- `Api.Tests/SupplierDecisionSchemaSqlTests.cs`

This audit intentionally does **not** change production SQL semantics. Several findings need real DB validation, EXPLAIN output, row-count evidence and targeted regression tests before code changes.

## Executive summary

The SQL layer is useful and already has several guardrails: view recreation safety, materialized-view refresh ownership, cache invalidation after nightly refresh, and schema tests. The largest remaining risks are not syntax errors; they are **trust semantics** and **query ownership** risks.

Top risks:

1. `vw_vendor_sales_nivelacija` maps zero-baseline change to bounded numbers: `pre = 0, post > 0` becomes `100`, and `pre = 0, post = 0` becomes `0`. That can hide “new/no baseline” semantics behind a normal percent value.
2. Supplier-decision SQL uses many `COALESCE(..., 0)` fallbacks in scoring inputs. Some are valid derived defaults, but several need explicit “missing evidence” flags so missing cost/post/nivelacija evidence does not look like real zero.
3. The supplier-sales-stats endpoint still performs significant EF/in-memory aggregation after pulling grouped sale rows. It may be fine for pilot ranges, but broad ranges should be proven with query timings and row counts instead of assumed safe.
4. `scripts/check_supplier_sales_stats.sql` is useful for manual verification, but it can drift from the endpoint because the endpoint also has snapshot-cost, cache-key, `dataScope`, season and metadata behavior.
5. The 90d and 180d supplier-decision SQL copies duplicate formula logic. Avoid refactoring first; add parity/regression tests first.

## Findings

### F01 - Nivelacija percent change from zero baseline is ambiguous

File: `Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql`

Current behavior in `vw_vendor_sales_nivelacija`:

- `pre_qty = 0` and `post_qty > 0` returns `100` for `change_percent_qty`.
- `pre_qty = 0` and `post_qty = 0` returns `0`.
- Same pattern exists for revenue.

Risk:

- `100%` can be misread as a normal doubling, not “no comparable baseline”.
- `0%` can be misread as stable, not “no signal”.
- This can leak into decision UX, reports, supplier ranking and markdown analysis as fake certainty.

Recommended follow-up:

- Do not change this blindly.
- First add a string-level/test audit that documents the current contract.
- Then decide whether to:
  - make the existing percent columns nullable when baseline is zero, or
  - keep legacy columns and add explicit semantic columns such as `change_percent_qty_semantic`, `change_percent_revenue_semantic`, `has_qty_baseline`, `has_revenue_baseline`, `change_baseline_reason`.

### F02 - `COALESCE(..., 0)` in supplier-decision SQL needs a no-fake-zero pass

Files:

- `Database/Migrations/018_AddSupplierDecisionHubViews.sql`
- `Database/Migrations/029_AddSupplierDecisionWindowedViews.sql`

Valid uses:

- Aggregating known quantities and revenues where no matching row means no observed sales in that bounded window.
- Defensive denominator handling with `NULLIF`.

Risky uses that need review:

- Cost fallback to `0` when cost is unknown.
- Post-markdown/nivelacija metrics becoming zero when no matching event row is found.
- Rank and score inputs turning unknown metrics into the lowest or a valid score bucket.
- Single-supplier `PERCENT_RANK` behavior returning `1` can overstate confidence unless explicitly documented.

Recommended follow-up:

- Add no-fake-zero tests and a decision table before changing formulas.
- Keep confidence and recommendation logic conservative when evidence is missing.
- Add missing-evidence flags rather than broad formula rewrites.

### F03 - Windowed supplier-decision views duplicate large formula blocks

File: `Database/Migrations/029_AddSupplierDecisionWindowedViews.sql`

The 90d and 180d materialized views duplicate the same scoring formula with different source views.

Risk:

- A future bug fix can land in one window but not the other.
- The all-time, 90d and 180d contracts can drift.

Recommended follow-up:

- Add tests that explicitly compare required fragments and output columns for 90d and 180d.
- Avoid SQL DRY refactor until tests exist and a real database refresh has been validated.

### F04 - Manual verification script is diagnostic, not endpoint contract

File: `scripts/check_supplier_sales_stats.sql`

Risk:

- The script does not fully encode endpoint behavior: active snapshot cost, cache key metadata, `dataScope`, season normalization and frontend trust metadata.
- It is still valuable, but should say which endpoint behaviors it does not verify.

Recommended follow-up:

- Add parameter guidance, EXPLAIN usage, expected result notes and fake-zero checks.
- Add a short “does not verify” section.

### F05 - Supplier-sales-stats endpoint can be expensive for broad ranges

File: `Api/Endpoints/AllEndpoints.cs`

Observed pattern:

- The endpoint loads grouped sale rows and then calculates supplier/type/nivelacija/margin breakdowns in memory.
- That keeps business logic in C# and is easier to reason about, but can become slow with wide date ranges or many products.

Recommended follow-up:

- Add timing evidence and row-count logging review before moving logic into SQL.
- Consider a staged service extraction first, not a big SQL rewrite.
- If DB aggregation is introduced, keep response metadata and no-fake-zero semantics identical.

### F06 - Refresh ownership is mostly healthy, but tests should lock new windowed MVs

Files:

- `Infrastructure/Configuration/NightlyAnalyticsRefreshOptions.cs`
- `Workers/NightlyAnalyticsRefreshWorker.cs`
- `Api.Tests/SupplierDecisionSchemaSqlTests.cs`

Good current state:

- 90d and 180d materialized views are in the default refresh list.
- Worker uses an advisory lock, validates relations, tries concurrent refresh when possible, and clears analytics cache after successful refresh.

Gap:

- Existing schema tests should explicitly assert the 90d/180d entries in refresh configuration and any intended absence of a 30d MV.

## Safe improvements made by this audit

Documentation/queue-only changes are safe because they do not alter runtime behavior:

- Add a dedicated SQL analytics prompt queue.
- Add a queue protocol that separates READY/WAITING tasks and local in-progress locks.
- Split risky SQL work into small prompts so Codex and Cursor do not work on the same feature family.

## Q69 test-backed evidence

`Api.Tests/SupplierDecisionSchemaSqlTests.cs` now locks the current high-risk SQL contracts without changing runtime behavior.

| Test | Locks | Safe to fix next | Needs DB/EXPLAIN evidence |
|---|---|---|---|
| `VendorSalesNivelacijaZeroBaselinePercentContractKeepsExplicitSentinelValues` | `vw_vendor_sales_nivelacija` keeps the current `100` / `0` zero-baseline sentinel behavior and still uses the normal `NULLIF` branch for non-zero baselines. | Add explicit semantic columns or flags in a follow-up prompt if the product contract decides that zero-baseline should be labeled, not rewritten. | Any rewrite that changes the emitted percent values or their nullability. |
| `SupplierDecisionViewsKeepExplicitZeroFallbacksForMissingEvidence` | Supplier-decision views still coalesce missing post-signal and cost inputs to zero in the current SQL contract. | Add missing-evidence flags or narrower nullability tests before touching formulas. | Changing the cost/post-signal fallback semantics or rank inputs. |
| `SupplierDecisionWindowedScoreCachesRepeatTheSameColumnContract` and `SupplierDecisionWindowedMvAudit_Confirms90d180dAndAllTimeContract` | 90d and 180d score caches keep the same output column contract, and the nightly refresh list includes both windowed caches. | Add a readiness/parity prompt before any refactor that would deduplicate the 90d/180d SQL blocks. | Deciding whether startup should own windowed MV creation or only log readiness gaps. |

Second-pass findings from `docs/qa/ANALYTICS_SQL_SECOND_PASS_REVIEW.md` now line up with the same evidence model:

- Safe to fix next: F01, F03, F06, F07, F09, F10.
- Needs DB/EXPLAIN evidence before changing runtime behavior: F05, F08, F12, F13, F14, and any semantic rewrite of F02.

## Q70 compatibility note

`vw_vendor_sales_nivelacija` now exposes additive semantic fields for zero-baseline clarity:

- `has_qty_baseline`
- `qty_baseline_reason`
- `change_percent_qty_semantic`
- `has_revenue_baseline`
- `revenue_baseline_reason`
- `change_percent_revenue_semantic`

The legacy `change_percent_qty` and `change_percent_revenue` columns remain for compatibility. New consumers should prefer the semantic columns when they need to distinguish zero-baseline uplift from an ordinary percent change.

## Q71 test-backed evidence

`Api.Tests/SupplierDecisionSchemaSqlTests.cs` now locks the supplier-decision missing-evidence contract without changing the endpoint surface.

| Test | Locks | Safe to fix next | Needs DB/EXPLAIN evidence |
|---|---|---|---|
| `SupplierDecisionViewsExposeMissingEvidenceFlagsAndConservativeGuardrails` | `vw_supplier_decision_score` keeps explicit post/did/cost coverage signals, a missing-sales reason for `return_rate`, and a `REVIEW_QUALITY` guard when evidence is partial. | Fine to tune the evidence weights or add more explicit nullability fields in a later prompt. | Any change to the conservative guard or confidence weighting. |
| `SupplierDecisionWindowedScoreCachesRepeatTheSameColumnContract` | 90d and 180d caches keep the same evidence fields, nullable `return_rate`, and output contract. | Safe to add more parity asserts before any dedup/refactor work. | Any refactor that changes column names, order, or fallback semantics. |
| `SupplierDecisionWindowedScoreCachesKeepOneSupplierRankGuardAndEvidenceReviewFallback` | The windowed SQL still uses one-supplier rank protection and now falls back to `REVIEW_QUALITY` when evidence is partial. | Safe to extend with more guardrail assertions before larger SQL changes. | Any semantic rewrite of the guard or score weights. |

## Q72 review evidence

`Api/Endpoints/AllEndpoints.cs` now emits row-count telemetry for supplier-sales-stats broad-range review without changing the response shape, cache key behavior or recommendation semantics.

Observed endpoint materialization points:

- optional `Sezone` lookup when `sezonaId` is present
- active snapshot batch lookup
- snapshot cost dictionary load by batch
- `GetSalesDataWindowAsync` helper
- supplier name dictionary
- footwear type dictionary
- previous comparable rows query
- current `stavke` query
- `Sezone` list query

In-memory aggregation points:

- `previousSupplierMetrics` and `previousSupplierFootwearMetrics`
- `stavke.GroupBy(...)` supplier aggregation
- per-supplier footwear breakdown
- `suppliersWithRecommendation`
- totals and `dataQuality` rollups

The final log now includes `SnapshotCostRows`, `PreviousComparableRows`, `SalesRows`, and `SeasonCount`, which gives the next prompt concrete evidence before deciding between docs-only guidance, service extraction, or a DB-side proposal.

Safe next step:

- document or extract the orchestration layer into a smaller service while keeping the exact endpoint contract
- defer any DB-side aggregation proposal until broad-range timings and row counts are observed on real requests

## Q73 runbook evidence

`scripts/check_supplier_sales_stats.sql` now reads like a manual diagnostic instead of a hidden endpoint clone.

Notable additions:

- explicit `data_scope` parameterization for `all`, `existing`, and `imported`
- example scenarios in the script header and the companion QA doc
- row-count and fake-zero reason columns for previous-period, margin, and pre/post checks
- clear "does not verify" guidance for snapshot cost, cache metadata, frontend trust metadata, and HTTP contract behavior

Companion doc:

- `docs/qa/SUPPLIER_SALES_STATS_SQL_VERIFICATION.md`

## Q74 test-backed evidence

`Api.Tests/SupplierDecisionSchemaSqlTests.cs` now locks the supplier-decision refresh and fallback contract without changing runtime code.

| Test | Locks | Safe to fix next | Needs DB/EXPLAIN evidence |
|---|---|---|---|
| `SupplierDecisionWindowedMvAudit_Confirms90d180dAndAllTimeContract` | The windowed supplier-decision contract keeps the 90d/180d refresh targets, has no 30d MV entry, and keeps the 30d/90d/180d/all_time fallback language aligned with the SQL comments. | Safe to add more comment or refresh-list asserts before any startup readiness work. | Any change to the dataset fallback rule or refresh target list. |
| `SupplierDecisionWindowedScoreCachesRepeatTheSameColumnContract` | The 90d and 180d caches still share the same column contract and evidence fields. | Safe to extend parity checks before a dedup refactor. | Any column-shape or fallback-semantic rewrite. |
| `SupplierDecisionWindowedScoreCachesKeepOneSupplierRankGuardAndEvidenceReviewFallback` | The one-supplier rank guard and `REVIEW_QUALITY` fallback remain explicit. | Safe to extend guardrail assertions before a larger rewrite. | Any semantic rewrite of the guard or score weights. |

Safe next step:

- audit whether startup ownership should only validate readiness or also surface a warning for missing refresh targets
- keep any worker or refresh behavior change in a later prompt unless the readiness audit proves a drift

## Do not do in one prompt

Do not combine these in a single Codex/Cursor task:

- Changing `014_CreateVendorSalesNivelacijaViews.sql` percent semantics and supplier-decision scoring.
- SQL formula changes and frontend copy changes.
- Endpoint performance rewrite and materialized-view contract changes.
- Query refactor and production deploy proof.
- Queue reconciliation and app code.

## Recommended queue order

1. Q69 - SQL trust semantic audit and regression tests.
2. Q70 - Nivelacija zero-baseline percent semantics.
3. Q71 - Supplier-decision no-fake-zero/nullability guardrails.
4. Q72 - Supplier-sales-stats query plan and service split review.
5. Q73 - Manual SQL verification script parity/runbook.
6. Q74 - Refresh/window contract tests.

Only Q69 should be READY first. The rest should stay WAITING until Q69 produces exact evidence and acceptance criteria.
