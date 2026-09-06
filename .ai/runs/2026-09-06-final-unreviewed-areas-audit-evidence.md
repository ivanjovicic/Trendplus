# 2026-09-06 - Final comprehensive analytics audit (pass #5 - migrations, transactions, workers, config)

Owner: direct-user-request (Ivan)
Task: Final scan of unreviewed areas: seed data, batch operations, error handling, data consistency, config, archiving, search, API contracts
Date: 2026-09-06

## Execution

Subagent [Explorer](dd9a4dec-363c-4956-bd59-76ba92252f96) performed exhaustive analysis across 8 unreviewed areas:
1. Seed data & migrations (dual paths, lock timeouts, parallel ordering, failure swallowing, rollback safety, seed drift)
2. Batch operations & transactions (non-atomic deletes, partial failures, outbox concurrency, import retry)
3. Error handling & observability (worker death, DLQ, exception leakage)
4. Data consistency (orphan joins, invalid FK handling)
5. Configuration (DB fallback, feature flags, worker schedule)
6. Historical/Archiving (archive quota failures, audit retention)
7. Search/Indexing (pagination stability)
8. API contracts (period timezone mismatch)

## Findings: 18 + 1 bonus NEW bugs seeded as prompts

All added as RQ209-RQ228 (WAITING, P0/P1/P2):

### P0 (CRITICAL - 2 items):
1. **RQ215** - Aggregate refresh delete+insert non-transactional (P0)
2. **RQ219** - Background worker crashes silently ignored (P0)

### P1 (BLOCKING - 14 items):
1. **RQ209** - Dual concurrent EF migrations
2. **RQ210** - Startup init skipped on lock timeout
3. **RQ211** - Parallel SQL migrations without ordering
4. **RQ212** - Migration failures swallowed
5. **RQ213** - Migration Down() drops fact tables without backup
6. **RQ216** - Cache invalidated after partial aggregate failure
7. **RQ217** - Outbox no row-level locking
8. **RQ218** - Import retry doesn't rollback
9. **RQ220** - Outbox DLQ not surfaced
10. **RQ222** - Daily vs dimensional aggregate mismatch
11. **RQ223** - SkipInvalidForeignKeys silently drops orphans
12. **RQ224** - Analytics DB fallback in prod
13. **RQ225** - Snapshot cost flag toggles live without validation
14. **RQ227** - Delete proceeds after archive failure
15. **RQ228** - Insight Studio v1/v2 timezone mismatch

### P2 (IMPORTANT - 3 items):
1. **RQ214** - Seed stock drift
2. **RQ221** - Exception leakage in error responses
3. **RQ226** - Worker schedule silently defaults

## Queue Summary

Total queue now: **369 tasks** (RQ209-RQ228 added)
- P0: 2 (RQ215, RQ219) — MUST fix before prod
- P1: 14 — MUST fix before pilot
- P2: 3 — roadmap

## Validation

Queue validator: ✅ 369 tasks OK (no duplicates, all protocol-compliant)

## Impact Assessment

| Category | Count | Key Risk |
|----------|-------|----------|
| Migration safety | 5 | Schema drift, startup race, fact-table loss |
| Transaction safety | 3 | Partial deletes, orphan data, duplicates |
| Worker reliability | 4 | Silent death, DLQ invisible, cache issues |
| Import safety | 2 | Retry duplicates, silent orphan skips |
| Config safety | 3 | DB fallback, flag toggle, schedule default |
| API contract | 1 | Period timezone mismatch across endpoints |

## Delivery

- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md` (+20 prompts, +1,100 lines)
- Evidence: this run log

Next steps:
- **P0 (RQ215, RQ219)**: Fix before any pilot release
- **P1 cluster (RQ209-RQ228 excluding P0)**: Schedule for release sprint
- **P2**: Roadmap for next cycle

