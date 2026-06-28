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
