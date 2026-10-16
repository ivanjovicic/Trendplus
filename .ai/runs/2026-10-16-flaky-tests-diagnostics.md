# Flaky tests diagnostics - 2026-10-16

Diagnostics-only task approved by the owner. No timeout bumps, no skips, no local 5434 database, no Development environment.

## 1. Backend: WorkerRuntimeSettings / EF ManyServiceProvidersCreatedWarning

- Logs of all failed and re-attempted `analytics-tests.yml` runs among the last 200 (2026-10-10..16) were downloaded with `gh api .../attempts/N/logs` and parsed.
- Dominant real failure in every mass-failure run: `InvalidOperationException` for `ManyServiceProvidersCreatedWarning` (runs `38422963322` 89, `38647868045` 229, `38718398984` 227, `38752847869` 97, `38798077405` 18 failing tests). After RQ665 (2026-10-14 12:50 UTC): 4/49 non-cancelled runs (~8 %).
- `relation "WorkerRuntimeSettings" does not exist` appeared in exactly one failure (`38798077405`, `WorkerRuntimePolicyServiceTests.GetPolicyAsync_CreatesDefaults_WhenRowDoesNotExist`, line 112), preceded by `Failed to evaluate worker runtime policy ... ManyServiceProvidersCreatedWarning`: the service swallowed the warning, returned the allow fallback, schema ensure never ran on the fresh database.
- Migration `20260507132430_AddWorkerRuntimeSettings` carries `[DbContext(typeof(TrendplusDbContext))]`. Test fixtures create a unique database per test (`TryCreateDatabaseConnectionStringAsync`), schema via `EnsureCreated`/`MigrateAsync` per class; no shared-schema race found.
- RQ665 covered only WebApplicationFactory hosts; failing sites are plain `ServiceCollection` registrations. Of the failing classes in `38798077405`, `NightlyAnalyticsRefreshWorkerTests` (2 commits) and `AnalyticsAggregationWorkerTests` (1 commit) changed after RQ665; the rest pre-date it (coverage gap, not a regression of RQ665's own scope).
- Local Docker reproduction was not run (CI stack traces are deterministic about the cause; the trigger is process-wide order).
- Follow-up: RQ685.

## 2. Frontend: InventoryPage.queueStatus

- Before fix: alone 0/20, `--sequence.shuffle` 1/15 (seed 15; rerun of seed 15 0/5, so timing- not order-dependent), full `vitest run` 1/3; always `passes the selected store into rebalance suggestions`, `getByLabelText("Filter po prodavnici")` at line 430.
- Cause: the test waited only for the first `getRebalanceSuggestions` call; the store filter renders only when `storeFiltersReady`, which can settle later.
- Fix: await `findByLabelText` and the store option before `fireEvent.change` (test-only). After fix: shuffle 0/30, full suite 3/3 (2060 tests). PR #183.

## 3. EF service providers per class

See section 1 and RQ685 evidence (171 sites, 119 without opt-out; risky subset = per-test `ServiceCollection`).
