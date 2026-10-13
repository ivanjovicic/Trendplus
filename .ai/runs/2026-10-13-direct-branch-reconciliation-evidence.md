Task ID: direct-branch-reconciliation
Queue: direct-user-request
Date: 2026-10-13
Agent/tool: Codex
Delivery target: main
Working branch / PR: codex/pg-proof-convention-audit / direct merge
Main commit SHA: ee79f7415b0be30287924a99740b54124c9c7bb8
Main verification: passed - refreshed origin/main contains ee79f7415b0be30287924a99740b54124c9c7bb8
Evidence state: synchronized
Ownership transfer: none

## What was done

- Refreshed all remote references against `main` `3307679e6c54c17daf77a2fb1a2e426eb5807d6b`: 112 non-main refs total, 83 reachable from main and 29 not reachable.
- Reviewed every not-reachable ref by changed paths, current-main blob/content state, delivery lineage and active-worktree ownership. No stale branch was forward-merged or deleted.
- Confirmed that the shipped current-main semantics include the former RQ481 fail-closed URL-ID behavior, RQ626/RQ628/RQ630 corrections, RQ637 startup gate, RQ638 Unicode bound, RQ645 Supplier contracts, and the `box/acc` null/zero fixes; later main changes intentionally differ from several old blobs.
- Repaired a confirmed test-integrity gap: the PostgreSQL convention check now follows same-file helper calls that gate on fixture availability, supports qualified xUnit attributes and collection fixtures, and has regression coverage for those patterns.
- Marked 50 discovered PostgreSQL-backed tests with `PostgresRequiredFact`, so no-Docker runs are reported as skipped rather than as false passes. Kept test behavior unchanged when PostgreSQL is available/required.
- Added an explicit SQL018/history prerequisite assertion to the current SQL029 lifecycle fixture, salvaging the one useful assertion from the stale RQ635 trial branch without replacing its newer fixture layout.
- Delivered the implementation through merge commit `ee79f7415b0be30287924a99740b54124c9c7bb8` on `main` and pushed it to `origin/main`.

## Files changed

- `Api.Tests/PostgresRequiredAttributeConventionTests.cs`
- `Api.Tests/AccessImportExecutionStrategyTests.cs`
- `Api.Tests/AccessImportForeignKeyGuardTests.cs`
- `Api.Tests/AccessImportHeartbeatPostgresIntegrationTests.cs`
- `Api.Tests/AccessImportJobQueueTests.cs`
- `Api.Tests/AssortmentNivelacijaOracleTests.cs`
- `Api.Tests/ColorSalesStatsIndependentOracleIntegrationTests.cs`
- `Api.Tests/DataQualityIssuesHandlerTests.cs`
- `Api.Tests/DataQualityPostgresIntegrationTests.cs`
- `Api.Tests/LostSalesValidationScopePostgresIntegrationTests.cs`
- `Api.Tests/NivelacijaReconciliationPackTests.cs`
- `Api.Tests/Rq605LegacySalesPostgresIntegrationTests.cs`
- `Api.Tests/Rq606TransferIdentityPostgresIntegrationTests.cs`
- `Api.Tests/SupplierWindowedSql029LifecycleTests.cs`
- `Api.Tests/VendorSalesNivelacijaContractDiagnosticsTests.cs`
- `.ai/runs/2026-10-13-direct-branch-reconciliation-evidence.md`

## Validation run

