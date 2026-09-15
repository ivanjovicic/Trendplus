# Evening Operations consolidation evidence

Date: 2026-09-15

Queue: direct-user-request

Status: delivered to local `main`; remote push pending final commit.

## Outcome and ownership

Reviewed and integrated the evening RQ270–RQ282 Inventory and Supplier changes. The owning surfaces are Analytics Frontend / Inventory, Analytics Frontend / Supplier, and the Inventory API contract.

The integrated branch was `origin/cursor/rq282-supplier-null-id-identity-c753`, fast-forwarded from `658d1d28` to `044b6545`. It includes the consolidated fixes for Inventory reload/value/export/forecast/queue/impact semantics and Supplier embedded composition/filter scope/fallback/previous-period/freshness/identity behavior.

## Review findings repaired after integration

- Confirmed: Inventory reacted to `trendplus:data-scope-changed` but did not send the chosen scope to balance, list, insights, store comparison, workflow, or detail requests. Added the query contract end-to-end for every one of those endpoints.
- Confirmed: cached Inventory balance ignored scope and its cache key omitted it; cached list normalized scope but did not apply it to the article query. Both now use `access` for imported data and `existing`/empty origins for existing data, consistently with `InventoryEndpoints`.
- Confirmed: the merged Inventory detail construction no longer matched its internal record after `CostMissing` was added, causing two API compilation errors. Restored the nullable detail estimated-value contract so a missing cost remains unknown, and supplied the missing internal `CostMissing` argument.
- Deliberate scope boundary: forecast, alerts, rebalance, and size-curve snapshots have no data-origin/scope dimension in their backend snapshot contracts. They were not falsely marked as scoped. Adding that dimension requires a separate snapshot/materializer and schema change.

## Files changed in the follow-up repair

- `Api/Endpoints/InventoryEndpoints.cs`
- `Api/Endpoints/CachedAnalyticsEndpoints.cs`
- `Api/Dtos/InventoryExperienceDtos.cs`
- `Klijent/clientapp/src/services/analyticsApi.ts`
- `Klijent/clientapp/src/pages/InventoryPage.tsx`
- `Klijent/clientapp/src/services/__tests__/inventoryDataScopeApi.spec.ts`

## Validation

- `npm ci --ignore-scripts` in an isolated review worktree: passed.
- Review-branch focused frontend tests: 92 passed.
- `npm run check:analytics-guardrails`: passed.
- Consolidated current-main frontend tests: 17 files, 98 tests passed.
- `dotnet build Api/Api.csproj --no-restore`: passed after the follow-up repair; existing analyzer warnings remain.
- `dotnet test Api.Tests/Api.Tests.csproj --no-restore --filter "FullyQualifiedName~InventorySnapshotContractTests" --logger "console;verbosity=normal"`: 32 passed.
- `npm run build`: passed. Existing Vite chunk-size warnings remain for the `recharts` chunk.

## Local branch and worktree disposition

Reviewed dirty detached worktrees `Trendplus2-mainlatest`, `Trendplus2-obs10`, `Trendplus2-rl10`, and `Trendplus2-rq96`.

- Their committed bases were already ancestors of `main`.
- Their staged legacy inventory snapshot work was already represented by current `main`; remaining differences removed validity tests, removed explanatory SQL comments, weakened fail-closed database initialization, or converted missing readiness data to zeros.
- Removed the four dirty stale worktrees and the clean review worktree after that evidence review.
- Removed local recovery refs `cursor/obs10-dashboard-honesty`, `cursor/rl10-advisory-calibration`, `cursor/rq96-land`, and `cursor/rq96-observed-inventory-snapshot`. Remote recovery refs were not changed.

## Residual risk and next owner

The scope contract for snapshot-driven forecast, alert, rebalance, and size-curve panels remains a backend data-model task. The owner must add a proven per-row data-origin dimension to the snapshot materializers and then expose and test `dataScope`; sending an ignored query parameter would not be a valid fix.
