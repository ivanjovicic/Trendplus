# Analytics Reliability Retrospective Audit — 2026-08-23

Status: current retrospective pointer; historical run logs remain immutable evidence snapshots.
Verified delivery baseline: `main` / `origin/main` at `0794cfc61250c23d3377b0c8670c830b21d32152`.

## Purpose

This audit compares the recent analytics reliability commits, their durable run logs, the active prompt queue, and the master roadmap. It records only concrete residual gaps or evidence weaknesses that can affect data accuracy, provenance, or operator trust.

## Evidence reviewed

- `.ai/runs/2026-08-22-RQ108-evidence.md`
- `.ai/runs/2026-08-22-RQ109-evidence.md`
- `.ai/runs/2026-08-22-RQ110-evidence.md`
- `.ai/runs/2026-08-22-large-commit-review-evidence.md`
- `.ai/runs/2026-08-23-direct-analytics-prompt-seeding-evidence.md`
- `docs/qa/ANALYTICS_PILOT_SCREEN_DATA_AVAILABILITY_MATRIX.md`
- `docs/qa/ANALYTICS_DATASCOPE_CONSISTENCY_AUDIT.md`
- `docs/qa/ANALYTICS_CACHE_INVALIDATION_AUDIT.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- `MASTER_ROADMAP.md`

## Findings and proof strength

| Finding | Evidence strength | Current conclusion | Routed follow-up |
|---|---|---|---|
| Dashboard lacks an isolated seeded non-empty backend proof. | High: explicitly recorded by RQ110 and repeated in the pilot matrix. | Dashboard visibility is not yet proven from an authoritative source basis. | RQ115 |
| Refresh/cache/materialized-view parity is not yet executed after RQ110. | High: RQ111 is still WAITING. | A visible screen can still be stale or blank after refresh churn. | RQ111, then RQ112 |
| Decision Pulse has no live SMTP receipt proof and no complete attempt-state evidence. | High: RQ109 says live SMTP was not run. | Local scheduling/build proof is not delivery proof; missing config must remain explicit. | RQ116 |
| Forecast/observed pairing remains dependent on observed daily stock availability. | High: RQ108 residual risk and RQ96 contract. | No observed window must remain `missing_relation`/`unavailable`, not zero or trusted. | RQ117 |
| Data Quality issues handler now scopes `sales_30d` by sale-header origin. | RQ118 closed the RQ06-F1 residual in the dataScope audit and queue notes. | Scoped issue revenue no longer crosses data origins in this handler. | RQ118 DONE |
| PDC dual-origin now has an explicit scope/provenance contract; inventory/Decision Board forced-all remains open. | Medium: RQ119 closed the PDC half, but RQ05-F2 still stands in both scope audits. | Like-for-like comparisons are now explicit for PDC, but not yet for every inventory family. | RQ119 / RQ05-F2 |
| Backend trust metadata is not uniformly visible in pilot UI. | Medium: earlier contracts added fields; RQ104 covers only selected core pages. | The first reconciled/provenance-backed family still needs explicit UI mapping. | RQ120 |
| Historical logs contain pending delivery fields after later synchronization. | High as a process finding, not a product defect: old logs are immutable snapshots. | Do not rewrite history; use this audit and current SHA as the authoritative pointer. | Governance only |
| Tracked task-lock files are stale (claimed 2026-08-06/10) although their prompts are DONE or no longer active. | High: files are present under `.ai/task-locks` and are tracked. | They can block or mislead future agents and violate the lock protocol. | Remove stale locks in this delivery |

## Plan corrections

- The analytics queue declares no separate READY prompt while `RQ110` is `IN_PROGRESS`; `RQ111`–`RQ120` remain `WAITING` until their dependencies and owner promotion rules are met.
- The master roadmap and compact priority review must point to the current 2026-08-23 truth, not the historical BCI/STAB/QDB READY state from 2026-08-20.
- No prompt below claims live production SMTP, production database freshness, or a missing observed history that was not actually tested.

## Accuracy and proof assessment

- Focused backend Decision Pulse/pulse-scheduler changes on `main` have targeted test evidence; the frontend Vitest proof is environment-sensitive because the local `clientapp` dependency set is incomplete.
- Prompt/plan validators are executable proof for queue consistency, not proof that analytics values are correct in production.
- Current pilot non-empty, refresh, summary/detail/export, provenance, and UI metadata confidence remains staged: runtime confidence is low/unknown until the corresponding prompts produce deterministic fixtures and current-main evidence.
- Historical “pending” fields in dated run logs are not silently upgraded; the verified `0794cfc` SHA and this audit are the current delivery evidence.

## Next owner sequence

`RQ110` close → `RQ111` refresh/cache parity → `RQ112` summary/detail/export reconciliation → `RQ113` provenance → `RQ114` reusable seed pack, with the residual closures `RQ115`–`RQ120` promoted only when their named dependencies/owner gates are satisfied.
