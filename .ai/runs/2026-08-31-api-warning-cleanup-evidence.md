Task ID: api-warning-cleanup
Queue: direct-user-request
Date: 2026-08-31
Agent/tool: Codex
Delivery target: main
Working branch / PR: main
Main commit SHA: e4d53a6186c3a6e1c796a10ce9ddf6be543305e4
Main verification: passed - origin/main contains e4d53a6186c3a6e1c796a10ce9ddf6be543305e4
Evidence state: synchronized

## What was done
- Removed duplicate direct `Microsoft.Data.SqlClient` package references from `Api.csproj` after verifying the project still built cleanly through transitive references.
- Made `Program.cs` only reload JSON configuration files on development builds to avoid production file watcher churn.
- Consolidated runtime metadata key lookup arrays in `Program.cs` and improved invariant formatting for Npgsql tuning logs.
- Cleaned several analytics endpoint helpers by using concrete in-memory collection types where the methods already returned lists or dictionaries.
- Fixed mojibake/encoding artifacts in human-facing API text.
- Reduced analyzer warnings in `SupplierDecisionHubEndpoints` by replacing repeated list LINQ calls with direct index access and by hoisting a repeated constant reason-code array.

## Files changed
- Api/Api.csproj
- Api/Endpoints/AllEndpoints.cs
- Api/Endpoints/AnalyticsActionsEndpoints.cs
- Api/Endpoints/CachedAnalyticsEndpoints.cs
- Api/Endpoints/DataQualityEndpoints.cs
- Api/Endpoints/DecisionBoardEndpoints.cs
- Api/Endpoints/InventoryEndpoints.cs
- Api/Endpoints/PreNivelacijaPriorityEndpoints.cs
- Api/Endpoints/SupplierDecisionHubEndpoints.cs
- Api/Program.cs
- Api/Services/TransferService.cs

## Validation run
- `dotnet build Api/Api.csproj -c Release` -> pass
- `git diff --check` -> pass

## Validation not run
- `dotnet test` -> not run - backend cleanup was verified by build and analyzer output only
- `npm run check:analytics-guardrails` -> not run - no frontend or analytics contract files changed
- `npm run build` -> not run - no frontend or analytics contract files changed

## Documentation impact
- No owner docs were changed for this cleanup.

## What was missed
- The API still has unrelated analyzer warnings outside the touched areas.
- I did not add a dedicated regression test for the `TransferService` case-insensitive lookup path.

## Risks
- `TransferService` now relies on Npgsql provider translation for case-insensitive equality in the destination-article lookup path.
- Remaining warnings elsewhere in the API can still hide future regressions if the analyzer budget is not tackled in a dedicated pass.

## Next
- Consider a focused follow-up pass on the remaining API warnings, starting with the current warning hotspots outside this cleanup scope.
