# Pilot Data Safety Checklist

This document is an internal pilot safety checklist for Trendplus.
It describes what data must be protected, how it is typically backed up and restored, and what manual gaps still need ops coverage.

Important:
- This document does not claim that automated backup, restore, export, or deletion already exists.
- If your hosting layer already provides backups or snapshots, treat that as the source of truth and map the steps below to that platform.

## Stored data

| Data type | Examples | Why it matters |
|---|---|---|
| Operational DB | users, tenants, imports, actions, configuration, background job state | System of record for app behavior and pilot operations |
| Analytics DB | sales analytics, inventory analytics, scorecards, readiness signals, refresh metadata | Drives dashboards and pilot decisioning |
| Import files | CSV/XLSX uploads, staging files, mapped inputs | Needed for re-import, audit, and troubleshooting |
| Reports | exported PDFs, CSVs, print outputs, generated files | Needed for customer handoff and audit trail |
| Logs | app logs, worker logs, import logs, export logs | Needed to investigate failures and verify restore results |
| Cache | analytics cache, refresh cache, stale flags, derived snapshots | Can affect freshness, performance, and perceived state after restore |

## Backup policy

| What to back up | Frequency | Retention | Access |
|---|---|---|---|
| Operational DB | Daily at minimum, and before pilot cutovers or risky maintenance | Keep according to pilot retention policy or hosting policy | Ops or DBA only |
| Analytics DB | Daily at minimum, and after major import batches if that is part of your process | Keep according to pilot retention policy or hosting policy | Ops or DBA only |
| Import files | Preserve after successful ingest if they are needed for reprocessing or audit | At least through pilot window, longer if customer contract requires it | Ops only |
| Reports | Preserve key customer-facing exports if they are part of the deliverable | Per customer retention agreement | Pilot owner, ops, or account team as needed |
| Logs | Keep enough history to debug import, refresh, export, and restore issues | Shorter retention is acceptable if logs are centralized elsewhere | Ops only |
| Cache | Usually not backed up; cache can be rebuilt | No retention expected unless your cache contains durable state | Usually no access needed |

Notes:
- If backups are stored outside the application database, document the storage location separately.
- If access is split across cloud console, database admin tools, and file shares, list each path in the implementation checklist for your environment.
- If a backup tool is not configured, this checklist is incomplete until ops provides one.

## Restore policy

Restore is the process of returning the operational DB and analytics DB to a known-good state after data loss, corruption, failed import, or broken refresh.

### Restore steps

1. Stop or pause writes if the hosting setup allows it.
2. Identify the backup snapshot or dump that matches the recovery point you want.
3. Restore the operational DB first if application state is affected.
4. Restore the analytics DB next if dashboards, refresh metadata, or imported analytics are affected.
5. Restore only the import files or report archives that are required for audit or reprocessing.
6. Clear derived cache after restore if the cache may contain stale or inconsistent state.
7. Run validation queries and smoke tests.
8. Trigger a post-restore analytics refresh if the restored data should rehydrate dashboards.

### Restore validation

After restore, verify:
- row counts for key entities are within expected range
- latest import batch is present or intentionally absent
- analytics refresh metadata shows a valid state
- dashboards and scorecards render without error
- report export still works for at least one representative report

### Post-restore refresh

If analytics data was restored, perform a manual refresh or equivalent rebuild step so cached or derived data matches the restored source tables.

## Export policy

Export is separate from backup.

### Supported export types

- Reports for customers or internal review, typically PDF or CSV
- Raw data extracts from operational tables or analytics tables, typically CSV or database dump

### Export guidance

- Use report exports when the goal is to share a decision artifact.
- Use raw data exports when the goal is to reprocess, audit, or compare imported records.
- If export data contains personal or commercial sensitive information, treat it as controlled access data.
- Do not assume an export is a backup. Exports are point-in-time artifacts, not a full recovery plan.

## Delete policy

Delete is the process of cleaning up pilot data after the pilot ends or when a customer requests removal.

### What should be considered for cleanup

- operational records related only to the pilot
- analytics records for that pilot
- import files and staging files
- generated reports and exports
- logs that are outside the required retention window
- cache entries and derived snapshots

### Manual gaps

- There may be no automated delete flow today.
- File system cleanup may be manual.
- Object storage cleanup may be manual.
- Report archives may live outside the app and need separate deletion.
- Logs may be retained centrally even after app-level cleanup.

### Retention notes

- Keep only what is required by contract, audit, or security policy.
- If retention is undefined, document the gap before the pilot starts.
- If a customer asks for deletion, confirm which systems are in scope before you execute cleanup.

## Checklist summary

- [ ] Operational DB backup path documented
- [ ] Analytics DB backup path documented
- [ ] Import file storage location documented
- [ ] Report export retention documented
- [ ] Log retention documented
- [ ] Cache rebuild behavior documented
- [ ] Restore validation steps documented
- [ ] Post-restore refresh step documented
- [ ] Manual delete gaps documented
