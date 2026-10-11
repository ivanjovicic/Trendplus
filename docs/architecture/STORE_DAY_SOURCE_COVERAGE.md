# Store/day source coverage evidence

RQ611 records what a specific Access import batch actually proves for each
store and Serbian business date. The evidence is appendable by batch and is
written in the same Trendplus PostgreSQL transaction as the imported sales
snapshot.

## Status meaning

- `observed_sales`: a full direct receipt-header and sale-line source scan
  completed, every source row was accepted and persisted once, the store is
  mapped, and at least one receipt exists for this store/date.
- `verified_empty`: the same complete source scan found no receipt for a date
  between that store's first and last observed receipt dates. The evidence does
  not extend before or after that bounded interval.
- `partial`: a store/date has observed rows but the source was incremental,
  synthesized, missing its direct line table, or had a skipped/duplicate row.
- `unverified`: the source row has no store identity, an unmapped store ID, or
  an invalid date. Missing dates outside stored evidence remain missing in the
  RQ602 oracle.
- `failed`: represented by the latest failed/cancelled source attempt in the
  oracle; a failed transaction does not leave successful day evidence behind.

An unknown or unmapped store anywhere in a batch prevents that batch from
certifying empty days. Missing source dates are not converted to zero. DUG and
KOREKCIJA receipts remain part of the source completeness counts; business
sales calculations may exclude them separately. Signed returns remain source
lines and retain their sign in downstream calculations.

## Lineage and correction behavior

`source_batch_id` references the existing `DataImportBatches` registry.
`source_generation` is that exact Access batch. `source_scope_fingerprint` is a
SHA-256 hash of the source locator and system (no path or credential is stored
in the coverage table); `source_checkpoint_hash` hashes the import cursor when
one exists. Business dates preserve Access wall-clock dates and explicitly use
`Europe/Belgrade`.

The coverage table is a read model of future source evidence; it does not
backfill batch #23 or other history. `tools/markdown-pilot-store-day-coverage-oracle.sql`
uses the latest attempt for the same source locator. A later partial, failed or
empty-evidence attempt therefore invalidates the earlier complete status for
the requested window. The helper reports completeness only; it does not
calculate sales, uplift or a recommendation, and leaves the frozen RQ602
cohort unchanged.

Apply the additive Trendplus migration before expecting imports to persist this
evidence. If the schema is absent, the import reports that store/day evidence
was not recorded and continues without certifying any day. Live batch #23
qualification, production migration/application and historical coverage remain
outside this repository-only implementation.
