# Analytics pilot readiness gap assessment - 2026-09-01

Status: current repository assessment; not a release approval

## Executive verdict

Trendplus has a strong code-level safety foundation, but it is **not ready to claim trusted pilot decisions or 100% verified data**. The repository contains explicit contracts for unknown versus zero, freshness, fallback, recommendation gating, outcomes, cache invalidation, workers and UI trust states. The missing part is continuous, production-backed proof that the same truth travels from the retail source through import, refresh, cache, API, UI, notification and measured outcome.

`100% accurate` is a release claim only after a bounded reconciliation contract defines the source window, identifiers, expected totals, tolerance, owner and evidence artifact. Code and unit tests alone cannot establish that claim for live customer data.

## Operator-provided current facts

The operator supplied the following facts on 2026-09-01. They are input to the next verification run, not yet independently verified evidence:

| Fact | Current value | Verification consequence |
| --- | --- | --- |
| Pilot tenant | `Trendplus` | Treat as one dedicated pilot scope; do not infer shared-SaaS tenant identity from the name. |
| Pilot stores | One retail store: `StoreId=2082886995`, `Trend PLUS 1`, `Trgovačka 30B` | Store identity is now resolved in `StoresDim`; reconciliation must still use the numeric ID, not the name. |
| Candidate window | `2026-08-01` through `2026-08-31`, inclusive | The source currently has sales only through `2026-08-05` for this store, so August is not yet a complete observed pilot window. Use `[2026-08-01T00:00:00, 2026-09-01T00:00:00)` only after the owner accepts the five-day observed window or supplies the missing source data. |
| Source data | Test user and data are available; changes are currently permitted | This permits a controlled test workflow, but does not authorize destructive production changes or turn a write-capable credential into read-only evidence. |
| Analytics database | Neon PostgreSQL; storage `0.54 / 0.5 GB` and dashboard reports `100%` storage usage | P0 storage triage is required before refresh/reconciliation. Do not delete commercial facts, snapshots or audit history without table-size evidence, backup/reference and explicit owner approval. |
| Web deployment | Render service `trendplus-api`, live SHA `d38aafd405a9213a279bb76664cde4bf69ddf83b` | Record this SHA as the current web baseline; repository configuration is not runtime proof for worker parity. |
| Worker deployment | `trendplus-worker` is defined in `render.yaml`, but its live existence/plan is unconfirmed | If the free plan prevents the worker, record the provider limitation and keep refresh-dependent pilot decisions blocked. Do not run heavy workers in the web process. |

The database credentials shared during the intake are considered compromised and must be rotated before any further use. They are intentionally not reproduced in this report or in a run log. The replacement access contract is: a dedicated read-only source credential, a dedicated read-only analytics/audit credential, and a separate pilot login stored outside the repository.

## P0 Neon storage triage

The Neon dashboard reports `0.54 GB` used against a `0.5 GB` allowance. This is a capacity incident, not evidence that any particular table is disposable. The safe order is:

1. restore enough capacity or access to run read-only size queries;
2. measure table, index and toast size in the analytics database;
3. classify candidates as disposable test data, rebuildable cache, retention-eligible operational logs, or authoritative commercial/audit data;
4. preserve a backup/snapshot or export reference and obtain the data owner's deletion approval;
5. execute the smallest approved cleanup, run `VACUUM (ANALYZE)` only as an approved maintenance action, and re-measure;
6. rerun analytics/data-quality checks and record what was removed and what was intentionally retained.

The first read-only size query is:

```sql
SELECT
    n.nspname AS schema_name,
    c.relname AS table_name,
    pg_size_pretty(pg_total_relation_size(c.oid)) AS total_size,
    pg_total_relation_size(c.oid) AS total_bytes,
    pg_size_pretty(pg_relation_size(c.oid)) AS table_size,
    pg_size_pretty(pg_indexes_size(c.oid)) AS index_size
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE c.relkind IN ('r', 'm')
  AND n.nspname NOT IN ('pg_catalog', 'information_schema')
ORDER BY pg_total_relation_size(c.oid) DESC;
```

Until this inventory is captured, no deletion target is approved. In particular, do not delete `SalesFacts`, `SalesLineFacts`, `ReturnFacts`, `StoresDim`, refresh/data-quality history, outbox records or audit records merely because they are old or large; each has different correctness, recovery and evidence implications.

### First archive measurement received

The operator's first read-only measurement reported `582,788` rows in `deleted_rows_archive`, from `2026-04-04 10:21:42 UTC` through `2026-05-06 16:06:25 UTC`, occupying `198 MB`. A follow-up check showed `582,263` rows linked to known cleanup batches and `525` rows with `batch_id IS NULL`; no rows were outside the cleanup-batch set. All 525 unlinked rows were written at `2026-04-04 10:21:42 UTC` by `neondb_owner` with reason `cleanup-non-access`, covering 386 `prodaja_stavke`, 137 `prodaja_zaglavlje`, one `povracaj_stavke` and one `povracaj_zaglavlje` row. The operator then confirmed the approved archive-only cleanup: `remaining_archive_rows=0`, while current business row counts remained non-zero (`prodaja_stavke=67,144`, `prodaja_zaglavlje=5,550`, `DnevnikPromena=67,629`).

The archive cleanup is complete. The remaining P0 storage proof is the new Neon project usage metric; the cleanup result alone does not prove the provider quota has recovered.

## Evidence observed