- `dotnet test Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~PostgresRequiredAttributeConventionTests --no-restore -p:WarningLevel=0` -> pass; 4 passed.
- `dotnet test Api.Tests/Api.Tests.csproj --filter "(FullyQualifiedName~AccessImportExecutionStrategyTests|FullyQualifiedName~DataQualityPostgresIntegrationTests|FullyQualifiedName~Rq606TransferIdentityPostgresIntegrationTests)" --no-build --no-restore -p:WarningLevel=0` -> expected no-Docker result; 16 skipped, 0 passed, 0 failed.
- `dotnet test Api.Tests/Api.Tests.csproj --filter "(FullyQualifiedName~PostgresRequiredAttributeConventionTests|FullyQualifiedName~SupplierWindowedSql029LifecycleTests)" --no-restore -p:WarningLevel=0` -> 6 passed, 4 expected PostgreSQL skips, 0 failed.
- `TRENDPLUS_REQUIRE_POSTGRES_TESTS=true dotnet test Api.Tests/Api.Tests.csproj --filter "(FullyQualifiedName~AccessImportExecutionStrategyTests|FullyQualifiedName~AccessImportForeignKeyGuardTests|FullyQualifiedName~AccessImportHeartbeatPostgresIntegrationTests|FullyQualifiedName~AccessImportJobQueueTests)" --no-build --no-restore -p:WarningLevel=0` -> real PostgreSQL pass; 17 passed, 0 skipped.
- `TRENDPLUS_REQUIRE_POSTGRES_TESTS=true dotnet test Api.Tests/Api.Tests.csproj --filter "(FullyQualifiedName~DataQualityIssuesHandlerTests|FullyQualifiedName~DataQualityPostgresIntegrationTests|FullyQualifiedName~LostSalesValidationScopePostgresIntegrationTests)" --no-build --no-restore -p:WarningLevel=0` -> real PostgreSQL pass; 17 passed, 0 skipped.
- `TRENDPLUS_REQUIRE_POSTGRES_TESTS=true dotnet test Api.Tests/Api.Tests.csproj --filter "(FullyQualifiedName~AssortmentNivelacijaOracleTests|FullyQualifiedName~ColorSalesStatsIndependentOracleIntegrationTests|FullyQualifiedName~VendorSalesNivelacijaContractDiagnosticsTests)" --no-build --no-restore -p:WarningLevel=0` -> real PostgreSQL pass; 22 passed, 0 skipped.
- `TRENDPLUS_REQUIRE_POSTGRES_TESTS=true dotnet test Api.Tests/Api.Tests.csproj --filter "(FullyQualifiedName~NivelacijaReconciliationPackTests|FullyQualifiedName~Rq605LegacySalesPostgresIntegrationTests|FullyQualifiedName~Rq606TransferIdentityPostgresIntegrationTests)" --no-build --no-restore -p:WarningLevel=0` -> real PostgreSQL pass; 11 passed, 0 skipped.
- `TRENDPLUS_REQUIRE_POSTGRES_TESTS=true dotnet test Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~SupplierWindowedSql029LifecycleTests --no-build --no-restore -p:WarningLevel=0` -> real PostgreSQL pass; 6 passed, 0 skipped.
- `git diff --check` -> pass before delivery.
- GitHub Actions `Analytics Tests & Data Integrity` run `38432014732` for `ee79f7415b0be30287924a99740b54124c9c7bb8` -> in progress when inspected after delivery.

## Validation not run

- Full backend suite -> not run locally; the changed PostgreSQL contracts and convention executed locally against real Testcontainers PostgreSQL, while the relevant current-main Actions run is in progress.

## Documentation impact

- No live queue/roadmap status was edited: this is a direct-user-request audit, and RQ648 was not claimed or closed.
- This durable run log records branch dispositions and the test-integrity repair.

## What was missed

- No safe branch-wide forward merge exists. Every reviewed divergent branch is either delivered/evolved on current main, an obsolete planning/evidence branch, or an active-owner artifact.
- Uncommitted contents of other worktrees were not read or modified. The active `codex/bci17-42-skip-gate` worktree remains exclusively owned by its existing task.

## Risks

- The changed PostgreSQL contracts executed locally; a full backend run was not run locally. GitHub Actions run `38432014732` is in progress and remains residual risk until it completes.
- Old planning/evidence branches intentionally remain; no remote branch cleanup was requested or authorized.
- Branch names and ancestor counts do not prove missing code after squash/direct-main delivery; future changes require the same content-level comparison.

## Post-close routing recovery

- not applicable - direct-user-request.

## Next

- Inspect Actions run `38432014732` if it turns red; otherwise no further repository action is required for this direct request.

## Branch dispositions

| Ref family | Disposition |
|---|---|
| `box/acc` | Its null-margin/trend and best-day fixes are already delivered by current-main commit `7aed4aad`; main test/source blobs subsequently evolved. Do not merge. |
| `audit/*` (five review/reliability branches) | Historical planning/evidence, with their actionable code delivered via later main/PR work. Current queues have newer routing. Do not merge. |
| `codex/product-value-plan-refinement`, `cursor/p-ui-*`, `cursor/queue-idle-*`, `docs/*` | Historical product, UI or closeout evidence; current canonical documents are newer. Do not merge stale statuses. |
| `audit/rq616-null-stock-*` | The nullable stock-baseline semantics are present in current SQL/test evolution after `9cc10817`; do not replace later source. |
| `codex/rq626-*`, `codex/rq628-*`, `codex/rq630-*`, `codex/rq645-*` | Delivered via their named current-main successor commits/PRs; some blobs differ only because main has later corrections. Do not merge. |
| `cursor/rq637-*`, `fix/rq638-*` | Exact code/test blob parity with main for their owned changes. Do not merge. |
| `fix/2026-10-13-supplier-diagnostics-*`, `fix/rq481-*` | Main contains the shipped diagnostics and fail-closed filter behavior, later evolved. Do not merge. |
| `fix/rq635-sql029-postgres-fixture` | Current main contains the corrected fixture and stricter mandatory-test gate. Do not merge. |
| `fix/rq635-real-postgres-ci-oracle` | Stale fixture layout; only its prerequisite assertion was safely retained in the current fixture. Do not merge. |
| local `codex/bci17-42-skip-gate` | Active worktree owned by another task; not inspected or merged. |
