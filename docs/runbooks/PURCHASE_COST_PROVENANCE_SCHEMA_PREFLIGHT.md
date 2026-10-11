# Purchase cost provenance schema preflight

This procedure is a release gate for `prodaja_stavke.purchase_cost_provenance`. The preflight is read-only. It prints one JSON object and exits `0` only for a compatible schema; `2` means `missing` or `unknown`. It never prints the connection string or raw database errors.

Requirements: Node.js and PostgreSQL client tools (`psql`, `pg_dump`, `pg_restore`) available on `PATH`.

`/ready` is an application/startup and database-connectivity signal. It does not inspect this column, so a healthy `/ready` response is not proof that provenance-dependent endpoints can query the live schema. Run this preflight against the exact target selected for migration and again after migration.

## 1. Confirm the target and take a backup

Use an operator-approved maintenance window and the exact database target authorized for this release. Never point this procedure at the local `trendplus` business database, Render, Neon, or another business database without separate authorization. The repository task does not grant migration or deployment authority.

Set the connection in the current PowerShell session. Keep the command out of tickets, transcripts and screenshots. A libpq URI is supported, as is a semicolon-delimited Npgsql connection string:

```powershell
$env:TRENDPLUS_PREFLIGHT_CONNECTION = 'Host=<approved-host>;Port=5432;Database=<approved-db>;Username=<operator>;Password=<secret>;SSL Mode=Require'
$env:ConnectionStrings__DefaultConnection = $env:TRENDPLUS_PREFLIGHT_CONNECTION
```

The script reads credentials from the environment and passes connection parameters to `psql` through its environment, not command-line arguments. Configure `psql` and `pg_dump` for the same target using normal libpq environment variables or an approved password file, then make and verify a custom-format backup. Enter the password at the tool prompt or use an approved `PGPASSFILE`; do not place it in a command line:

```powershell
$env:PGHOST = '<approved-host>'
$env:PGPORT = '5432'
$env:PGDATABASE = '<approved-db>'
$env:PGUSER = '<operator>'
$backup = Join-Path $PWD 'purchase-cost-provenance-before.dump'
pg_dump --format=custom --file $backup
if ($LASTEXITCODE -ne 0) { throw 'Backup failed; stop before migration.' }
pg_restore --list $backup | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Backup verification failed; stop before migration.' }
```

## 2. Read-only preflight and SQL preview

Run from the repository root. Save the JSON with the change record after reviewing the target identity (`host`, `port`, `database`, `user`):

```powershell
node tools/purchase-cost-provenance-preflight.mjs
if ($LASTEXITCODE -ne 0) { throw 'Schema preflight is not ready; stop and investigate.' }
```

The expected column is nullable `character varying(64)`. The report separately identifies whether EF recorded migration `20261010160000_AddPurchaseCostProvenance`; a compatible column without that history entry is reported as `bootstrap_or_unrecorded`. `unknown` is not approval to proceed.

Inspect the exact additive EF SQL before execution. Substitute the immediately preceding migration ID from `dotnet ef migrations list`:

```powershell
dotnet ef migrations script <previous-migration-id> 20261010160000_AddPurchaseCostProvenance `
  --project .\Infrastructure\Infrastructure.csproj `
  --startup-project .\Api\Api.csproj `
  --context TrendplusDbContext `
  --output .\purchase-cost-provenance-preview.sql
```

Review the generated SQL and confirm it adds only the nullable provenance column. Do not execute the preview file as part of this read-only step.

## 3. Separately authorized migration and verification

Only after the database owner has explicitly approved the target, backup and preview, execute the canonical EF migration:

```powershell
dotnet ef database update 20261010160000_AddPurchaseCostProvenance `
  --project .\Infrastructure\Infrastructure.csproj `
  --startup-project .\Api\Api.csproj `
  --context TrendplusDbContext
if ($LASTEXITCODE -ne 0) { throw 'Migration failed; follow the approved incident/rollback plan.' }
node tools/purchase-cost-provenance-preflight.mjs
if ($LASTEXITCODE -ne 0) { throw 'Post-migration schema verification failed.' }
```

The migration is additive and idempotent. It does not rewrite `nabavna_cena`; existing rows may retain NULL provenance. An `unknown_provenance` value remains an explicit unknown and must not be treated as a confirmed historical cost or as zero. Record both JSON reports, command exit codes, migration SHA and database target identity in the operator change record without recording credentials.

## 4. Rollback and recovery

The EF `Down` migration intentionally does not drop this metadata column. Do not manually drop it: that would discard provenance. If verification fails, stop application rollout, keep the column, retain the backup and follow the separately authorized incident procedure. A code/application rollback may proceed only if the previous code is compatible with the additive column. Restoring the database from backup is a separate owner-approved destructive operation; first verify the backup, target identity and recovery point with the database owner. Never run a restore or migration against a business database as part of this repository workflow.

## Contract limits

- This script checks the current schema and EF history on one explicitly configured target; it makes no writes.
- A successful `/ready` response only proves the API's existing connectivity/startup contract, not the presence or compatibility of this column.
- This runbook does not authorize a migration, deployment, local business database mutation or change to Render's manual-deploy policy.
