# Backup / Restore Rehearsal Evidence — 2026-08-06

Repo: `ivanjovicic/Trendplus`  
Task: `STAB07`  
Inspected at (UTC): `2026-08-06T07:38:44Z`  
Result: **DONE** (live local disposable restore PASS) with accepted non-P0 warnings

## Ownership map

| Asset | Backup owner | Restore target | Notes |
|---|---|---|---|
| Operational DB (`DefaultConnection`) | Ops / admin | Disposable DB only | Separate dump via `-RoleName operational` |
| Analytics DB (`AnalyticsConnection`) | Ops / admin | Disposable DB only | Separate dump via `-RoleName analytics` |
| Import files | Import operator | File archive / storage root | Not covered by pg_dump; keep originals |
| Generated documents/reports | Ops / sales | Object storage / `out/documents` copy | Non-DB artifact |
| Logs | Ops | Log sink / file archive | Do not commit customer data |
| Cache (Redis/memory) | n/a | Rebuild | Non-durable; invalidate after restore |

## Executable path added

| Script | Purpose |
|---|---|
| `scripts/ops/PostgresBackupCommon.ps1` | Shared production refusal, redacted endpoint summary, checksum helper, Npgsql to libpq URI, optional Docker client |
| `scripts/ops/Backup-PostgresDatabase.ps1` | `pg_dump` for `operational` or `analytics` |
| `scripts/ops/Restore-PostgresDatabase.ps1` | `pg_restore` into disposable DB (`-AllowDestructiveRestore`) |
| `scripts/ops/Invoke-BackupRestoreRehearsal.ps1` | Orchestrates operational + analytics roles |
| `scripts/ops/Test-BackupRestoreGuards.ps1` | Guard self-tests without production access |

### Required env (secrets never printed)

- `TRENDPLUS_OPS_REHEARSAL_SOURCE_URL`
- `TRENDPLUS_OPS_REHEARSAL_DEST_URL`
- `TRENDPLUS_ANALYTICS_REHEARSAL_SOURCE_URL`
- `TRENDPLUS_ANALYTICS_REHEARSAL_DEST_URL`
- Optional: `TRENDPLUS_PG_DOCKER_CONTAINER` (e.g. `trendplus-postgres`) — use container `pg_dump`/`pg_restore` so client major matches server (host PG 18 vs container PG 16 breaks restore via `transaction_timeout`)

Allowed `-EnvironmentLabel` values: `local`, `rehearsal`, `disposable`, `staging-rehearsal`, `ci-rehearsal`.  
Blocked labels: `production`, `prod`, `live`, …

Default restore sections: `pre-data,data` (skip post-data). Full restore with indexes/MV refresh: `-IncludePostData` (MV refresh can take tens of minutes locally).

## Guard / dry-run evidence

| Check | Result |
|---|---|
| `Test-BackupRestoreGuards.ps1` | **PASS** |
| Production label refusal | Covered by self-test |
| Remote `Database=trendplus` refusal | Covered by self-test |
| Missing dump fail-closed | Covered by self-test |
| Secret not present in endpoint summary | Covered by self-test |
| Npgsql to libpq URI conversion | Covered by self-test |
| `Invoke-BackupRestoreRehearsal.ps1 -EnvironmentLabel local -DryRun` | **PASS** |

Artifacts under `tmp/ops-rehearsal/` are local-only and must not be committed.

## Live disposable restore (local Docker)

| Item | Status |
|---|---|
| Runtime | Docker Desktop; container `trendplus-postgres` (PG 16.13); host port `5434` |
| Source DBs | `trendplus`, `analytics` on `127.0.0.1:5434` |
| Destination DBs | `trendplus_rehearsal_dest`, `analytics_rehearsal_dest` (created for rehearsal) |
| Client path | `TRENDPLUS_PG_DOCKER_CONTAINER=trendplus-postgres` |
| Command | `Invoke-BackupRestoreRehearsal.ps1 -EnvironmentLabel local -AllowDestructiveRestore` |
| StartedUtc | `2026-08-06T07:38:44Z` |
| DurationSeconds | `9` |
| Operational dump | SHA256=`D2B63EFC4EC0B2C998F0592F229ADB590863BA8956D61C05C9E91BA90324BD11`; SizeBytes=`5807290` |
| Analytics dump | SHA256=`7C4A57DBA19AF2E9BBCD24E9C8FE8FD6D6685121D1E82209C3FA0A18F61FAA39`; SizeBytes=`2013481` |
| Restore sections | `pre-data,data` (post-data / MV refresh skipped by default) |
| `pg_restore` errors on clean dest | **None** |
| Schema presence (ops dest) | `112` non-system tables/views |
| Schema presence (analytics dest) | `80` (matches source `80`) |
| Spot check | `prodaja_zaglavlje` source=`3655` dest=`3655` |
| Provider-managed backup retention | **Not verified** (accepted non-P0; needs Render/Neon console) |
| App `/health`/`/ready` against restored URLs | **Not run** (accepted non-P0; requires pointing app at dest URLs) |

### Local rehearsal env shape (no secrets committed)

```text
TRENDPLUS_PG_DOCKER_CONTAINER=trendplus-postgres
TRENDPLUS_*_REHEARSAL_*_URL = Host=127.0.0.1;Port=5434;Database=<name>;Username=...;Password=...
```

## Accepted non-P0 warnings

1. **Provider retention** not recorded from managed console.
2. **post-data** (indexes, FK constraints, materialized view refresh) skipped in default gate run. Earlier attempt with full restore hung ~23+ min on `REFRESH MATERIALIZED VIEW public.mv_supplier_decision_score_cache`. Use `-IncludePostData` for overnight/full proof.
3. **App health** against restored connection strings not exercised in this session; post-restore analytics refresh remains explicitly required before trusting aggregates.

## Post-restore analytics contract (explicit)

After a successful disposable restore:

1. Point app connections at restored URLs only in the disposable environment.
2. Confirm `/health` and `/ready` against those connections.
3. Run analytics refresh (or record that refresh is required before trusting aggregates).
4. Invalidate cache; do not treat Redis/memory cache as restored truth.
5. Record duration, `pg_dump`/`pg_restore` versions, dump SHA256/size, and cleanup of `tmp/ops-rehearsal`.

## Cleanup

- Delete dump files from `tmp/ops-rehearsal/` after evidence fields are copied here.
- Never commit dumps, connection strings, or customer row samples.
- Disposable dest DBs may remain on local Docker for later full `-IncludePostData` runs; drop when finished.
