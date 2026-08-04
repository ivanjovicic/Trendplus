# Supplier Sales Stats SQL Verification

Date: 2026-08-04
Related script: `scripts/check_supplier_sales_stats.sql`

This runbook is for manual SQL inspection of the supplier-sales-stats contract. It is diagnostic only and does not replace the API response contract.

## How to use

1. Open `scripts/check_supplier_sales_stats.sql`.
2. Edit the `params` CTE values.
3. Run the script in Neon or `psql`.
4. Review the returned row together with the reason columns:
   - `previous_period_missing_reason`
   - `margin_fake_zero_reason`
   - `pre_post_fake_zero_reason`

## Parameters

- `from_utc` and `to_utc`: the current window to inspect.
- `store_id`: set to `NULL` for all stores.
- `supplier_id`: set to `NULL` to inspect the unknown supplier bucket.
- `data_scope`:
  - `all`
  - `existing`
  - `imported`

If `data_scope` is anything else, the script normalizes it back to `all`.

## Example scenarios

- Normal pilot window:
  - `from_utc = '2026-01-06T00:00:00Z'`
  - `to_utc = '2026-04-04T23:59:59Z'`
  - `store_id = 1`
  - `supplier_id = 42`
  - `data_scope = 'all'`
- Existing-only rows:
  - keep the same window and set `data_scope = 'existing'`
- Imported-only rows:
  - keep the same window and set `data_scope = 'imported'`
- Unknown bucket check:
  - set `supplier_id = NULL`

## EXPLAIN guidance

- Use plain `EXPLAIN` first when you only need the plan shape.
- Use `EXPLAIN (ANALYZE, BUFFERS)` only when you intentionally want runtime evidence.
- Avoid `ANALYZE` on production unless measuring the live plan is the goal.

## Does not verify

- Active snapshot-cost path used by the API endpoint
- Cache metadata and cache-key behavior
- Frontend trust metadata
- HTTP response envelope and status codes
- `dataScope` edge cases that are not explicitly parameterized in the script

## Notes

- The script now includes row-count and fake-zero reason columns so missing evidence is visible instead of looking like a trusted zero.
- A non-null reason should be treated as a diagnostic warning, not a clean pass.
