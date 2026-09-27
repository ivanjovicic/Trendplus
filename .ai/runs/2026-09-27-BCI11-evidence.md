Task ID: BCI11
Queue: docs/ai/BACKEND_CI_REPAIR_PROMPT_QUEUE.md
Date: 2026-09-27
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / direct delivery
Main commit SHA: 2c347ec5
Main verification: passed - `git rev-parse origin/main` and `git rev-parse HEAD` both resolve to 2c347ec5; `git merge-base --is-ancestor 2c347ec5 origin/main` passed.
Evidence state: synchronized

## What was done
- Reproduced the five deterministic SQL Server source-session contract failures on current main.
- Reconciled the connector runtime and focused tests around the canonical read-only contract.
- Kept source identity and connection diagnostics credential-free.
- Added provider-safe parameterized TOP, named cursor parameters, quoted identifiers and deterministic full-scan ordering.
- Updated the stale family expectation that still required an unordered full scan.

## Files changed
- Api/Services/DataSources/SqlServerSourceDataSession.cs
- Api.Tests/SqlServerSourceDataSessionSqlTests.cs
- Api.Tests/SqlServerSourceDataSessionTests.cs
- Api.Tests/SqlServerSourceDataSessionIntegrationTests.cs
- MASTER_ROADMAP.md
- docs/ai/BACKEND_CI_REPAIR_PROMPT_QUEUE.md
- .ai/runs/2026-09-27-BCI11-evidence.md

## Validation run
- Initial focused reproduction on current main: 5 failed / 9 passed; all five failures matched the BCI11 evidence.
- `dotnet test Api.Tests/Api.Tests.csproj --no-restore --configuration Release --filter "FullyQualifiedName~SqlServerSourceDataSessionSqlTests" --verbosity minimal`: pass, 14/14, 0 skipped.
- `dotnet test Api.Tests/Api.Tests.csproj --no-restore --configuration Release --filter "FullyQualifiedName~SqlServerSourceDataSession" --verbosity minimal`: pass, 25/25, 0 skipped; live Testcontainers SQL Server integration included.
- `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --verbosity minimal --logger "console;verbosity=minimal"`: fail for unrelated current-main residuals, 1478 total / 1427 passed / 13 failed / 38 skipped.
- `git diff --check`: pass.

## Validation not run
- GitHub Actions result for the final delivery SHA: not inspected; repository policy does not require waiting for remote CI before main delivery.
- Full broad-suite failures were not repaired in BCI11 because they are outside the SQL Server source-session owner boundary.

## Documentation impact
- BCI11 was closed in the owning backend CI queue and the canonical roadmap now points to BCI12 as the primary READY lane.
- The broad-suite residual is recorded honestly; BCI10 remains PARTIAL.

## What was missed
- No current-main broad green proof; the suite remains red because the local environment exposes invalid Neon credentials, missing SQL Server connection configuration and unrelated endpoint/provider/order-isolation failures.

## Risks
- BCI10 cannot close until the residual broad-suite families are repaired and a fresh exact-main broad run is green.
- Remote CI may expose additional environment-specific failures not reproduced by the focused SQL Server family.

## Next
- BCI12: align the Color cache-key version contract.
- Then route the smallest proven BCI13/BCI14 residual family after a fresh broad run.
