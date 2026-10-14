# Access import audit - 2026-10-14

Base: `origin/main` `292b6957`. Scope: `Klijent/clientapp/src/pages/AccessImportPage.tsx`, `src/services/accessImportApi.ts`, `Api/Endpoints/AccessImportEndpoints.cs`, `Api/Services/AccessImportService.cs`, `Api/Services/Access/*` (queue, worker, heartbeat, stale recovery). No import was run and no database was written.

## Root cause: running import not visible after reload or in another session

The server side was already correct: batches are persisted in `DataImportBatches` with status, step, table, percent and a heartbeat, `PreventConcurrentRuns` returns the existing batch instead of creating a new one, and the worker recovers stale batches. The client never asked the server:

1. `runningBatchId` was set only by the tab that clicked **Pokreni import** (`AccessImportPage.tsx:338`).
2. There was no mount-time load of batches (calls only after actions/manual refresh).
3. Reading batches requires admin access and the key was kept only in memory, so a new tab had no credential and no prompt.
4. The batch request deduper shared one in-flight request regardless of the admin key.

## Fixed now (RQ672, frontend)

- On load the screen fetches batches and attaches to the running or queued batch: status, batch id, step, table, percent, elapsed time from the server start time, and cancel.
- Without admin access it shows **Proveri status importa** instead of an idle screen or an error.
- Warning when a running batch has no heartbeat for 15 minutes (server minimum stale window).
- In-flight batch requests are shared only for the same admin key; an older request no longer clears a newer one.
- `Cancel import` translated to `Otkaži import`.

## Checked, already handled server-side

- Double start in the normal case: `PreventConcurrentRuns` reuses the active batch (`AccessImportService.cs:2291-2299`).
- API restart mid-import: worker marks the batch interrupted on shutdown and runs stale recovery at startup and periodically (`AccessImportBackgroundWorker.cs:93,209,419`); heartbeat persisted with short lock/statement timeouts (`AccessImportService.cs:1483-1576`).
- Cancel: persisted `CancellationRequested` flag, admin-protected endpoints (`AccessImportEndpoints.cs:226-275`).

## Registered prompts

- **RQ673** (READY, P2): atomic concurrent-start guard (check-then-insert race).
- **RQ674** (READY, P3): batch history timeout returns an empty list.
- **RQ675** (WAITING, owner decision): admin key across reloads/sessions (`window.prompt`, memory only).
- **RQ676** (READY, P3): Serbian text/diacritics and English server warnings.
- **RQ677** (WAITING, owner input): explicit upload size limit and 413 message.

## Verification

- New specs: `accessImportActiveBatch.spec.ts` (6), `AccessImportPage.spec.tsx` (2); `npm run test:analytics` 163 files / 1233 tests.
- `npm run typecheck`, `npm run build`, `check:analytics-guardrails`, `check:ui-ratchets`, `check:encoding` passed.
