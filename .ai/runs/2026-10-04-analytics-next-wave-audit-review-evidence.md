# Evidence — analytics next-wave audit review/corrections 2026-10-04

Task: verify the claims in the next-wave reliability/value audit originally delivered as `8ae06a508ae8f98163015c869a816e2a3b7a11d3`, correct overclaims, improve prompt boundaries and add only evidence-backed missing owners.

Delivery target: `main`
Runtime code changed: no
Deployment triggered intentionally: no
Original audit live runtime evidence: `02f9915887f99115348bd241590da581dafee45d` (retained; not re-probed in this connector-only review)

## Verdict

The audit is materially useful and most retained live observations remain valid. The strongest reliable production statement is the fixed July cross-screen parity window (1,561,120 RSD / 307 units). The audit needed corrections in causal language, worker inference, outcome-prompt dependencies, Data Quality period semantics and performance routing.

## Verified section-level queue truth

- RQ142: OBSOLETE
- RQ147: DONE
- RQ148: DONE
- RQ149: DONE
- RQ150: OBSOLETE
- RQ479: READY P2
- RQ586: READY P3
- RQ587: READY P1
- RQ588: READY P3
- RQ585: WAITING on RQ570/RQ573/RQ574/RQ576 + RQ583 freshness, not on Actions outcomes
- RQ557: WAITING only for RQ553 shared Pre/Post ownership after scope repair
- RQ558: WAITING after RQ557 plus explicit mature/control coverage and owner promotion
- PERF19: WAITING after RQ573

## Corrections

### 1. Startup/readiness claim narrowed

Current code proves that a required strict database initializer cannot still be pending/throwing while readiness reports clean ready. However, `ready=true` plus missing analytics objects does not uniquely prove which Render flag is disabled.

The retained evidence is compatible with multiple hypotheses:
- AutoMigrate not effective;
- non-strict/FailFast behavior allowed initialization to return after errors;
- a different effective config/connection/runtime path;
- post-readiness schema drift.

RQ587 now reports a safe startup-init outcome on public readiness while exact effective config/stage belongs to an admin-authorized diagnostic. `/api/runtime/version` remains deployment identity. Startup `succeeded` is explicitly not current schema certification.

### 2. Worker inference corrected

`AnalyticsRefreshStatusService` first loads durable `AnalyticsRefreshRuns` from the analytics database. Only when no matching durable run exists does it fall back to process-local worker health.

Therefore:
- `processType=web` / `workersEnabled=false` correctly says heavy workers are not registered in the web process;
- it does not prove a separate Render worker service is absent;
- the actionable production truth is that required job families currently lack usable durable success evidence visible to the API.

STAB16 was corrected accordingly.

### 3. Data Quality historical period defect made explicit

`/api/analytics/data-quality/health` currently declares `lookbackDays` and `dataScope`, not `fromDate/toDate`. Extra date query keys are silently ignored and `DataQualitySalesWindow.Resolve` anchors the health window to wall-clock today.

RQ578 now requires a real explicit historical-period contract, validation, backend population binding and frontend displayed-period parity.

### 4. Decision Board performance routed by measurement

Retained evidence measured Decision Board around 17.3 s, while the existing budget targets p95 <=2 s. The server composes Product Decision, inventory, supplier, Actions, refresh and Data Quality sequentially, but there is no deployed per-contributor timing proving PDC alone causes the full latency.

RQ573 now measures PDC and Board before/after its payload/order work. New PERF19 waits for RQ573, then profiles contributors and accepts only the smallest measured optimization with exact business/trust parity. No speculative index/cache/concurrency work is authorized.

### 5. Smoke action quarantine hardened

RQ479 now requires one shared positive-only fixture predicate across operational list/count/outcome/Board projections. An exact reserved `:smoke:` key segment or explicit fixture marker may classify a fixture; missing optional evidence, old dates or titles may not.

No anonymous `includeFixtures=true` bypass is authorized. Diagnostic fixture visibility must use an existing admin-authorized path if retained. Production deletion is still out of scope.

### 6. RQ557/RQ558/RQ585 dependency repair

The original audit incorrectly treated the empty Analytics Actions ledger as a blocker for all outcome/value work.

