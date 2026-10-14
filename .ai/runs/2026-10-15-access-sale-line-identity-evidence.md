Task ID: P0-access-sale-line-identity
Queue: direct-user-request
Date: 2026-10-15
Agent/tool: Codex
Delivery target: main
Working branch / PR: codex/access-sale-line-identity / pending
Main commit SHA: pending
Main verification: pending
Evidence state: pending
Ownership transfer: none
Claim reservation: direct-user-request/not applicable

## What was done
- Confirmed that all three active asynchronous sale-line paths used either a global target ID or `MAX(Id)+1`; both can collide with POS or concurrent writers.
- Changed direct, line-table and dnevnik synthesis paths to resolve existing lines by `(source_table_key, source_row_id)` and let PostgreSQL allocate new target IDs.
- Added classified PostgreSQL unique-violation handling for sale-line primary/source identity collisions.
- Rows lacking a stable source line ID now fail closed with `ambiguous_legacy_import_identity` instead of heuristic value-based deduplication.

## Files changed
- Api/Services/AccessImportService.cs
- Api.Tests/AccessImportForeignKeyGuardTests.cs
- .ai/runs/2026-10-15-access-sale-line-identity-evidence.md

## Validation run
- `dotnet build Api/Api.csproj --no-restore -p:BuildProjectReferences=false -m:1 --verbosity:minimal` -> pass (0 errors; pre-existing analyzer warnings).
- `dotnet build Api.Tests/Api.Tests.csproj --no-restore -p:BuildProjectReferences=false -m:1 --verbosity:minimal` -> pass (0 errors; 89 pre-existing warnings).
- `git diff --check` -> pass.
- Focused PostgreSQL class -> 0 passed, 5 skipped locally because PostgreSQL-required tests were not required by the local environment.

## Validation not run
- Required PostgreSQL execution, full/retry/concurrency scenarios, SQL oracle, and exact-head CI -> pending PR; the requested P0 coverage is not complete locally.

## Documentation impact
- This run log records the proven root cause and partial scope only.

## What was missed
- Cross-source namespace migration, remaining import-entity audit/remediation, all requested scenario coverage, and operator runbook are not complete.

## Risks
- Do not merge until focused PostgreSQL CI confirms the new identity path and the remaining required paths/tests are implemented.

## Post-close routing recovery
- not applicable for direct-user-request.

## Next
- Complete the remaining P0 matrix and PostgreSQL regression suite before delivery to main.
