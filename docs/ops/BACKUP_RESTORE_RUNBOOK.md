# Backup and Restore Runbook

This runbook is a pilot-facing operational guide for backup and restore.
It uses placeholders because the exact backup tool depends on hosting and database platform.

Important:
- Do not treat these commands as proof that backups already exist.
- Replace placeholders with the actual commands for your environment.
- If your platform uses snapshots, managed backups, or a different database engine, adapt the steps accordingly.

## Pre-check

Before any backup or restore:

- Confirm the environment: dev, staging, or pilot customer environment.
- Confirm the database target: operational DB, analytics DB, or both.
- Confirm whether imports, reports, or logs also need to be preserved.
- Confirm whether writes should be paused.
- Confirm the recovery point and who approved it.
- Confirm access to the backup location and restore target.
- Confirm whether a post-restore refresh is required.

## Backup command placeholders

Use the command style that matches your database platform.

```bash
# Operational DB placeholder
pg_dump --format=custom --file=<backup-file> <operational-db-name>

# Analytics DB placeholder
pg_dump --format=custom --file=<backup-file> <analytics-db-name>

# If your platform uses a snapshot or managed backup, replace the command above with that tool.
```

Backup checklist:

- Capture the operational DB if app state matters.
- Capture the analytics DB if dashboards, scorecards, or refresh metadata matter.
- Preserve import files if they are needed for replay or audit.
- Preserve report exports if the pilot requires handoff artifacts.
- Record the timestamp, backup target, and operator name.

## Restore command placeholders

```bash
# Restore operational DB placeholder
pg_restore --clean --no-owner --dbname=<operational-db-name> <backup-file>

# Restore analytics DB placeholder
pg_restore --clean --no-owner --dbname=<analytics-db-name> <backup-file>

# Replace with platform-specific restore steps if you do not use PostgreSQL-style dumps.
```

Restore checklist:

- Restore the minimal scope needed for the incident.
- Restore the operational DB before the analytics DB if app state affects analytics processing.
- Restore analytics DB data before triggering refresh or cache rebuild.
- Recreate or re-link any file paths needed for imports or reports.
- Clear stale cache after restore if cached data could conflict with restored tables.

## Validation queries checklist

Run validation queries that fit your schema. Examples:

```sql
-- Basic presence checks
SELECT COUNT(*) FROM <operational_table>;
SELECT COUNT(*) FROM <analytics_table>;

-- Date range checks
SELECT MIN(sale_date), MAX(sale_date) FROM <sales_table>;

-- Last import status
SELECT status, created_at FROM <import_batch_table> ORDER BY created_at DESC LIMIT 1;

-- Last refresh status
SELECT status, completed_at FROM <refresh_table> ORDER BY completed_at DESC LIMIT 1;
```

Validation checklist:

- Key table row counts are plausible.
- Latest import status is expected.
- Latest refresh status is expected.
- Dashboard pages render without obvious error state.
- A representative report export works.
- If applicable, a supplier or inventory signal still matches expected totals.

## Post-restore analytics refresh

After a successful restore:

1. Refresh analytics or rebuild derived tables if the platform requires it.
2. Clear analytics cache if the restored data could conflict with cached state.
3. Re-run a smoke check on dashboard freshness.
4. Confirm the latest refresh status is recorded.
5. Inform the pilot owner that data is back in a known state.

## Rollback notes

If the restore introduces unexpected data loss or corruption:

- Stop further writes if possible.
- Keep the pre-restore backup available until the issue is resolved.
- Roll back to the prior backup or snapshot if the restore point is incorrect.
- Document exactly what was restored, what was not restored, and what remained manual.
- If cache or derived state was rebuilt incorrectly, clear it and re-run refresh.

## Open gaps to document in your environment

- Whether backups are automated or manual.
- Where backups are stored.
- Who can access backups and restores.
- Whether import files are retained.
- Whether report exports are archived.
- Whether logs are centralized outside the app.
- Whether restore requires a support window or downtime.
