Task ID: RQ05-Journal
Queue: direct-user-request
Date: 2026-08-27
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / none
Main commit SHA: 397f9408639c4142a305f7b330c6d516f9ca3e41
Main verification: passed - local main contains 397f9408639c4142a305f7b330c6d516f9ca3e41
Evidence state: synchronized

## What was done
- Threaded `dataScope` through `LoadInventorySignalWindowStatsFromJournalAsync` so journal movement stats now respect imported/existing scope.
- Passed journal scope into both cached inventory list and Product Decision Center inventory signal paths.
- Added a focused regression test proving the same article can return different inventory sell-through evidence when journal rows are filtered by `dataScope`.
- Updated the scope audit and retrospective notes so the PDC journal-signal family is no longer listed as open.

## Files changed
- Api/Endpoints/CachedAnalyticsEndpoints.cs
- Api.Tests/CachedAnalyticsCriticalEndpointsIntegrationTests.cs
- docs/qa/ANALYTICS_DATASCOPE_CONSISTENCY_AUDIT.md
- docs/qa/ANALYTICS_DATA_RELIABILITY_AUDIT.md
- docs/qa/ANALYTICS_RELIABILITY_RETROSPECTIVE_AUDIT_2026-08-23.md
- .ai/runs/2026-08-27-RQ05-Journal-evidence.md

## Validation run
- dotnet test .\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~Api.Tests.CachedAnalyticsCriticalEndpointsIntegrationTests.CachedInventoryList_RespectsJournalDataScope" -> pass
- git diff --check -> pass

## Validation not run
- Full solution build/test sweep -> not run - the narrow journal-signal regression was enough to prove the scoped repair.
- Queue governance validators -> not run - no live queue file was edited in this turn.

## Documentation impact
- Marked the PDC inventory journal-signal row as resolved in the data-scope audit.
- Updated the retrospective and reliability audit notes so journal-signal scope is documented as closed.

## What was missed
- `RQ05-F4` lost-sales validation/bootstrap remains the next open data-scope follow-up.

## Risks
- The journal-signal family itself is now scoped, but unrelated pre-existing dirty files in the working tree were intentionally left untouched.

## Next
- RQ05-F4 - Lost-sales validation/bootstrap honor request `dataScope`
