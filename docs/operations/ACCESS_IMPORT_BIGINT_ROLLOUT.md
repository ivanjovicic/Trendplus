# STAB17: sales `integer` to `bigint` rollout

Status: design and isolated-validation plan only. It is not authorization to run a migration, `ALTER`, `setval`, import, backfill, or deploy against production.

The importer now blocks a transaction when `prodaja_zaglavlje.id` or `prodaja_stavke.id` has fewer than 10,000 values before `Int32.MaxValue`. This is a safety stop, not an attempt to repair a sequence. The incident evidence recorded only 4,281 values of headroom, so production stays WAITING for this rollout and its approved window.

## P0 baseline and unsupported master families

The routine path performs a read-only pre-write baseline check. Any historical Access
header, line, return, movement or article without the complete
`(source_namespace, physical source table, source row identity)` is reported as
`exact=0 / ambiguous=0 / unmatched=N` and blocks the transaction. Registration of a
namespace is not certification and no date/receipt/amount heuristic or automatic
backfill is permitted.

`TipObuce`, `Dobavljac`, `Sezona` and store dimensions still have legacy global
integer keys in the current model. A source ID that collides with a non-Access row is
rejected, and a second logical Access namespace is rejected while those rows lack a
namespace-scoped target mapping. This is an intentional fail-closed boundary until
the separately reviewed lineage migration is available; it must not be weakened by
setting `overwriteExisting`.

When Trendplus and Analytics use separate PostgreSQL databases, the supported import
mode is operational-only (`includeAnalytics=false`). The response is persisted with
an explicit `Analytics pending/stale` warning; it never claims cross-database
atomicity or an analytics refresh. A durable outbox/projection worker remains a
separate production dependency.

## Required scope

All of these must be inspected and converted together in a disposable PostgreSQL rehearsal:

- `prodaja_zaglavlje.id` and `prodaja_stavke.id`;
- `prodaja_stavke.id_prodaja` and the `FK_prodaja_stavke_prodaja_zaglavlje_id_prodaja` foreign key;
- fact references: `SalesFacts.SaleId` and `SalesLineFacts.SaleId`, their indexes and import staging tables;
- `StoreDaySalesCoverage`, `DailySalesStats`, views/materialized views, report SQL, analytics SQL and any ETL temporary table that assumes `integer` sale IDs;
- application domain properties, DTOs, EF mappings, bulk-copy `NpgsqlDbType`, test fixtures and independent SQL oracles.

Do not silently cast a source Access ID into a target ID. The target IDs continue to be generated PostgreSQL identifiers; source identity is `(source_namespace, source_table_key, source_row_id)`.

## Rehearsal sequence

1. Restore a disposable snapshot, record table counts and run `tools/access-import-namespace-oracle.sql`.
2. Test an explicit, reviewed migration which changes parents before dependent FKs, recreates every named index/view and does not call `setval` automatically.
3. Run the three sales strategies (direct header, line-derived header, Dnevnik-derived header), including same numeric Access IDs in two namespaces, POS preservation, duplicate lines, invalid FK rejection and an injected analytics failure. Verify rollback leaves both original and analytics tables unchanged.
4. Re-run the same Access fixture after process/worker restart. The second run must add no headers, lines or facts; cursors must advance only after the enclosing transaction commits.
5. Run the namespace oracle before/after each replay plus `EXPLAIN` for normal sales/day and supplier analytics queries. Capture exact migration SHA, PostgreSQL version, timings and result counts.

## Production gate and backout

Production requires an approved maintenance window, verified backup/restore, the exact rehearsal artifact, source-namespace registration and two-person review of all FK/view dependencies. Stop if any legacy Access row has a missing namespace or source-row identity; no automatic backfill is allowed.

Backout for a failed rehearsal or deployment is application rollback followed by restore of the verified pre-migration database snapshot. Do not attempt to recreate the old `integer` uniqueness constraints after a namespaced/bigint import: it can collapse distinct source histories. Record the restore point and rerun the read-only oracle before opening imports.
