## Plan: Access Import Pending→Running Remediation

TL;DR: Find why `DataImportBatches` rows stay `pending` and fix the handoff between the API and the background worker. Primary suspects: (1) no worker host registered or running, (2) runtime worker toggle disabled, (3) ClaimNext SQL filters or missing DB schema, (4) storage/file staging issues. The plan covers diagnostics, quick fixes, and targeted code/config changes.

**Steps**
1. Verify worker process & registration
   - Check app startup logs for the `Worker hosted services registered` and `Background workers startup state` messages printed by Program.cs. If `NO` or `DISABLED`, no hosted worker is running.
   - Confirm deployed process type: `PROCESS_TYPE=worker` or a separate worker host must run. *If only web processes run, start a worker.*
2. Verify runtime toggles
   - Call `GET /api/workers/control` and `GET /api/workers/health`. If the worker is paused, enable it via `POST /api/workers/control/enable` or set `Workers:Enabled=true` and ensure `AccessImportOptions.WorkerEnabled=true` in config.
3. Inspect pending batches in DB
   - Run: SELECT id, status, queuedatutc, sourcefilepath, sourcestoragekey, cancellationrequested, completedatutc FROM "DataImportBatches" WHERE status = 'pending' ORDER BY queuedatutc DESC LIMIT 50;
   - Verify `SourceFilePath` or `SourceStorageKey` is present (ClaimNext requires one of them). Check `CancellationRequested` and `CompletedAtUtc`.
4. Try manual enqueue
   - Use the API: `POST /api/access-import/jobs/{batchId}/enqueue` for a test batch. Observe HTTP response and worker logs.
5. Check job claiming path and logs
   - Inspect worker logs for: "Access import job claimed. BatchId: {BatchId}." or "Access import background worker started." Also look for warnings about missing columns: "Access import queue claim is using legacy schema..." or "skipped because DataImportBatches queue columns are not fully available yet."
6. Check DB schema & migrations
   - Ensure `DataImportBatches` has columns: `SourceFilePath`, `SourceStorageKey`, `QueuedAtUtc`, `LastHeartbeatUtc`, `CurrentStep`, `CurrentTable`. If missing, run migrations or let runtime bootstrap (EnsureDataImportBatchesTableAsync) run at startup.
7. Check ClaimNext SQL behavior and locks
   - Claim SQL (in AccessImportJobQueue) uses `FOR UPDATE SKIP LOCKED` and updates `Status='running'` atomically. Ensure your Postgres version/support and no long-running transaction blocks this query.
   - If ClaimNext repeatedly returns null while `pending` rows exist, search logs for Postgres errors or missing columns.
8. Diagnose storage/staging issues
   - If job is storage-backed (SourceStorageKey present), check storage provider connectivity and that staging in the worker (`StageSourceFromStorageAsync`) can download the file.
9. Short-term fixes (quick unblock)
   - Ensure a worker process is running with workers enabled.
   - If worker exists but paused, enable via `/api/workers/control/enable`.
   - Manually enqueue affected batches via `POST /api/access-import/jobs/{batchId}/enqueue` or run a safe ad-hoc SQL to set `Status='pending'` for eligible batches.
   - As an emergency: run a one-off script that scans `DataImportBatches WHERE Status IN ('failed','interrupted') AND COALESCE(CancellationRequested,false)=false` and calls enqueue API for each.
10. Mid/long-term fixes
   - Add an automatic, idempotent enqueuer (short-lived hosted service) that periodically scans `DataImportBatches` for `pending` rows and calls `IAccessImportJobQueue.EnqueueAsync`.
   - Improve observability: log ClaimNext results (debug), Enqueue attempts, and heartbeat persistence more prominently; emit metrics for pending count and last worker heartbeat.
   - Consider a message-broker-backed queue (RabbitMQ/Hangfire) if scaling requires durable queue semantics.

**Relevant files**
- [Api/Services/Access/AccessImportJobQueue.cs](Api/Services/Access/AccessImportJobQueue.cs) — `EnqueueAsync`, `ClaimNextAsync` (enqueue & claim SQL)
- [Api/Services/Access/AccessImportBackgroundWorker.cs](Api/Services/Access/AccessImportBackgroundWorker.cs) — worker loop, `ProcessJobAsync`, pause/resume logic
- [Api/Services/AccessImportService.cs](Api/Services/AccessImportService.cs) — `StartImportAsync`, `CreateImportBatchAsync`, `RunExistingBatchAsync`, `ExecuteImportBatchAsync`, heartbeat persistence
- [Api/Endpoints/AccessImportEndpoints.cs](Api/Endpoints/AccessImportEndpoints.cs) — manual enqueue endpoint
- [Api/Program.cs](Api/Program.cs) — DI + worker registration, process-type selection prints
- [Api/Config/WorkerRuntimeConfig.cs](Api/Config/WorkerRuntimeConfig.cs) — hosted-service registration policy
- [Api/Config/AccessImportOptions.cs](Api/Config/AccessImportOptions.cs) — worker config flags
- [Domain/Model/DataImportBatch.cs](Domain/Model/DataImportBatch.cs) — entity shape & fields used in claim/enqueue
- [Database/Migrations/015_EnhancedAccessImport.sql](Database/Migrations/015_EnhancedAccessImport.sql) — DB-side schema for enhanced import features

**Verification**
1. After enabling worker / enqueueing batch: SQL to verify transition
   - SELECT id, status, startedatutc, lastheartbeatutc, currentstep, currenttable FROM "DataImportBatches" WHERE id = {batchId};
   - Expect `status='running'` and `LastHeartbeatUtc` updated soon after.
2. Worker logs: look for both claim and start messages
   - "Access import job claimed. BatchId: {BatchId}." and "Access import started. BatchId: {BatchId}."
3. Heartbeats: within a few seconds `LastHeartbeatUtc` should update; heartbeats are persisted periodically by `PersistBatchProgressAsync`.
4. If claim still not happening, capture DB query plan / blocked queries (Postgres): SELECT pid, state, query_start, query FROM pg_stat_activity WHERE state <> 'idle' ORDER BY query_start; and check for locks.

**Decisions / Assumptions**
- I assume the primary DB is Postgres (code uses Npgsql). If using a different DB, some SQL features (SKIP LOCKED) may not work identically.
- I assume worker hosted services are intentionally registered only in a dedicated worker process (see WorkerRuntimeConfig). If you run only web processes, background workers will not be registered.

**Further considerations**
1. Provide me with these artifacts to finalize remediation patches: worker process logs (startup + `Access import` lines), output of the pending-batches SQL, production `AccessImportOptions` values, and whether a separate worker host exists.
2. If you want an urgent hotfix, I can draft a small, safe automatic-enqueuer design and the minimal change set (no DB schema changes) for you to apply.
