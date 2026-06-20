# Analytics Observability Review

Date: 2026-06-19 15:15:00 +02:00
Local HEAD: `36c5e544916758520f6d8efed9901f5414313a80`

## Scope

- [Api/Middleware/RequestPerformanceLoggingMiddleware.cs](../../Api/Middleware/RequestPerformanceLoggingMiddleware.cs)
- [Api/Endpoints/HandledErrorLogging.cs](../../Api/Endpoints/HandledErrorLogging.cs)
- [Api/Services/AnalyticsRefreshStatusService.cs](../../Api/Services/AnalyticsRefreshStatusService.cs)
- [Klijent/clientapp/src/services/analyticsApi.ts](../../Klijent/clientapp/src/services/analyticsApi.ts)
- [Klijent/clientapp/src/components/analytics/AnalyticsErrorState.tsx](../../Klijent/clientapp/src/components/analytics/AnalyticsErrorState.tsx)
- [Klijent/clientapp/src/components/analytics/AnalyticsRefreshStatusBanner.tsx](../../Klijent/clientapp/src/components/analytics/AnalyticsRefreshStatusBanner.tsx)
- [docs/qa/ANALYTICS_PILOT_SMOKE_RESULT.md](./ANALYTICS_PILOT_SMOKE_RESULT.md)
- [docs/qa/ANALYTICS_LIVE_SMOKE_RESULT.md](./ANALYTICS_LIVE_SMOKE_RESULT.md)

## What We Verified

### 1. Backend logs already carry correlation IDs

- Request logging middleware records `CorrelationId` on completed HTTP requests.
- Handled error logging also stores correlation IDs when available.
- Analytics refresh status runs already persist correlation IDs on the recent run history.

### 2. Live smoke docs already record correlation IDs

- The pilot smoke result table already has a `Correlation ID` column.
- The live smoke result table already has a `Correlation ID` column for backend checks.
- That means the docs side of observability was already in place before this task.

### 3. Frontend error states now surface correlation IDs more clearly

- `AnalyticsErrorState` already renders the correlation ID when the backend provides one.
- The shared analytics error parser now preserves correlation IDs from response JSON or the `X-Correlation-ID` header.
- `AnalyticsRefreshStatusBanner` now shows the latest run correlation ID when the refresh state is stale/critical or otherwise warning-like.

## Evidence

- `Klijent/clientapp/src/services/analyticsApi.ts`
  - correlation ID suffix preserved in shared analytics API error strings
- `Klijent/clientapp/src/components/analytics/AnalyticsRefreshStatusBanner.tsx`
  - correlation ID visible in refresh warning/error states
- `Klijent/clientapp/src/components/analytics/__tests__/AnalyticsRefreshStatusBanner.spec.tsx`
  - regression coverage for correlation ID display
- `docs/qa/ANALYTICS_PILOT_SMOKE_RESULT.md`
  - correlation ID column already present in smoke evidence
- `docs/qa/ANALYTICS_LIVE_SMOKE_RESULT.md`
  - correlation ID column already present in live smoke evidence

## Verification

- `git diff --check` - pass
- `cd Klijent/clientapp && npm run check:analytics-guardrails` - pass
- `cd Klijent/clientapp && npm run build` - pass
- `cd Klijent/clientapp && npm run test -- --run AnalyticsRefreshStatusBanner` - pass

## Risk

- The refresh-status endpoint itself still returns a healthy payload with warning metadata rather than a hard error in many failure modes, so correlation IDs are only visible when a recent run exists or the shared API layer receives an explicit failing response.
- That is acceptable for now because it keeps the UI honest without inventing a fake incident ID.

## Next

- Q41 - Action Impact Ledger Phase 1 design-to-implementation gap review
