# RQ487 promotion recovery evidence

Date: 2026-10-02  
Base main: `a74c700eddab1c2f3c0f21acd52dc81c87df132c`  
Routing delivery through: `2f9b048b1aa9707dfddb8f8b6eb40ea1e10f2e6e`  
Task: canonical idle-recovery recheck after "no safe prompt" result  
Evidence state: synchronized for routing/promotion; product implementation not started

## Decision

`RQ487` is promoted `WAITING -> READY` as the single active Analytics Reliability prompt.

The previous gate was too broad and partly circular:
- `RQ474`, `RQ483` and `RQ470` are DONE;
- the old `Ready after` required a baseline timing/`EXPLAIN ANALYZE` measurement before RQ487, although producing that baseline is RQ487's first executable step;
- exact provider logs for the live Supplier 503 are still useful, but they belong to `RQ454`/`STAB16`, not to repository-local query profiling/equivalence work.

`RQ128` remains WAITING on STAB16. `RQ139` remains PARTIAL because its broad cross-surface/runtime proof is not complete. Neither was promoted.

## Current-main code recheck

The recheck deliberately removed stale PS14 work and retained only proven residuals:

1. Product Decision still materializes the matching article population and feeds the resulting IDs into current-period, previous-period and last-sale queries. This is a measurable large-ID-set candidate, not an automatic rewrite.
2. The RQ483 last-sale upper bound already exists (`DatumProdaje < periodToExclusiveUtc`). Do not redo it.
3. Product Decision cache search normalization already exists through `AnalyticsCacheKeys.HashPart` (trim + lowercase before hashing). Do not redo it.
4. Supplier overview still loads the full active snapshot batch into `snapshotCostBySaleLineId` before the relevant period sale-line population is bounded.
5. Supplier overview still carries a sale-line/timestamp-grain projection and the first-nivelacija lookup is bounded by period/store but not the relevant article set.
6. Supplier cache-hit code reads `AnalyticsCacheEntryMetadata` for logging and returns the stored JSON verbatim. RQ487 must prove whether request-time correlation/freshness/integrity-gate truth can become stale before changing it.

## Collision/dependency checks

- Primary RQ queue had no READY or IN_PROGRESS prompt before promotion.
- GitHub branch search for `rq487`: none.
- Open PR search for `RQ487`: none.
- No repository task-lock path matching RQ487 was found in code search.
- `RQ474`, `RQ483`, `RQ470` detailed sections are DONE.
- `RQ530` remains PARTIAL and consumes the RQ487 acceptance; it is not promoted in parallel.
- Provider/deployed root-cause proof remains with `RQ454`/`STAB16`.

## Prompt repair

RQ487 now requires the agent to:

1. establish a deterministic 30d/90d PostgreSQL/Testcontainers baseline before editing;
2. capture fixture cardinalities, timing/query shape and disposable-db `EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON)` where practical;
3. optimize only measured Product Decision ID-set work;
4. bound Supplier snapshot costs to participating sale-line IDs;
5. bound first-nivelacija work to relevant articles and only reduce SQL grain with row/totals parity;
6. prove cache miss -> hit request-time truth before changing cache projection;
7. rerun identical measurements and prove numeric/categorical equivalence;
8. stop/revert when a rewrite changes analytics truth or yields no reviewable improvement.

No production `EXPLAIN ANALYZE`, writes, schema/index change or invented percentage target is allowed by this prompt.

## Related Daily Sales triage

No duplicate Daily Sales prompt was created:
- commit `1ed9c9e57603f0534d81b79b6594774190a82824` fixed frontend/runtime validation for negative Access supplier IDs;
- commit `deb6db0419a60207bcf0790bd2d906ce6eac3d33` fixed the `from`/`to` query aliases that had been ignored in favor of the default last-30-day period;
- current code contains both repairs.

If the deployed API still shows the old behavior, the remaining action is deploy/exact-SHA verification rather than another implementation prompt.

## Files changed by routing repair

- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- `MASTER_ROADMAP.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_SUPPLIER_AUDIT_ADDENDUM.md`
- this evidence file

## Validation

Connector-side structural preflight after the routing edits:
- canonical RQ pointer = `RQ487`;
- detailed active RQ set = exactly `RQ487: READY`;
- RQ487 summary row = READY;
- MASTER_ROADMAP current RQ = `RQ487`;
- Supplier addendum current READY remains `none`; it only records the canonical RQ487 owner;
- RQ530 remains PARTIAL.

GitHub Planning Governance:
- run `37023237129` on `2f9b048b1aa9707dfddb8f8b6eb40ea1e10f2e6e`: completed / success.

Product/backend/frontend tests were not run because this recovery changes only queue/roadmap/evidence text. RQ487 itself must run the focused PostgreSQL/equivalence/performance tests named in its prompt.

## Next

Claim `RQ487`, create its local task lock, run the before-change disposable PostgreSQL baseline, then implement only the measured safe bounds. Do not wait for STAB16 merely to start repository-local RQ487 work. Do not mark deployed/provider performance VERIFIED without the separate STAB16/RQ454 evidence.
