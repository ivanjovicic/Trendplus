Task ID: DIRECT-2BP01
Queue: direct-user-request
Date: 2026-10-08
Agent/tool: Codex
Delivery target: main
Working branch / PR: fix/analytics-013-dependent-materialized-view / #112
Main commit SHA: 4ad1f3c5315813f005d61103686f226a84b9adcb
Main verification: implementation remains on the open PR branch; not merged
Evidence state: validated on isolated PostgreSQL 18.3; production schema not inspected
Ownership transfer: none

## What was done
- Changed the 013 compatibility script to create `povracaj_zaglavlje_mv` only when absent. It no longer drops an object with dependent views.
- Extended the supplier schema PostgreSQL integration test to delete the 013 startup-history row before the second repair. This forces a changed-hash rerun after `povracaj_zaglavlje` depends on the materialized view.
- Added assertions for the exact eight-column, ordered name-and-type contract on both `povracaj_zaglavlje_mv` and `povracaj_zaglavlje`.
- Added a no-container SQL contract test asserting that 013 uses `CREATE MATERIALIZED VIEW IF NOT EXISTS` and contains no drop for this materialized view.
- Opened PR #112. No production database was accessed or changed.

## Existing schema compatibility evidence
- The earlier 013 definition at commit `4f9e98872d81d3c316ff462fbd12ba31c0403b2c` exposed `povracaj_zaglavlje` as a regular view directly over `ReturnFacts`.
- The materialized-view definition introduced at commit `e089d61a7e0ec32e08941cf509a2a7e2668e2c88` and retained on current `main` uses the same aggregation and the same eight projected names: `id`, `broj_zapisnika`, `datum_povracaja`, `id_dobavljac`, `razlog_povracaja`, `status`, `ukupan_iznos`, `data_origin`.
- On the isolated PostgreSQL 18.3 test database after the forced rerun, catalog inspection confirmed `povracaj_zaglavlje_mv` is a populated materialized view (`relkind = 'm'`), `povracaj_zaglavlje` is a regular view (`relkind = 'v'`), and both expose the same ordered types: integer, text, timestamp with time zone, integer, text, text, numeric(18,2), text.
- This checks compatibility against the repository's historical schema definitions and the integration database. It does not prove the live production database has that exact definition because production was not inspected.

## Validation run
- `dotnet restore Api.Tests/Api.Tests.csproj --configfile NuGet.Config` -> pass (temporary NuGet cache/config on F:).
- `git diff --check` -> pass after the additional column-contract assertion.
- `dotnet test Api.Tests/Api.Tests.csproj --no-restore --filter FullyQualifiedName~SupplierDecisionSchemaSqlTests` -> pass, 47 tests; this also built Domain, Application, Infrastructure, Workers, Api and Api.Tests.
- `dotnet test Api.Tests/Api.Tests.csproj --no-restore --filter FullyQualifiedName~SupplierDecisionSchemaReadinessIntegrationTests` -> pass, 6/6, 0 skipped, against an isolated local PostgreSQL 18.3 server on 127.0.0.1:55432. A temporary local-only fixture connection override was used because Docker is unavailable; it is not part of the PR. No production database was used.
- GitHub combined status for the original PR head: Vercel success.

## Limitations and risk
- `CREATE MATERIALIZED VIEW IF NOT EXISTS` intentionally preserves an existing relation; it does not reconcile a future changed materialized-view query. Such changes need an explicit dependency-aware, non-destructive migration.
- The live production catalog was not queried. Before merge, confirm it has the repository-compatible projection if independent production-schema evidence is required.
- This PostgreSQL run used native PostgreSQL 18.3 rather than the Docker image normally selected by the Testcontainers fixture.

## Documentation impact
- Updated this run log with the PostgreSQL execution and schema compatibility evidence.

## Next
- Review PR #112 and its latest checks. Leave it open for review; no production schema changes were made.
