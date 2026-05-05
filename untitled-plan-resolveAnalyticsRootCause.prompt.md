## Plan: Resolve analytics bootstrap timeouts

TL;DR
- The dashboard bootstrap endpoint (`/api/analytics/cached/dashboard/bootstrap`) composes many expensive snapshot builders. When cache is cold, the server computes multiple heavy DB queries in one request. Client-side failover layer currently times out requests after 25s (production default), so long server work or cache misses lead to client timeouts and the user-facing failure. Likely root causes: missing/old aggregates, cache backend (Redis) unavailable or misconfigured, worker not running, or missing DB indexes causing full scans.

**Steps**
1. Discovery (immediate, parallel)
   - Confirm whether precomputed aggregates exist and when they were last updated (SQL checks).
   - Check worker health/logs (`Workers/AnalyticsAggregationWorker.cs`) to see if periodic refreshes ran recently.
   - Inspect server logs for messages: `"Dashboard bootstrap fallback due to timeout"`, `"Analytics refresh completed"`, and `"Redis is not available"`.
   - Check the hybrid cache (Redis) availability in production.
   - Reproduce timing with a direct request to bootstrap and measure end-to-end time.

2. Short-term mitigations (minutes → hours)
   - If worker is not running / failed: start it or run `Update-AnalyticsSnapshots` script manually to warm aggregates.
   - If Redis is unavailable: enable/repair Redis or rely on in-memory cache for a single instance while addressing Redis.
   - Consider temporarily increasing client bootstrap timeout to 60s (quick UX fix) and show partial results as they arrive.

3. Medium-term fixes (hours → days)
   - Apply missing DB indexes from `018_PerformanceIndexes_Trendplus.sql` and `018_PerformanceIndexes_Analytics.sql` (or equivalent migrations).
   - Ensure `AnalyticsAggregationWorker` runs reliably, with appropriate refresh interval and no frequent cache invalidation.
   - Verify `HybridCacheService` is configured with a healthy Redis instance; tune TTLs for heavy endpoints (bootstrap currently short TTL = 30s on client).

4. Long-term refactor (days → weeks)
   - Break `dashboard/bootstrap` into smaller endpoints and progressively hydrate UI (summary first, then advanced panels lazily).
   - Stream or chunk large snapshots, or return partial payloads with status (so the page shows data early and fills remaining panels when ready).
   - Move more expensive aggregations to precomputed materialized views or daily batch jobs and store results in dedicated tables used by cached endpoints.