- RQ557 consumes Nivelacija price events/sales/cost evidence, not Analytics Actions. It is now a descriptive mature-markdown outcome ledger only: no causal uplift, no repeat/avoid label, no elasticity/scoring feedback. Historical stock fields are nullable unless a certified dated-stock source exists.
- RQ558 remains later controlled-effect work only after RQ557 and measured mature/control/category coverage; RL12 remains the causal-claim gate.
- RQ585 is a current certified-signal weekly digest and retains only its existing signal/freshness dependencies. It does not wait for measured Actions outcomes.

### 7. Migration guard made safer

RQ588 now tests EF migration discovery semantics rather than merely raw reflection attributes. Production-intended migrations must be discoverable or sit on an explicit reviewed allowlist with owner/reason/sunset. The four current legacy classes are classified before deletion; attributes are not blindly added and no DDL is executed by this prompt.

The prior statement that the missing orphan index migrations have "no measurable performance impact" was removed because the audit did not benchmark that delta.

### 8. Historical master/as-of conclusion refined

Fresh import is not required to analyze as-of risk. Existing RQ411 already protects Supplier and Shoe Type history with sale-time attribution fields and explicit `AttributionBasis`; legacy backfill is marked `frozen_current_master_backfill`.

Remaining poorly populated dimensions such as category/color do not justify a new SCD runtime project now. A future owner must first decide `current_master` vs `as_of_sale` semantics when those sources become populated.

### 9. Routing correction

Canonical P-UI READY is:
- P-UI-39 primary
- P-UI-40
- P-UI-41
- P-UI-47
- P-UI-49

P-UI-45 is WAITING behind P-UI-40 + P-UI-48.

RQ primary remains RQ569. Additional READY lanes remain RQ553, RQ574, RQ578, RQ580, RQ581, RQ586 (P3), RQ587, RQ588 (P3) and RQ479 (P2), subject to fresh claim-time collisions.

## Post-review commits

- `2ac56f00f90498618c27cbd5337cba6f0a09c5b8` — parent RQ evidence/routing, RQ479/RQ573/RQ578 corrections
- `147506caf6740eac8bd885136b7c692cf0570ccc` — RQ587/RQ588 hardening + RQ585 correction
- `ebb121897476e045bd501a8d4cb9c0796707d8c9` — RQ545/RQ557/RQ558 repair
- `55c8e059e99d2176451a798e12fd51d47d73d316` — STAB16 worker/startup inference correction
- `e6d923de74371c8bbb0222094385ac1b5b45835b` — PERF19 registration
- `b3bc7ad2289c8e6eb456466bdae2915107a52e1c` — next-wave audit correction
- `c56d93bf3cb162d45eddc5449e62887270306091`, `de228b39d1e039014e22d77baa1d2c4d7f7f29d0` — original evidence synchronization/corrections
- `c806dace3c01ed820ec4eb31664877024639afa7`, `08a3aaf3a3685553bac80b38e702a89c34053125`, `11842106867241b65be18382a4175b555816b736` — performance/master routing sync
- `837ac370515883e3296e51f6bcba6deb083f412f` — RQ587/RQ588 metadata alignment

## Validation scope

This review changed documentation, queues and roadmaps only. It did not change frontend/backend runtime source. The original audit recorded all three governance validators plus `git diff --check` as passing at delivery. This connector-only post-review directly re-read canonical files and section statuses on current `main`; it did not execute the local Node/.NET/frontend suites and does not claim that it did.

The original 16 read-only live GET results are retained evidence from 22:50–22:56 CEST on runtime `02f99158`; they were not independently re-executed in this review environment.

## Residual external evidence

- effective Render AutoMigrate/FailFast/RunDatabaseInitialization state;
- provider startup log correlated to the retained readiness timestamp;
- whether a separate worker service currently exists/runs;
- durable successful refresh run history after worker/import restoration;
- exact cause of Pulse missing source;
- live database reconciliation and deployed browser/export proof;
- dominant deployed Decision Board contributor after RQ573.

## Next

- Global reliability priority: RQ569.
- High-value independent repository lane: RQ587.
- Cheap data-hygiene lane: RQ479.
- Data Quality correctness lane: RQ578.
- Do not claim PERF19 until RQ573 is DONE.
- Owner/provider step: capture config/worker/log evidence before changing startup strictness.
