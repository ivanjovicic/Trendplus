Plan: PU02 timeout mitigation

Goals
- Identify and eliminate causes of Fly `[PU02]` upstream timeouts for Access import.
- Ensure web request returns quickly or fails fast; imports run safely in background.
- Add bounded uploads, diagnostics, and tests to prevent regressions.

Steps
1. Audit request flow and OPTIONS path (completed)
   - Verify `OPTIONS /api/access-import/run` returns immediately and does not invoke import code.
2. Audit upload and enqueue path
   - Confirm `POST /run` only validates, stages source, persists pending batch, and returns 202.
3. Add timing and timeout instrumentation
   - Add start/finish/failure logs with `CorrelationId` and `ElapsedMs` for request, upload, worker.
   - Ensure `Storage:UploadTimeoutSeconds` is read from config and enforced.
4. Bound durable storage upload
   - Create linked `CancellationTokenSource` with `_storageUploadTimeout` and cancel after timeout.
   - Translate `OperationCanceledException` into `TimeoutException` and return 504.
5. Add regression tests
   - Cover OPTIONS fast path, POST accepted flow, upload timeout→504, and no corrupt batch on timeout.
6. Fix EF transaction retry bug
   - Introduce `RetriableDbContextTransaction` to use execution strategy when transactions are required.
7. Validate targeted tests and build
   - Run Access-import test subset and full test suite; fix any failures.
8. Stage, commit and push changes
   - Commit message: "PU02 timeout mitigation: bounded uploads, OPTIONS fast-path, logging and tests"
9. Verify remote push and CI
   - Confirm remote branch receives commit and CI passes.

Post-deploy follow-ups (recommended)
- Fix 504 response detail to return `ex.Message` rather than `ex.GetBaseException().Message`.
- Consider capping multipart/form parsing time or rejecting very large requests earlier.
- Run in separate web/worker machines and/or increase Fly machine memory to reduce contention.
- Monitor logs: `Access import run request started`, `S3 upload started/completed/failed`, `storage_timeout` outcomes, and Fly `[PU02]` occurrences.

Status
- Current: most server-side code changes implemented and pushed; tests for Access-import passing locally.
- Next: refine plan items, add any additional tests, and prepare staging deploy.