| Area | What exists in code/docs | What is still unproven or unsafe |
| --- | --- | --- |
| Analytics contracts | The safety gate and analytics standards require null/unknown to remain distinct from zero, explicit freshness/fallback, and backend-owned decisions. | No current production reconciliation proves endpoint totals, units and row lineage against the retailer source for a fixed window. |
| Test foundation | `Api.Tests` has 123 `*Tests.cs` files / 887 `[Fact]` or `[Theory]` occurrences; the client has 100 specs / 442 `it` or `test` occurrences. Focused tests cover workers, cached endpoints, Decision Board and outcome states. | Counts are not coverage. A shared deterministic gold dataset and cross-surface differential test pack are not yet the release authority. |
| Refresh and cache | `WorkerRuntimeConfig`, worker registry, refresh-status contract and analytics cache-family invalidation exist. The 2026-09-01 local RQ134/RQ135 work adds worker cache parity tests. | The 2026-08-27 live audit found no registered dedicated worker, unknown freshness and no durable successful-job evidence. Local uncommitted work is not production proof. |
| Decision safety | Product, inventory and Decision Board code/tests are designed to fail closed for insufficient evidence; action/outcome models distinguish pending and not measured. | Production must be rechecked at the deployed SHA. The audit observed stale/insufficient PDC rows that were still actionable on the deployed runtime. |
| UI trust | Core pages use `AnalyticsTrustHeader`, `AnalyticsErrorState` and `AnalyticsEmptyState`; frontend guardrails prohibit browser-owned recommendation logic. | Some secondary trust-header inputs are intentionally unknown/null, and there is no fresh authenticated browser proof that all key state combinations render correctly on the deployed build. |
| Messages and notifications | Transactional outbox, `OutboxProcessorWorker`, Decision Pulse scheduler, worker-status UI, inventory alerts and toast messages exist. | There is no single delivery contract proving recipient authorization, idempotency, payload truth, retry/dead-letter state and a user-visible message that does not imply a completed action when it was only queued. |
| Operations | Monitoring, alerting, operator runbook, backup/recovery and release-check documents exist. | The authoritative business milestone still requires current deploy parity, worker/import/refresh monitoring and read-only production reconciliation. STAB16 is blocked on provider worker access and the audit DB connection. |

## Pilot blockers, ordered

1. **P0 - source-to-screen proof is absent.** `STAB16` needs a read-only production source/analytics connection and deployed worker access. Until then, KPI correctness is asserted, not independently reconciled.
2. **P0 - freshness is operationally unproven.** Production evidence observed `workersEnabled=false`, unknown freshness and missing successful-job records. The code supports a dedicated worker process, but provider configuration and durable execution need proof.
3. **P0 - deployment/actionability parity is unproven.** Local fixes do not clear the released Product Decision Center behavior until the exact deploy SHA, critical/stale rows and Decision Board aggregation are checked together.
4. **P1 - notification truth is not end-to-end proven.** An outbox can exist while sending the wrong message, to the wrong recipient, twice, or while UI wording overstates delivery/execution.
5. **P1 - outcome learning lacks a business measurement protocol.** The model keeps `not_measured` distinct, but pilot acceptance still needs a controlled definition of expected impact, observed outcome, comparison window and approval responsibility.
6. **P1 - cross-surface parity needs executable gold evidence.** API, table, detail, export, report, action and notification should be checked against one immutable fixture rather than separate ad-hoc expectations.

## Immediate execution decision

The proposed first pilot window is the closed month of August 2026, but it remains `PROPOSED` until the source contains rows for the selected store and the business owner approves the window. The first missing technical fact is the exact `StoreId`; resolve it from the store master/source export and record the mapping before querying metrics.

No queue prompt is promoted to `READY` by this report. The canonical queues currently declare no runnable STAB/RQ prompt, while `STAB16` is now blocked by three concrete external prerequisites: provider worker access, read-only audit access and Neon storage capacity. `RQ128` depends on that proof. Promoting a later live prompt now would bypass the dependency and could produce false freshness or accuracy evidence.

Work that can proceed as direct, local, non-production preparation is limited to:

- creating the versioned gold-dataset assertions without customer secrets;
- writing the KPI decision dictionary for business approval;
- preparing the reconciliation query/manifest for the confirmed `StoreId` and August window;
- adding deterministic tests for notification wording and cross-surface unknown/zero/stale behavior.

These are preparation activities, not pilot approval and not a substitute for the blocked STAB16 proof.

## Minimum pilot gate

The pilot may be called `Pilot Ready` only when all of these are true for one signed, bounded source window:

- deployed backend and frontend identifiers are recorded and match the approved release;
- a dedicated worker has a successful, timestamped refresh and the freshness contract reports it honestly;
- source, import, snapshot and endpoint reconciliation results are attached, with any variance classified and approved;
- false-actionability, unknown/zero and stale/fallback counterexamples pass on the deployed API and authenticated UI;
- one notification is traced from event through outbox/delivery to the authorized user, and its wording matches actual state;
- a representative action is recorded, measured using a defined method and kept distinct from an unmeasured or qualitative outcome;
- rollback, alert and owner escalation are exercised, not merely documented.

## Scope boundary

This assessment does not claim that the live provider is broken, that all historical audits remain current, or that source data is inaccurate. It identifies the evidence required to make a defensible accuracy and pilot-readiness claim.

## Next planning artifact

`docs/planning/ANALYTICS_PILOT_READINESS_PROMPT_PACK_2026-09-01.md` contains 21 `WAITING` candidates, including the storage-budget prevention prompt `PILOT-NET-05`. They are planning candidates only; promotion follows `MASTER_ROADMAP.md`, the named owner queue and the existing `STAB16` gate.
