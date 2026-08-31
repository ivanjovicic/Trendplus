Task ID: mapping-preview-route-fix
Queue: direct-user-request
Date: 2026-08-31
Agent/tool: Codex
Delivery target: main
Working branch / PR: main
Main commit SHA: 353cd7cb22e84c3eff0e0e4c162498db1fb26aa7
Main verification: passed - origin/main contains 353cd7cb22e84c3eff0e0e4c162498db1fb26aa7
Evidence state: synchronized

## What was done
- Fixed the duplicate endpoint name collision that was crashing `/ready`.
- Renamed the admin data-source mapping preview route name to `PreviewAdminDataSourceMapping` so it no longer collides with the public data-sources preview route.
- Verified the two mapping-preview endpoints now have unique names.

## Files changed
- Api/Endpoints/AdminDataSourceEndpoints.cs

## Validation run
- `rg -n "PreviewDataSourceMapping|PreviewAdminDataSourceMapping" Api/Endpoints/AdminDataSourceEndpoints.cs Api/Endpoints/DataSourceMappingPreviewEndpoints.cs` -> pass
- `dotnet build Api/Api.csproj -c Release` -> pass
- `git diff --check` -> pass

## Validation not run
- `dotnet test` -> not run - the crash was a routing-name collision and build verification was sufficient for this small fix
- `npm run check:analytics-guardrails` -> not run - no analytics contract or frontend files changed
- `npm run build` -> not run - no frontend files changed

## Documentation impact
- No owner docs were changed for this routing fix.

## What was missed
- The Neon/Postgres `53100` storage limit from the pasted log was not changed by code; it remains an external environment constraint.

## Risks
- If another endpoint later reuses the same route name, `/ready` can fail again; endpoint-name uniqueness should stay part of review discipline.

## Next
- Investigate the Neon project size limit separately if that error is still happening in the target environment.
