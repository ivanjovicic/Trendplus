# RQ187 evidence

Task ID: RQ187
Queue: `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
Date: 2026-09-08
Agent/tool: Codex
Delivery target: `main`
Working branch / PR: `codex/rq187-cache-refresh-provenance-20260908` / local merge, no PR
Main commit SHA: pending
Main verification: pending
Evidence state: pending

## What was done

RQ187 was explicitly promoted because the canonical analytics queue had no current READY prompt, then claimed with a local task lock. Cached analytics policy metadata now keeps the cache-entry creation time separate from the authoritative source refresh time. On cache misses, the policy wrapper records the latest successful refresh timestamp from `AnalyticsRefreshStatusService`; on cache hits, it reuses that stored timestamp and never replaces it with the cache-read time. Legacy metadata without a source refresh timestamp remains `null` rather than inventing freshness.

The public analytics meta contract exposes `CacheCreatedAtUtc` for cache diagnostics and continues to expose `LastRefreshAtUtc` only from `DataRefreshAtUtc`. Staleness is evaluated against the source refresh timestamp when present, falling back to cache age only to preserve the existing warning behavior when source lineage is unavailable; the UI therefore sees an unknown refresh timestamp instead of a false current timestamp.

## Files changed

- `Infrastructure/Services/Caching/AnalyticsCacheEntryMetadata.cs` — nullable authoritative source refresh field.
- `Api/Dtos/AnalyticsResponseMetaDto.cs` — additive cache-created diagnostic field.
- `Api/Endpoints/CachedAnalyticsEndpoints.cs` — refresh-status propagation and cache metadata projection for core policy-wrapped endpoints.
- `Klijent/clientapp/src/types/analytics.ts` — additive frontend meta type.
- `Api.Tests/AnalyticsCacheFreshnessTests.cs` — old-source/new-cache, unknown-source and legacy metadata proof.
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md` — promotion, completion and delivery evidence.

## Validation run

- `dotnet test Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~AnalyticsCacheFreshnessTests --no-restore` — passed, 3/3.
- `dotnet test Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~CachedAnalyticsCriticalEndpointsIntegrationTests --no-build` — 15/16 passed; one unrelated existing integration fixture failed because the configured Neon database rejected authentication while loading inventory store names. No code change was made for that environment failure.
- `npm run check:analytics-guardrails` — passed, including encoding, analytics guardrails and TypeScript typecheck.
- `npm run build` — passed; TypeScript project build and Vite production bundle completed.
- `dotnet build Api.Tests/Api.Tests.csproj --configuration Release --no-restore` — passed, 0 errors; existing warnings remain.
- `node scripts/check-agent-instructions.mjs --self-test` — passed.
- `node scripts/check-agent-instructions.mjs` — passed.
- `node scripts/check-prompt-queues.mjs --self-test` — passed.
- `node scripts/check-prompt-queues.mjs` — passed.
- `node scripts/check-planning-architecture.mjs --self-test` — passed.
- `node scripts/check-planning-architecture.mjs` — passed.
- `git diff --check` — passed.

## Validation not run

- Live production refresh-worker execution and browser/deployed-runtime proof were not run.
- Full backend and frontend suites were not run; the focused metadata tests, existing cached-endpoint suite and mapped guardrails were used.
- No production data, schema or external service was changed.

## Analytics safety gate

- Surface: cached analytics response metadata used by Sales Summary, Top Products, Product Decision Center and Dashboard Bootstrap trust headers.
- Source of truth: durable successful analytics refresh status from `AnalyticsRefreshStatusService`; cache creation time is diagnostic only.
- Contract changed? yes, additively: cache-created time and source-refresh time are separate.
- Old contract: `LastRefreshAtUtc = AnalyticsCacheEntryMetadata.CreatedAtUtc`.
- New contract: `LastRefreshAtUtc = DataRefreshAtUtc`; `CacheCreatedAtUtc = CreatedAtUtc`; missing source refresh remains `null`.
- Unit/denominator: timestamps in UTC; no numeric analytics formula changed.
- Numerator: not applicable.
- Denominator: not applicable.
- True zero case: not applicable.
- Missing/unknown case: absent or legacy `DataRefreshAtUtc` stays null and is not replaced by query/cache time.
- No-baseline case: not applicable.
- Freshness/fallback case: stale age uses the authoritative source refresh when present; missing lineage remains visibly unknown and retains cache-age warning compatibility.
- DataScope/store/search filters: unchanged; cache keys and filtered data are unchanged.
- User-visible surfaces affected: cached Dashboard/PDC/Sales Summary/Top Products meta and their trust/freshness consumers.
- Export/detail/action payload affected? no direct export value or action contract changed; only additive response metadata for cached analytics.
- Tests proving true-zero vs unknown: not applicable; legacy/missing refresh test proves unknown timestamp is preserved.
- Tests proving table/detail/export/action parity: no numeric/table/detail/export/action semantics changed; existing payload shape is additive.
- Stop condition hit? no.

## Documentation impact

The queue completion note records the promotion, contract split, validation result, known environment failure and delivery proof.

## What was missed

The refresh worker was not executed against live infrastructure, so provider-side refresh lineage was not runtime-proven. The broad integration class retained one environment-authentication failure unrelated to this change.

## Risks

Existing cache metadata created before `DataRefreshAtUtc` was introduced has unknown source freshness until the cache is repopulated. This is intentionally fail-closed. RQ254 remains the separate owner for the Product Decision direct-builder/cache-miss query-time timestamp gap.

## Next

After local merge and push, verify the RQ187 merge SHA is contained in `origin/main`, synchronize the final remote SHA in this log and queue, and leave RQ188 WAITING.
