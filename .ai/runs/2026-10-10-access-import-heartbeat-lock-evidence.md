# Trendplus Access import heartbeat/transaction blocking evidence

Task ID: access-import-heartbeat-lock
Queue: direct-user-request
Date: 2026-10-10
Agent/tool: Codex desktop
Delivery target: main
Working branch / PR: main / direct delivery
Main commit SHA: 59090c0b96b4f7ecb8f94cd00d07aca2056b31e4
Main verification: fresh fetch confirms origin/main == 690313888df6b904c98e0e3268dde508b46d5fa0; implementation SHA 59090c0b96b4f7ecb8f94cd00d07aca2056b31e4 is an ancestor of current main
Evidence state: synchronized
Ownership transfer: none

## What was done

- Confirmed the lock cycle: `ExecuteImportBatchAsync` kept `DataImportBatches` tracked by the import DbContext and transaction, while `PersistBatchProgressAsync` used another scope to update the same row. The heartbeat therefore waited behind the long business transaction and could inherit the global Npgsql timeout.
- Detached the batch entity before the business transaction; the business import remains one retryable transaction and writes the terminal `completed` state with the final business commit.
- Reworked heartbeat persistence to an independent Npgsql connection/short transaction with local `lock_timeout=750ms`, `statement_timeout=2000ms` and a 3-second command cap. Contention is skipped and retried on the next heartbeat tick.
- Made rollback cancellation-safe with `RollbackAsync(CancellationToken.None)`.
- Added a PostgreSQL advisory queue guard and active-running check so concurrent workers cannot claim parallel Access imports.
- Added an atomic pending-to-running claim for direct execution race protection and preserved stale-running terminal recovery.

## Files changed

- `Api/Services/AccessImportService.cs`
- `Api/Services/Access/AccessImportJobQueue.cs`
- `Api/Services/RetriableDbContextTransaction.cs`
- `Api.Tests/AccessImportHeartbeatPostgresIntegrationTests.cs`
- `Api.Tests/AccessImportJobQueueTests.cs`
- `.ai/runs/2026-10-10-access-import-heartbeat-lock-evidence.md`

## Validation run

- `git fetch origin main` -> pass; local `main` and `origin/main` were both `ecda09b2fec80d374128cbe8ff80b91545288c15` before implementation.
- `git push origin main` -> pass; implementation commit `59090c0b96b4f7ecb8f94cd00d07aca2056b31e4` delivered to `main`.
- Fresh `git fetch origin main` + `git rev-parse` + ancestor check -> pass; `origin/main` resolved to `59090c0b96b4f7ecb8f94cd00d07aca2056b31e4`.
- Final fresh `git fetch origin main` + `git rev-parse` + ancestor check -> pass; current `origin/main` is `690313888df6b904c98e0e3268dde508b46d5fa0` and contains implementation SHA `59090c0b96b4f7ecb8f94cd00d07aca2056b31e4`.
- `dotnet build Api.Tests/Api.Tests.csproj -c Release --no-restore --verbosity minimal` -> pass; 0 errors, existing warning baseline.
- `dotnet test Api.Tests/Api.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~AccessImportHeartbeatPostgresIntegrationTests|FullyQualifiedName~AccessImportJobQueueTests|FullyQualifiedName~AccessImportExecutionStrategyTests|FullyQualifiedName~AccessImportCancellationTests|FullyQualifiedName~AccessImportRetryAtomicityTests"` -> pass, 16/16. PostgreSQL Testcontainers was available.
- `dotnet test Api.Tests/Api.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~AccessImportServiceTests|FullyQualifiedName~AccessImportEnqueueTests|FullyQualifiedName~OperationsAnalyticsPostImportProbeTests|FullyQualifiedName~OperationsAnalyticsIntegrityFamilyTests"` -> pass, 61/61.
- The PostgreSQL integration proof includes: long open business transaction with advancing heartbeat; intentional `DataImportBatches` row lock with bounded heartbeat return; cancellation rollback; concurrent worker claims; and stale-running recovery after worker restart.
- `git diff --check` -> pass.

## Validation not run

- Full backend suite -> not run; focused PostgreSQL/import/worker proof covers the changed owner and named failure modes.
- Debug build -> not used as completion proof; it was blocked by an unrelated existing `.NET Host (9796)` lock on `Api/bin/Debug/net8.0/Api.dll`. Release build passed.
- Remote CI -> not inspected; not a completion gate for this direct-main task.

## Documentation impact

- Added this durable run log. No product or analytics formula documentation required; the change is limited to Access import transaction/worker mechanics and tests.

## What was missed

- None known within the requested import transaction/heartbeat/queue/recovery scope.

## Risks

- Existing repository analyzer warnings remain; no new product-blocking warning was introduced by the runtime change.
- Heartbeat telemetry intentionally skips a cycle when the batch status row is independently locked; the next periodic tick retries, while terminal business status remains commit-coupled.

## Post-close routing recovery

- Not applicable: direct user request, no formal queue claim.

## Next

- None; implementation SHA `59090c0b96b4f7ecb8f94cd00d07aca2056b31e4` is verified on `origin/main`.
