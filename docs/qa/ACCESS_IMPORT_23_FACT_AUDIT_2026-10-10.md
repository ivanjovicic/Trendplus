# Access import #23 — analytical fact audit

Date: 2026-10-10
Scope: local Docker PostgreSQL `trendplus-postgres`, database `trendplus`
Mode: read-only; no import was started and no database, schema, cache or materialized view was changed.

## Verdict

Access batch `#23` is analytically complete and numerically exact at the source-line boundary. All 5,530 Access receipts have a `SalesFacts` row; all 67,092 Access sale lines have a `SalesLineFacts` row on `(SaleId, SourceLineId)`; quantity, unit price and line total all reconcile with zero delta.

The material discrepancy is not an Access import overcount. It is a legacy `existing`/DEMO population already present in analytical facts and in `AnalyticsDailySummary`. That population is harmless to the current canonical `/api/analytics/daily-sales` path, but it is visible to legacy raw-fact/cache paths that do not apply the operational receipt population or `dataScope`. This is a real code remediation item, not a reason to delete or rewrite rows in this phase.

## Batch and Access population

Batch `#23` is `completed`:

| field | value |
|---|---:|
| queued / started / completed | 2026-10-10 16:30:32 / 16:30:34 / 16:32:16 UTC |
| last heartbeat | 2026-10-10 16:32:16 UTC |
| rows read / accepted / written | 206,092 / 205,436 / 243,253 |
| inserted / updated / rejected | 237,886 / 5,367 / 0 |
| cancellation requested / retries | false / 0 |

The batch contains 5,530 operational receipt headers and 67,092 operational sale lines. Access `SalesFacts` totals are 5,530 receipts, 67,664 units and 240,603,523.64 RSD.

## Identity and amount proof

The RQ604 identity contract is satisfied for Access by `(SaleId, SourceLineId)`. The source-table namespace is intentionally different: operational lines use `prodajastavke`, while Access facts use `trendplus.prodaja_stavke`. Matching on `SaleId + SourceLineId` is therefore required; treating the namespace difference as a missing identity would be a false positive.

| check | result |
|---|---:|
| Access headers without `SalesFacts` | 0 / 5,530 |
| Access lines without `SalesLineFacts` | 0 / 67,092 |
| matched quantity deltas | 0 |
| matched unit-price deltas | 0 |
| matched line-total deltas | 0.00 RSD |
| operational Access line total | 240,603,523.64 RSD |
| `SalesLineFacts` Access line total | 240,603,523.64 RSD |

`tools/access-import-23-fact-audit.sql` preserves the repeatable SQL proof, including the row-level disputed-record queries.

## Difference classification

### 109 unmatched `SalesFacts`

All 109 rows are `DataOrigin=existing`, with 480 units and 2,676,700.00 RSD. They have no operational receipt and use the synthetic `DEMO-001` through `DEMO-005` receipt markers. They are dated 2026-02-28 through 2026-03-14, all at store 1, and are not part of Access batch #23.

| day | receipts | units | amount |
|---|---:|---:|---:|
| 2026-02-28 | 14 | 56 | 287,000.00 |
| 2026-03-01 | 22 | 87 | 502,500.00 |
| 2026-03-02 | 38 | 183 | 1,029,000.00 |
| 2026-03-10 | 4 | 16 | 82,000.00 |
| 2026-03-11 | 6 | 20 | 119,000.00 |
| 2026-03-12 | 17 | 82 | 449,900.00 |
| 2026-03-13 | 5 | 21 | 124,700.00 |
| 2026-03-14 | 3 | 15 | 82,600.00 |

### 286 `SalesLineFacts` without source identity

* 283 rows / 109 sale IDs / 2,676,700.00 RSD are the same DEMO `existing` population above.
* 3 rows / one receipt / 20,500.00 RSD belong to the RQ407 `RQ407-NIV-POST` test fixture (`existing`, 2026-08-05). They intentionally have no source identity and must not be backfilled from unrelated operational lines.

