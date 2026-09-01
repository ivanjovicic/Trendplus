# PILOT-NET-05 evidence

- Date: 2026-09-01
- Task: PILOT-NET-05
- Queue: direct-user-request; planning candidate remains WAITING
- Owner: import/storage stability
- Outcome: make deleted-row rollback archive opt-in and prevent routine cleanup from silently growing the database.
- Files changed: `Api/Config/AccessImportOptions.cs`, `Api/Endpoints/AccessImportEndpoints.cs`, `Api/Services/AccessImportService.cs`, `render.yaml`, `docs/ops/POSTGRES_STORAGE_LIMIT_RUNBOOK.md`, `Api.Tests/AccessImportArchivePolicyTests.cs`.
- Behavior: `AccessImport:ArchiveDeletedRows` defaults to `false`; both cleanup paths archive only when explicitly enabled.
- Safety boundary: no business-table deletion behavior was changed; when archive is disabled, existing admin-protected cleanup can still delete its intended rows without creating rollback payloads.
- Evidence input: operator measured and intentionally cleared `deleted_rows_archive` (198 MB, 582,788 rows); resulting analytics database size was 163 MB and business tables remained non-zero.
- Validation: `dotnet test .\\Api.Tests\\Api.Tests.csproj --filter "FullyQualifiedName~AccessImportArchivePolicyTests" --no-restore` passed, 2/2.
- Additional validation: `git diff --check` passed; existing line-ending warnings remain. Queue/planning validators are unchanged by this runtime-only scope.
- Warnings: existing duplicate `Testcontainers.MsSql` package reference and pre-existing analyzer warnings remain.
- Checks not run: full test suite, live Render deployment, live cleanup endpoint, Neon storage verification after deployment.
- Delivery mode: uncommitted working-tree change.
- Main commit SHA: not applicable.
- Main verification: not applicable.
- Residual risk: the opt-in flag must be verified on the actual deployed Render service; no storage budget/preflight diagnostic or automated quota alert is implemented yet.
- Next: deploy the change when a release is authorized, verify `AccessImport__ArchiveDeletedRows=false` in Render, then continue local stabilization on the next highest-risk analytics contract without introducing a worker.
