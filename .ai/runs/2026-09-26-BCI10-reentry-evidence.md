# BCI10 current-main re-entry evidence — 2026-09-26

## Claim

BCI10 was claimed for current-main backend-suite truth after the latest broad backend run returned red again. The local lock was created at `.ai/task-locks/BCI10-codex.lock.md` and will not be committed.

## Repository state

- Worktree: `C:\Users\Ivan\source\repos\Trendplus2`
- Branch: `main`
- Starting SHA: `3e845fd466d8899062f9a1ac81294c0cfda45e96`
- `origin/main` matched the starting SHA before this re-entry change.
- Existing `.codex-remote-attachments/` remains untracked and untouched.

## Remote evidence

- Run `36264090648`, head `c95171dff7bc8ad4dccb18dcefa7226df351f53d`, completed `failure`.
- Job `108465306251` failed only at `Run all backend tests with coverage`; restore, build and migration/bootstrap smoke passed.
- Job `108465306000` passed the dedicated RQ447 certification gate.
- Broad totals: `1475 total / 1394 passed / 43 failed / 38 skipped`.
- Concurrency is deliberate: `analytics-backend-tests-${{ github.ref }}` with `cancel-in-progress: true`. Older same-ref push runs can be cancelled, but run `36264090648` was fully completed, not stuck or waiting for cleanup.

## Local checks

- Focused SQL/cache-key probe: `32 total / 26 passed / 6 failed / 0 skipped`.
  - Failures are the known QDB03/session contract drift and the stale Color cache-version assertion.
- Focused supplier/report/cache probe exposed the warning-label drift and the minimal-API test-host binding failure.
- After the bounded binding repair, `AnalyticsCacheInvalidateAuthorizationTests` passed `4/4`, with `0` skipped.
- Local Docker/PostgreSQL is unavailable, so no claim is made for provider-backed integration parity.

## Changed file

- `Api/Endpoints/CachedAnalyticsEndpoints.cs`
  - Added explicit `[FromServices]` binding for the four `AnalyticsRefreshStatusService` route parameters.
  - This is a minimal test-host/metadata repair; it does not change authorization behavior or cache invalidation policy.

## Acceptance decision

BCI10 is `PARTIAL`, not `DONE`: the broad backend job remains red and contains several independent failure families. The dedicated RQ447 certification remains valid and is not reopened by this broad-suite triage.
