# 2026-09-06 - Comprehensive final analytics bugs audit (pass #4)

Owner: direct-user-request (Ivan)
Task: Final comprehensive scan of frontend state, exports, filters, charts, detail views, cache, workers, and time/period handling
Date: 2026-09-06

## Execution

Subagent [Explorer](a6e39791-b909-48da-9226-fbf242c9d71e) performed exhaustive analysis across:
- Frontend async state management (race conditions, loading states)
- Export/Report generation (validation, truncation, timing)
- Filter, Search, DataScope handling
- Chart and Table visualization parity
- Detail views and drill-down scope
- Cache invalidation and refresh semantics
- Worker and scheduled-task completion
- Time/period/timezone boundary handling
- Permission/authorization gaps
- Data type serialization mismatches

## Findings: 16 new bugs seeded as prompts

All added to canonical queue as RQ193-RQ208 (WAITING, P1/P2):

### P1 (Critical - 9 items):

1. **RQ193** - Inventory page cross-panel async race (multiple parallel fetches without sequence guard)
2. **RQ194** - Analytics Details missing in-flight guard (10+ parallel requests can mix)
3. **RQ196** - Inventory report schedules saved without validation (recipients, timezone)
4. **RQ198** - Decision Board hardcoded `dataScope="all"` (overrides user preference)
5. **RQ199** - Pre-nivelacija no DataScope parameter at all
6. **RQ200** - PDC search capped at backend rows (tail products unsearchable)
7. **RQ203** - Inventory detail ignores parent scope, uses fixed 30-day window
8. **RQ206** - Partial refresh treated as successful (green label on incomplete MV)
9. **RQ207** - Failed refresh skips cache invalidation (stale data persists)

### P2 (Important - 7 items):

1. **RQ195** - Pilot Readiness multi-signal load mixes reload generations
2. **RQ197** - Export has no row cap / tight timeout (large catalogs truncate)
3. **RQ201** - Daily Sales chart vs table order divergence
4. **RQ202** - Date sort uses local parsing (timezone drift)
5. **RQ204** - Analytics Details inventory scope unrelated to period
6. **RQ205** - Frontend client cache not invalidated after refresh
7. **RQ208** - Dashboard per-day KPIs use local day count

## Validation

- All prompts follow protocol statuses (WAITING, P1/P2)
- No duplicates of RQ154-RQ192 (calculation/numeric/freshness)
- New findings span: async races, scope/filter leaks, export validation, refresh semantics
- Cross-references recorded

## Delivery

- All 16 prompts added to canonical `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- Updated queue status summary table (now 349 total tasks)
- Evidence file: this run log

## Impact Summary

- **Async races** (RQ193-RQ195): Can cause mixed data snapshots after rapid filter/period changes
- **Scope/filter leaks** (RQ198-RQ200, RQ203-RQ204): Data visible outside intended scope (tenant, import, period)
- **Report/export issues** (RQ196-RQ197): Schedules fail silently; large exports truncate
- **Cache/refresh** (RQ205-RQ207): Stale data persists after refresh; partial updates not flagged
- **Timezone** (RQ202, RQ208): Period boundaries off-by-one near DST; local vs UTC confusion

Next steps:
- P1 issues should block pilot release or be explicitly accepted as known-safe
- P2 issues: roadmap for next cycle
- Frontend owners should prioritize RQ193-RQ194 (high-impact race conditions)
- Backend owners should prioritize RQ196, RQ206-RQ207 (validation, refresh completeness)

