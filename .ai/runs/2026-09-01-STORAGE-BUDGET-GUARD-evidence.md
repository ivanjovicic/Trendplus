# Storage Budget Guard Evidence

Date: 2026-09-01
Queue: direct-user-request
Status: implemented locally; deployment not performed

## Interpreted outcome

Prevent archive-enabled cleanup/import paths from starting when the existing `public.deleted_rows_archive` exceeds a bounded byte or row budget. Preserve archive-off-by-default behavior and do not introduce a worker or paid infrastructure.

## Owner and files

- Owner: access import cleanup/archive policy.
- Changed: `Api/Config/AccessImportOptions.cs`
- Changed: `Api/Endpoints/AccessImportEndpoints.cs`
- Changed: `Api/Services/AccessImportService.cs`
- Added: `Api/Services/ArchiveStorageBudgetGuard.cs`
- Changed: `Api.Tests/AccessImportArchivePolicyTests.cs`
- Changed: `render.yaml`
- Changed: `docs/ops/POSTGRES_STORAGE_LIMIT_RUNBOOK.md`

## Contract

- Archive writes remain disabled unless `AccessImport:ArchiveDeletedRows=true`.
- Default budget is 16 MiB and 10,000 archive rows.
- Cleanup fails closed when either existing limit is exceeded or a configured limit is invalid.
- The preflight is read-only and runs before destructive cleanup work.
- No business table is deleted by this change.

## Validation

Executed:

```text
dotnet test .\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~AccessImportArchivePolicyTests" --no-restore
```

Result: build and focused archive policy/budget tests passed. Existing warning: duplicate `Testcontainers.MsSql` package reference in `Api.Tests`.

Also executed: `dotnet restore .\Trendplus2.sln`, followed by `dotnet build .\Trendplus2.sln --no-restore` with 0 errors. `git diff --check` passed; line-ending normalization warnings are present for existing working-tree files.

## Not completed / residual risk

The guard checks current archive size and row count, but does not yet reserve or calculate projected growth for the incoming cleanup batch. Approved archive-enabled operations still require a post-operation storage check. No production deployment or database mutation was performed by this task.
