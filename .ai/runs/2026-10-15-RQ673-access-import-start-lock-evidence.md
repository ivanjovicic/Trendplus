# RQ673 evidence - Access import start lock

- Owner decision: Ivan 2026-10-15 08:03 (advisory lock, no unique index, no migration).
- Lock: `pg_advisory_xact_lock(AccessImportBatchCreateAdvisoryLockKey)`, key `0x5450_4149_4D50_0001` ("TPAIMP"+1), taken inside an EF execution-strategy transaction; stale recovery, active check and insert run under it; released on commit/rollback.
- Loser: `AccessImportBatchAlreadyActiveException` (InvalidOperationException) -> existing `StartImportAsync` reuse response with the active batch ID; its unused upload/working copy is deleted.
- Non-Npgsql providers: unchanged best-effort check.
- Rejected: partial unique index on pending/running (schema change + Neon migration; abandoned active rows would block starts).
- Tests: Api.Tests/AccessImportConcurrentStartPostgresTests.cs (3 x [PostgresRequiredFact]); the parallel test holds the key from a third session until both requests wait in pg_locks, then releases.
- CI: PR #164 backend 38690736223, frontend 38690736280, governance 38690736170 all pass; merge cb9a81a6.