**Relevant files**
- `Api/Endpoints/CachedAnalyticsEndpoints.cs` — dashboard bootstrap and snapshot builders: [Api/Endpoints/CachedAnalyticsEndpoints.cs](Api/Endpoints/CachedAnalyticsEndpoints.cs#L1137)
- `Api/Endpoints/AllEndpoints.cs` — redirects to cached endpoints: [Api/Endpoints/AllEndpoints.cs](Api/Endpoints/AllEndpoints.cs#L4022)
- `Workers/AnalyticsAggregationWorker.cs` — background snapshot refresh logic: [Workers/AnalyticsAggregationWorker.cs](Workers/AnalyticsAggregationWorker.cs#L1)
- `Infrastructure/Services/Caching/HybridCacheService.cs` — L1/L2 cache logic and Redis fallback: [Infrastructure/Services/Caching/HybridCacheService.cs](Infrastructure/Services/Caching/HybridCacheService.cs#L1)
- Client failover/timeouts: `Klijent/clientapp/src/utils/apiFailover.ts`: [Klijent/clientapp/src/utils/apiFailover.ts](Klijent/clientapp/src/utils/apiFailover.ts#L1)
- Client analytics API TTLs: `Klijent/clientapp/src/services/analyticsApi.ts`: [Klijent/clientapp/src/services/analyticsApi.ts](Klijent/clientapp/src/services/analyticsApi.ts#L208)
- Heavy query implementations (example): `BuildSalesSummarySnapshotAsync` at: [Api/Endpoints/CachedAnalyticsEndpoints.cs](Api/Endpoints/CachedAnalyticsEndpoints.cs#L2779)
- Index scripts: [018_PerformanceIndexes_Trendplus.sql](018_PerformanceIndexes_Trendplus.sql), [018_PerformanceIndexes_Analytics.sql](018_PerformanceIndexes_Analytics.sql)

**Verification (commands & queries)**
1. Measure bootstrap latency from a machine with production-like network:
```bash
# unix / mac / WSL
curl -w "%{http_code} %{time_total}\n" -sS "https://<API_BASE>/api/analytics/cached/dashboard/bootstrap?fromDate=2026-04-01&toDate=2026-04-30" -o /dev/null
```
or PowerShell:
```powershell
Measure-Command { Invoke-WebRequest -UseBasicParsing "https://<API_BASE>/api/analytics/cached/dashboard/bootstrap?fromDate=2026-04-01&toDate=2026-04-30" }
```
2. Check aggregated tables last update timestamps:
```sql
SELECT 'AnalyticsDailySummary' AS table, COUNT(*) AS rows, MAX("UpdatedAt") AS last_update FROM "AnalyticsDailySummary";
SELECT 'AnalyticsTopProducts' AS table, COUNT(*), MAX("UpdatedAt") FROM "AnalyticsTopProducts";
```
3. If aggregates missing or old, force a refresh (manual run of worker tasks):
```powershell
# If you have worker console access or the Update-AnalyticsSnapshots script
pwsh Api.Tests/Update-AnalyticsSnapshots.ps1 -BaseUrl https://<API_BASE>
```
4. Run EXPLAIN ANALYZE on the heavy queries used by `Build*SnapshotAsync` to find sequential scans or missing index usage. Example (psql):
```sql
EXPLAIN ANALYZE
SELECT p.datum_prodaje, SUM(ps.kolicina * ps.cena)
FROM prodaja_zaglavlje p
JOIN prodaja_stavke ps ON p.id = ps.id_prodaja
WHERE p.datum_prodaje >= '2026-04-01' AND p.datum_prodaje < '2026-05-01'
GROUP BY p.datum_prodaje;
```
5. Check worker logs and health (Docker/systemd/Kubernetes logs): look for `"Analytics refresh completed"` and recent timestamps.
6. Check application logs for cache/redis warnings (`"Redis is not available"`, `"Cache FACTORY executing"`, `"Cache MISS"`).

**Concrete patch plan (what to change, and where)**
1. Immediate patch (safe):
   - Start or trigger the analytics worker to warm aggregate tables (no code change). Files: [Workers/AnalyticsAggregationWorker.cs](Workers/AnalyticsAggregationWorker.cs#L1)
   - Optionally run `Api.Tests/Update-AnalyticsSnapshots.ps1` manually.
2. Low-risk code change (quick UX):
   - Update `getErrorText` mapping in `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx` so users see friendly message instead of raw `ApiFailoverTimeoutError` (already prepared).
   - Optionally bump `VITE_API_REQUEST_TIMEOUT_MS` to 60000 in production env if backend needs more time (short-term only).
3. Medium-risk infra change:
   - Ensure Redis is configured and reachable by the API (check `IDistributedCache` registration and connection string in production); fix connection.
   - Apply index SQL scripts from repo to production DB (safe to run if reviewed) — run `018_PerformanceIndexes_Trendplus.sql` and `018_PerformanceIndexes_Analytics.sql` on corresponding DBs.
4. Medium-term code change:
   - Split heavy `Build*SnapshotAsync` functions into smaller precomputed outputs produced by the worker and read by the cached endpoints.
   - Reduce client TTL for very heavy endpoints only if caching is effective; otherwise increase cache TTL to reduce recompute frequency.

**Decisions & assumptions**
- I assume production uses the same SQL schema names as in repo (case-sensitive names may differ).
- I assume Redis may be the intended L2 cache but could be misconfigured in production; if no Redis is used, caching is effectively L1-only and not shared across instances.
- I assume you can access production DB and logs (needed to confirm root cause). If not, we will provide exact commands to hand to your infra/ops person.

**Further considerations**
1. If you cannot grant DB/log access, ask your infra team for: worker logs for the last 24h, the result of `SELECT MAX("UpdatedAt") FROM "AnalyticsDailySummary"`, and Redis INFO output.
2. If many cache keys are constantly invalidated (worker calling `RemoveByPrefixAsync` frequently), consider narrowing invalidation to only affected keys.
3. After indexing and ensuring precomputed aggregates, re-run load tests and set client timeout to a conservative value (30s → 25s earlier; consider 30–60s depending on SLA).


-- End of plan