The four operational receipts without a `SalesFacts` row are all RQ407 fixtures: `RQ407-101`, `RQ407-101R`, `RQ407-103` and `RQ407-NIV-PRE`. Their five operational lines total 750.00 RSD including the signed -100.00 RSD return. `RQ407-NIV-POST` has three identity-less fixture facts, but their values are deliberately independent from the current operational line. No Access #23 line is missing.

### Dimension orphans

* 19 `ProductsDim` rows have no `Artikli` row. All are `existing`; four are explicitly archived articles (`ProductId` 2, 8, 9, 10) and 15 are demo products (16–30). Only the four archived products participate in existing facts: 110 fact lines, 198 units, 1,168,200.00 RSD. The 15 demo dimension rows have zero fact lines.
* 40 `SuppliersDim` rows have no `Dobavljaci` row. Two are existing archived/default suppliers; 38 are `import-fix` unmapped-vendor rows. The only fact impact is the existing archived supplier: 286 lines, 484 units, 2,697,200.00 RSD. The 38 `import-fix` rows have no fact lines.

These are provenance/data-quality findings, not proven stale business data. No delete, update, backfill or import-fix was authorized or executed.

## Independent retail reconciliation and impact

The independent retail oracle is `prodaja_zaglavlje` + `prodaja_stavke`, with `DUG` and `KOREKCIJA` excluded by the canonical `SalesReceiptPopulationPolicy`. Quantities and amounts remain signed; returns are not converted to absolute values.

| source / scope | receipts | units | amount |
|---|---:|---:|---:|
| raw `SalesFacts` / imported | 5,530 | 67,664 | 240,603,523.64 |
| raw `SalesFacts` / existing | 110 | 484 | 2,697,200.00 |
| raw `SalesFacts` / all | 5,640 | 68,148 | 243,300,723.64 |
| canonical retail / imported | 5,226 | 66,520 | 234,829,573.64 |
| canonical retail / existing | 5 | 8 | 750.00 |
| canonical retail / all | 5,231 | 66,528 | 234,830,323.64 |

For Access #23, 304 receipts and 859 lines are excluded (`DUG`/`KOREKCIJA`), totalling 1,144 units and 5,773,950.00 RSD. The included Access retail population is 5,226 receipts, 66,233 lines, 66,520 units and 234,829,573.64 RSD. No Access line has a negative quantity; the RQ407 signed return remains -100.00 RSD in the independent oracle.

The 2,676,700.00 RSD legacy population is therefore not proven Access overcount. It is an `existing` fact population that changes outputs only on consumers that read raw facts or stale aggregates without the canonical operational population. For daily/store impact it is concentrated at store 1 on the eight dates above; imported scope has zero unmatched amount.

## Cache, view and fallback audit

* The current `DailySalesStatsService` and `/api/analytics/daily-sales` path uses operational headers/lines, canonical DUG/KOREKCIJA exclusion and header-based `dataScope`; Access #23 is exact there.
* Legacy `/api/analytics/cached/sales/daily` first reads `AnalyticsDailySummary` for the no-filter path. The local summary has 38 rows from 2026-02-23 through 2026-04-20 and 2,697,200.00 RSD, exactly the legacy existing-fact population. Its cache key omits `dataScope`.
* The same legacy endpoint's `SalesFacts` fallback has no `dataScope`, operational-header, DUG/KOREKCIJA or signed-return population contract. Its operational fallback also lacks the canonical header exclusion.
* `/api/analytics/sales/comparison` aggregates raw `SalesFacts` and caches only by period, with no `dataScope` or operational retail population contract.
* `mv_daily_sales_facts` and its rolling/momentum dependents aggregate operational lines but do not show the canonical DUG/KOREKCIJA predicate in the installed definition. They must not be treated as certified standard-retail totals until refreshed and reconciled.
* `analytics_intel` signal views use `SalesFacts`/`SalesLineFacts` without a visible `DataOrigin` and operational-population predicate. They are a separate follow-up surface and must be included in regression coverage before being called current retail signals.

No evidence of a double join multiplying Access line amounts was found: source-line parity is exact, and the current canonical daily path joins one operational line to one receipt header and one article. The risk is population/fallback/cache divergence, not an Access line multiplication.

## Decision and remediation boundary

No local data repair is safe from this audit alone. Do not delete the 109/283 DEMO rows, the RQ407 fixtures, archived dimensions or `import-fix` suppliers. Do not rebuild `AnalyticsDailySummary` in place without a backup, dry-run before/after comparison, idempotent refresh and rollback procedure.

An independent code gap was registered as `RQ605` (P0, READY, unclaimed): unify legacy daily/comparison/fact-based signal consumers with the canonical operational retail population and `dataScope`, add scope-aware cache keys, and add real PostgreSQL regression tests. The acceptance must cover Access #23 parity, existing DEMO isolation, DUG/KOREKCIJA, signed returns, cache miss/hit, fallback and no double aggregation. This audit did not implement that remediation.

## Reviewed import/worker transaction boundary

The prior P1 transaction/heartbeat repair is present on current `main`: `PersistBatchProgressAsync` uses a separate connection with 750 ms lock timeout and 2 s statement timeout; the business import remains inside `RetriableDbContextTransaction`; cancellation rolls back with `CancellationToken.None`; worker failure/interruption paths write terminal/interrupted best-effort status; and `ClaimNextAsync` uses the advisory queue guard, `NOT EXISTS` running gate and `FOR UPDATE SKIP LOCKED`. Focused PostgreSQL heartbeat/lock-contention tests exist and passed in the preceding delivery. Batch #23 is now completed and the current database has no blocking lock chain.

## Files and reproducibility

* SQL evidence: `tools/access-import-23-fact-audit.sql`
* This audit: `docs/qa/ACCESS_IMPORT_23_FACT_AUDIT_2026-10-10.md`
* Queue item: `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md` — `RQ605`
* Durable run log: `.ai/runs/2026-10-10-access-import-23-fact-audit-evidence.md`

## RQ605 remediation verification — 2026-10-10

The legacy cached daily/summary and sales-comparison consumers were changed to use the operational receipt/line population directly. They now reuse `SalesDataScopePolicy.HeaderPredicate` and `SalesReceiptPopulationPolicy.IncludedHeaderPredicate`, retain signed `kolicina * cena`, and include normalized `dataScope` in their cache keys. The stale raw `SalesFacts`/aggregate path is not used by these three consumers; no analytical formula or fact row was changed.

The PostgreSQL RQ605 fixture used an independent SQL oracle with imported, existing, `all`, a negative return, a DUG receipt and an orphan 2,676,700.00 RSD fact. The endpoint totals matched the oracle for all three scopes, the orphan never entered canonical totals, the DUG receipt was excluded, the return remained signed, and the previous-period comparison matched. Cache miss/hit and scope-separated keys were asserted.

The initial 2b line audit was corrected during this run. `SalesLineSourceIdentity.Resolve` defines the expected identity as `(source_table_key, source_row_id)` only when both lineage values exist; otherwise it uses `('trendplus.prodaja_stavke', prodaja_stavke.id)`. Batch #23 has `source_row_id IS NULL` for all 67,092 operational lines, so the operational `prodajastavke` value is not the analytics namespace. The corrected audit returned 67,092 audited lines, `min_fact_count=1`, `max_fact_count=1`, and `violating_lines=0`; the full read-only audit exited successfully.

Fresh scope oracle totals remain:

| basis / scope | receipts | units | amount |
|---|---:|---:|---:|
| raw facts / imported | 5,530 | 67,664 | 240,603,523.64 |
| raw facts / existing | 110 | 484 | 2,697,200.00 |
| raw facts / all | 5,640 | 68,148 | 243,300,723.64 |
| operational retail / imported | 5,226 | 66,520 | 234,829,573.64 |
| operational retail / existing | 5 | 8 | 750.00 |
| operational retail / all | 5,231 | 66,528 | 234,830,323.64 |

The orphan `existing` population therefore remains a consumer-population issue, not proof of Access overcount. No repair, delete, cache rebuild or production mutation was executed.
