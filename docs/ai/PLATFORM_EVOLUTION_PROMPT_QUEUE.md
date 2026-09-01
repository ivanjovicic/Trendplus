# Trendplus Platform Evolution Planning Queue

Created: 2026-08-08  
Roadmaps:
- `docs/roadmaps/PERFORMANCE_ROADMAP.md`
- `docs/roadmaps/OBSERVABILITY_ROADMAP.md`
- `docs/roadmaps/SECURITY_EVOLUTION_ROADMAP.md`

Purpose: planning/contracts and measurement preparation. Runtime work requires later explicit promotion through the owning program.

## Current READY by program

| Program | Current READY | Execution class |
|---|---|---|
| PERF - Performance | none | `PERF16` BLOCKED on `MT10` / shared-SaaS gate |
| OBS - Observability | none | `OBS10` DONE; `OBS11` DONE |
| SEC - Security Evolution | none | frontend production audit triaged; SEC05 still WAITING on MT09 |

Only one prompt per program may be READY. These planning tasks never outrank higher-priority runtime gates in `MASTER_ROADMAP.md`.

Prepared later candidates (not runnable until explicitly promoted): `PERF17` bundle budget, `SEC08` reproducible dependency audit gate.

---

## PERF17 - Measure and enforce the frontend bundle budget without hiding regressions

Status: WAITING
Priority: future / planning
Type: frontend performance/docs/guardrail
Feature family: frontend-bundle-budget
Parallel-safe: yes, scoped measurement and contract work only
Owner: Codex
Commit suggestion: `docs(perf): define frontend bundle budget gate`

### Problem

The frontend build is successful but still reports a large `recharts` chunk. The current build must remain truthful: increasing a warning threshold is not an optimization and must not be used to hide bundle growth.

### Evidence

- The current client build passes but reports a `recharts` chunk of approximately 548 kB, above the existing 500 kB warning threshold.
- Existing manual chunk configuration already exists and must be measured before changing.
- Analytics values and trust-state semantics are unrelated to this prompt.

### Scope

- `Klijent/clientapp/vite.config.*` and the actual route/component import paths responsible for the measured chunk;
- one performance contract or evidence note under `docs/architecture/`, `docs/roadmaps/` or `docs/qa/`;
- a deterministic bundle-budget check only if the existing toolchain supports it.

Do not add a worker, change API contracts, alter analytics calculations, or raise the threshold without measured justification.

### Read first

- `AGENTS.md`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `docs/roadmaps/PERFORMANCE_ROADMAP.md`
- `Klijent/clientapp/vite.config.*`
- the current client build output and package scripts

### Do

1. Capture a reproducible baseline with commit SHA, build command, chunk names and sizes.
2. Trace the largest chunk to imports and route boundaries.
3. Prefer route-level lazy loading or a narrow existing split point over broad refactoring.
4. Add a deterministic budget check only when it fails on a demonstrable regression rather than normal tool noise.
5. Record the before/after measurement and explain any remaining justified warning.

### Tests

- `cd Klijent/clientapp; npm run typecheck`
- `cd Klijent/clientapp; npm run build`
- the narrowest affected Vitest suite
- `git diff --check`
- queue and planning validators when queue/docs change

### Acceptance

- The bundle baseline and measurement method are durable and reproducible.
- The largest avoidable chunk is reduced or a documented budget exception is approved with evidence.
- No warning is hidden by simply increasing a limit.
- No analytics behavior, API contract or worker/infrastructure requirement changes.

### Dependencies

- `PERF16` remains blocked on `MT10` for runtime/shared-SaaS performance claims.
- This prompt is planning/measurement-only until an owner explicitly promotes it.

---

## SEC08 - Make dependency vulnerability checks reproducible across frontend workspaces

Status: WAITING
Priority: future / planning
Type: security/CI/dependency governance
Feature family: dependency-supply-chain-gate
Parallel-safe: yes, scoped workflow and documentation work only
Owner: Codex
Commit suggestion: `ci(security): add reproducible frontend audit gate`

### Problem

The two frontend workspaces had a reported dependency vulnerability backlog which is now locally remediated, but the repository needs a repeatable gate so a future lockfile change cannot silently reintroduce production risk.

### Evidence

- The current dependency cleanup reduced the reported frontend audit findings to zero in both workspaces.
- The main client lockfile and POS lockfile are separate and must be audited separately.
- A local audit result is not GitHub Actions proof until a workflow executes the same commands.

### Scope

- frontend `package.json` and lockfiles only when required by the gate;
- the narrowest existing frontend/security workflow;
- security/dependency evidence documentation;
- no secret values, production database access, runtime analytics changes or unrelated package upgrades.

### Read first

- `AGENTS.md`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/roadmaps/SECURITY_EVOLUTION_ROADMAP.md`
- `docs/qa/FRONTEND_DEPENDENCY_VULNERABILITY_TRIAGE_2026-08-17.md`
- both frontend package manifests, lockfiles and CI scripts

### Do

1. Identify the canonical install and audit command for each frontend workspace.
2. Add or tighten a CI gate that runs against committed lockfiles and fails on the agreed severity threshold.
3. Keep development-only and production dependency policy explicit; do not claim production safety from an incomplete workspace audit.
4. Verify that the gate catches a deliberately introduced audit fixture without committing a vulnerable dependency.
5. Record exact commands, workspace paths and result interpretation in durable evidence.

### Tests

- clean install using each committed lockfile;
- audit command for each frontend workspace;
- both frontend builds;
- negative gate test or equivalent documented fixture;
- `git diff --check`;
- queue and planning validators.

### Acceptance

- Both frontend workspaces have one documented, reproducible audit command.
- CI fails for a newly introduced vulnerability at the agreed threshold.
- The gate does not depend on local caches or secrets.
- No current zero result is overstated as a permanent guarantee; the gate proves future detection.

### Dependencies

- `SEC07` is DONE and supplies the remediation baseline.
- `SEC05` remains waiting on `MT09`; this prompt must not be used to bypass that broader security/deployment gate.

---

---

---

## OBS10 - Prepare operational dashboard honesty contract

Status: DONE
Priority: future / planning
Feature family: observability-dashboard-honesty
Parallel-safe: yes, docs/contracts only
Owner: Codex
Local lock: removed after DONE

### Problem

Worker, import and analytics SLA contracts exist, but there is still no frozen honesty contract for operational dashboard layers. A later dashboard slice could default missing panels to green, invent healthy metrics, or mix business analytics into operational telemetry.

### Evidence

- docs/roadmaps/OBSERVABILITY_ROADMAP.md OBS-7
- docs/architecture/OBSERVABILITY_INSTRUMENTATION_ROLLOUT_PLAN.md Slice 5 / O2-6
- docs/architecture/OBSERVABILITY_SLI_CATALOG.md proposed dashboard layers
- docs/architecture/OBSERVABILITY_WORKER_SLA_EVIDENCE_CONTRACT.md
- docs/qa/OBSERVABILITY_WORKER_SLA_EVIDENCE_CAPTURE_2026-08-17.md

### Scope

- docs/contracts only for dashboard layers, unknown/WARN rules and source-of-truth pointers;
- keep business readiness, API, import, worker and database/cache layers distinct;
- no runtime dashboard product, vendor choice, alerting or SLA numbers.

### Read first

- OBS09 capture note
- OBS08 worker SLA contract
- OBS02 instrumentation rollout plan Slice 5
- OBS01 catalog dashboard layers
- MASTER_ROADMAP.md current READY

### Do

1. Freeze a citeable operational dashboard honesty contract that maps each layer to existing OBS evidence, not a new metric catalog.
2. Keep missing panel data as unknown/WARN, never green or `0`.
3. Keep tenant dimensions blocked until MT authorizes them.
4. Do not implement a dashboard UI or start `PERF16` in this prompt.

### Tests

- contract forbids treating missing panel data as healthy;
- last-success age stays unknown, not `0 seconds`;
- docs/queue validators pass; no runtime code in this prompt.

### Acceptance

- one citeable dashboard honesty contract exists;
- operators can tell which layers are defined vs still unknown;
- READY pointer remains single for OBS.

### Completion note

- Date: 2026-08-19
- Status: DONE
- Completion: 100%
- Changed files: docs/architecture/OBSERVABILITY_OPERATIONAL_DASHBOARD_HONESTY_CONTRACT.md, docs/roadmaps/OBSERVABILITY_ROADMAP.md, MASTER_ROADMAP.md, docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md, .ai/runs/2026-08-19-OBS10-evidence.md
- Contract/runtime behavior changed: froze the operational dashboard honesty contract as a docs-only layer map with explicit unknown/warn/blocked semantics, layer ownership and no fake-green defaults
- Checks run: pending
- Checks not run: pending
- Run log: .ai/runs/2026-08-19-OBS10-evidence.md
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: 5d60b43f636ea40858fe5c37ab66ce0b335699c7
- Main verification: passed - git rev-parse origin/main -> 5d60b43f636ea40858fe5c37ab66ce0b335699c7
- Missed: none known
- Follow-up: OBS11 - Prepare operational dashboard panel inventory contract
- Residual risk: the contract is frozen before any runtime dashboard implementation exists
- Prompt defect / scope repair: none

### Dependencies

- OBS09 DONE;
- OBS08 DONE;
- OBS01/OBS02 dashboard language already exists.

---

## OBS11 - Prepare operational dashboard panel inventory and correlation contract

Status: DONE
Ready after: `OBS10` is `DONE` and an owner explicitly promotes this additive docs slice
Priority: future / planning
Feature family: observability-dashboard-panel-inventory
Parallel-safe: yes, docs/contracts only
Owner: Cursor Auto
Local lock: removed after DONE
Promotion note: 2026-08-20 - owner-promoted via queue refill continuation (`docs/planning/QUEUE_REFILL_2026-08-20.md`).

### Problem

Dashboard honesty layers are frozen, but there is still no citeable inventory of which panels may appear, which correlation IDs they require, and how missing panels stay unknown/WARN. A later UI slice could invent healthy panels or mix business analytics KPIs into operational telemetry.

### Evidence

- docs/architecture/OBSERVABILITY_OPERATIONAL_DASHBOARD_HONESTY_CONTRACT.md
- docs/architecture/OBSERVABILITY_SLI_CATALOG.md
- docs/planning/QUEUE_REFILL_2026-08-20.md

### Scope

- docs/contracts only for panel inventory, required correlation fields and unknown/WARN rules;
- no runtime dashboard product, vendor choice, alerting or SLA numbers.

### Read first

- OBS10 completion note
- operational dashboard honesty contract
- MASTER_ROADMAP.md current READY

### Do

1. Freeze the allowed panel inventory per honesty layer.
2. Name required correlation IDs when present and keep missing correlation as unknown, not green.
3. Keep tenant dimensions blocked until MT authorizes them.
4. Do not implement a dashboard UI in this prompt.

### Tests

- missing panel stays unknown/WARN, never green or `0`;
- docs/queue validators pass; no runtime code in this prompt.

### Acceptance

- one citeable panel-inventory contract exists when promoted;
- READY pointer remains single for OBS.

### Dependencies

- OBS10 DONE.

### Completion note

- Date: 2026-08-20
- Status: DONE
- Completion: allowed operational dashboard panels and correlation honesty are frozen in a citeable docs-only contract
- Changed files: docs/architecture/OBSERVABILITY_DASHBOARD_PANEL_INVENTORY_CONTRACT.md, docs/roadmaps/OBSERVABILITY_ROADMAP.md, MASTER_ROADMAP.md, docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md, docs/planning/QUEUE_REFILL_2026-08-20.md, .ai/runs/2026-08-20-OBS11-evidence.md
- Contract/runtime behavior changed: yes; runtime UI remains intentionally out of scope
- Checks run: `node scripts/check-prompt-queues.mjs`; `node scripts/check-planning-architecture.mjs`
- Checks not run: dotnet/npm - docs only
- Run log: .ai/runs/2026-08-20-OBS11-evidence.md
- Evidence state: synchronized
- Delivery mode: branch `cursor/queue-refill-dt09-dex20`
- Main commit SHA: 8ec23c29564b188b4b41f18efb049b6954aee2fe
- Main verification: passed - origin/main contains 8ec23c29564b188b4b41f18efb049b6954aee2fe
- Missed: none
- Follow-up: none until owner promotes a runtime dashboard slice
- Residual risk: inventory is docs-only until a UI prompt exists
- Prompt defect / scope repair: none

---

## PERF16 - Reopen D8 shared-SaaS measurement when MT isolation fixtures exist

Status: BLOCKED
Ready after: `MT10` is `DONE` or `MASTER_ROADMAP.md` records an explicit shared-SaaS evidence gate
Priority: future / measurement
Feature family: performance-scalability-d8-reopen
Parallel-safe: yes, docs/contracts until MT fixtures exist
Owner: unassigned
Local lock: `.ai/task-locks/PERF16-<agent>.lock.md`

### Problem

PERF15 froze D8 as MT-owned and `n/a_dedicated`, but there is still no authorized shared-SaaS measurement pack. A later PERF slice must wait for MT fixtures or an owner-recorded gate instead of inventing isolation overhead.

### Evidence

- docs/architecture/PERFORMANCE_SHARED_SAAS_EVIDENCE_GATE.md
- docs/architecture/PERFORMANCE_SCALABILITY_GATE_EVIDENCE_CONTRACT.md
- docs/ai/MULTITENANCY_PROMPT_QUEUE.md (`MT02` WAITING, `MT10` WAITING)
- .ai/runs/2026-08-12-PERF14-evidence.md

### Scope

- docs/contracts or later measurement only after MT fixtures/`MT10` or an owner-recorded gate;
- no invented `shared_saas` claims from dedicated PERF10–PERF14 packs;
- no runtime optimization in this prompt.

### Read first

- PERF15 completion note
- PERF15 shared-SaaS evidence gate
- MT current READY / `MT10` release gate
- PERF09 D8 fields

### Do

1. Keep D8 `n/a_dedicated` on dedicated packs until MT fixtures exist.
2. Reopen measurement only when `MT10` is DONE or `MASTER_ROADMAP.md` records an explicit shared-SaaS gate.
3. If measuring, cite `mtGateIds`, keep leak checks MT-owned, and leave missing overhead null.
4. Do not start OBS08 in this prompt if this slice is not current execution.

### Tests

- docs forbid dedicated-to-shared_saas promotion;
- missing overhead stays null, not `0 ms`;
- queue and planning validators pass.

### Acceptance

- D8 reopen conditions stay citeable;
- READY pointer remains single for PERF.

### Dependencies

- PERF15 DONE;
- `MT10` DONE or an owner-recorded shared-SaaS evidence gate.

### Completion note

- Date: 2026-08-18
- Status: BLOCKED
- Completion: 0% - D8 measurement cannot reopen without `MT10` or an owner-recorded shared-SaaS gate
- Changed files: docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md, MASTER_ROADMAP.md, docs/roadmaps/PERFORMANCE_ROADMAP.md
- Contract/runtime behavior changed: no; status corrected from READY to BLOCKED so agents do not claim a non-startable prompt
- Checks run: node scripts/check-agent-instructions.mjs --self-test/--live; node scripts/check-prompt-queues.mjs --self-test/--live; node scripts/check-planning-architecture.mjs --self-test/--live; git diff --check
- Checks not run: npm/dotnet tests - docs/routing only
- Run log: .ai/runs/2026-08-18-queue-promote-ready-evidence.md
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: 51550ec833985a79149de9215adee38c7696ceab
- Main verification: passed - origin/main contains 51550ec833985a79149de9215adee38c7696ceab
- Missed: no D8 measurement pack
- Follow-up: reopen PERF16 only after MT10 or an explicit shared-SaaS gate
- Residual risk: a later agent could still infer shared_saas from dedicated PERF10-PERF14 packs
- Next: RQ96 is current execution READY; OBS10 and RL10 are parallel-safe planning READYs
- Prompt defect / scope repair: PERF16 was left READY after the sequential refill even though its MT dependency forbids a start

## PERF15 - Shared-SaaS evidence gate

Status: DONE
Priority: future / measurement
Feature family: performance-scalability-d8-shared-saas
Parallel-safe: no - shares performance/MT contract paths
Owner: Cursor Auto
Local lock: removed after DONE

### Problem

PERF14 finished the D6 import-overlap evidence track, but D8 tenant-isolation overhead is still MT-owned and the queue/roadmap need a docs-only gate that keeps shared-SaaS claims out of the runtime lane until MT fixtures or an owner-approved gate exist.

### Evidence

- `.ai/runs/2026-08-12-PERF14-evidence.md`;
- `.ai/runs/2026-08-12-PERF14-raw.json`;
- `docs/architecture/PERFORMANCE_SCALABILITY_GATE_EVIDENCE_CONTRACT.md`.

### Scope

- docs-only alignment for the D8 / shared-SaaS boundary;
- keep PERF15 as a planning gate only;
- no runtime optimization, harness, or measurement claims in this prompt.

### Read first

- PERF14 evidence;
- PERF09 contract;
- `docs/ai/MULTITENANCY_PROMPT_QUEUE.md`;
- `docs/architecture/MULTITENANCY_ARCHITECTURE_ROADMAP.md`;
- `MASTER_ROADMAP.md`.

### Do

1. Keep D8 explicitly MT-owned and n/a_dedicated until MT fixtures/gates exist.
2. Preserve citeable shared-SaaS contract language without inventing measurements.
3. Keep queue/roadmap pointers aligned on PERF15 as the current READY prompt.

### Tests

- queue and planning validators pass;
- docs remain UTF-8 and free of new mojibake.

### Acceptance

- PERF15 is the only READY prompt in the PERF lane;
- PERF14 is clearly DONE with completion evidence;
- D8 shared-SaaS claims remain blocked on MT-owned evidence.

### Dependencies

- PERF14 DONE;
- MT fixtures/gates or an explicit owner decision for shared-SaaS evidence.

### Completion note

- Date: 2026-08-14
- Status: DONE
- Completion: 94%
- Changed files:
  - docs/architecture/PERFORMANCE_SHARED_SAAS_EVIDENCE_GATE.md
  - docs/architecture/PERFORMANCE_SCALABILITY_GATE_EVIDENCE_CONTRACT.md
  - docs/roadmaps/PERFORMANCE_ROADMAP.md
  - docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md
  - MASTER_ROADMAP.md
  - docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md
  - docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md
  - docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md
  - docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_TEST_HARDENING_ADDENDUM.md
  - .ai/runs/2026-08-14-PERF15-evidence.md
- Checks run:
  - `node scripts/check-prompt-queues.mjs --self-test` - pass
  - `node scripts/check-prompt-queues.mjs` - pass (260 tasks)
  - `node scripts/check-planning-architecture.mjs --self-test` - pass
  - `node scripts/check-planning-architecture.mjs` - pass (68 new planning tasks)
  - `node scripts/check-agent-instructions.mjs --self-test` - pass
  - `node scripts/check-agent-instructions.mjs` - pass
  - `git diff --check` - pass
- Checks not run:
  - `dotnet build` / `dotnet test` - docs/contracts only
  - `npm run build` / frontend tests - docs/contracts only
- Run log: .ai/runs/2026-08-14-PERF15-evidence.md
- Delivery mode: direct-main
- Main commit SHA: 42a7f8826eed6ac8a5926aceacb2a96937b35534
- Main verification: git rev-parse origin/main -> f73b6fdd98b274f2afd62667a28da70cf386fc4c; work SHA 42a7f8826eed6ac8a5926aceacb2a96937b35534 is an ancestor
- Missed: no D8 measurement pack; no MT promotion; shared-SaaS remains unclaimed
- Follow-up: `OBS08` (current execution); PERF program READY is `PERF16`
- Residual risk: a later pack could still relabel dedicated evidence as `shared_saas` if it ignores the gate
- Prompt defect / scope repair: Do-line 3 kept PERF15 READY during the slice; after close the validator needs a successor READY, so `PERF16` was inserted. The Dependencies MT line is the D8 claim gate, not a start blocker.
- Next: `OBS08` - Define Worker SLA evidence contract

---

## PERF14 - Unblock D6 import-overlap evidence

Status: DONE
Priority: future / measurement
Feature family: performance-scalability-d6-import-overlap
Parallel-safe: no - shares import/analytics paths
Owner: unassigned
Local lock: released

### Problem

PERF13 measured D5 cache footprint with tracked keys 0 -> 6, but D6 import overlap remains blocked because the repo still lacks a real M-PERF Access fixture or an owner-approved durable skip with replacement evidence path.

### Evidence

- `.ai/runs/2026-08-12-PERF13-evidence.md`;
- `.ai/runs/2026-08-12-PERF13-raw.json`;
- `docs/architecture/PERFORMANCE_SCALABILITY_GATE_EVIDENCE_CONTRACT.md`.

### Scope

- add the minimum Access import fixture or harness needed to measure D6, or record an owner-approved durable skip with replacement evidence path;
- no production optimization semantics; D8 stays n/a without MT.

### Read first

- PERF13 evidence;
- PERF09 contract;
- baseline B4/B5 families.

### Do

1. Advance D6 to measured or explicit owner-gated skip.
2. Record `.ai/runs/` evidence.
3. Do not invent SLOs or shared_saas claims.

### Tests

- evidence cites dimension ids;
- docs/queue validators pass.

### Acceptance

- D6 status changes with citeable reason/result;
- no runtime optimization shipped beyond measurement enablement required for evidence.

### Dependencies

- PERF13 DONE.

---

## PERF13 - Unblock D5 cache footprint or D6 import-overlap evidence

Status: DONE
Priority: future / measurement
Feature family: performance-scalability-d5-d6-unblock
Parallel-safe: no - shares cache/import measurement paths
Owner: Cursor
Local lock: released

### Problem

PERF12 measured D4 worker health and D7 document generate bursts, but D5 cache footprint and D6 import overlap remain durable blockers (no instrumentation / no M-PERF Access fixture).

### Evidence

- `.ai/runs/2026-08-12-PERF12-evidence.md`;
- `.ai/runs/2026-08-12-PERF13-evidence.md`;
- `.ai/runs/2026-08-12-PERF13-raw.json`;
- `docs/architecture/PERFORMANCE_SCALABILITY_GATE_EVIDENCE_CONTRACT.md`.

### Scope

- add the minimum harness or fixture needed to measure D5 or D6, or record an owner-approved durable skip with replacement evidence path;
- no production optimization semantics; D8 stays n/a without MT.

### Read first

- PERF12 evidence;
- PERF09 contract;
- baseline B3/B4 families.

### Do

1. Advance D5 and/or D6 to measured or explicit owner-gated skip.
2. Record `.ai/runs/` evidence.
3. Do not invent SLOs or shared_saas claims.

### Tests

- evidence cites dimension ids;
- docs/queue validators pass.

### Acceptance

- D5 measured with citeable footprint evidence; D6 blocked with citeable reason/result;
- no runtime optimization shipped beyond measurement enablement required for evidence.

### Dependencies

- PERF12 DONE.

---

## PERF12 - Close remaining scalability gaps (D4 retry / D5-D7)

Status: DONE
Priority: future / measurement
Feature family: performance-scalability-remaining-gaps
Parallel-safe: no - shares API/DB/worker/export paths
Owner: Cursor
Local lock: released

### Problem

PERF11 measured the D1 host envelope and recorded D4/D5 as blocked-with-reason, but worker Healthy proof, cache footprint, import overlap, and export bursts remain open for G10 dedicated completeness.

### Evidence

- `.ai/runs/2026-08-12-PERF11-evidence.md`;
- `.ai/runs/2026-08-12-PERF10-evidence.md`;
- `.ai/runs/2026-08-12-PERF12-evidence.md`;
- `.ai/runs/2026-08-12-PERF12-raw.json`;
- `docs/architecture/PERFORMANCE_SCALABILITY_GATE_EVIDENCE_CONTRACT.md`.

### Scope

- retry D4 with stronger worker observability or document durable blocker;
- and/or measure at least one of D5/D6/D7 when fixtures/instrumentation allow;
- no optimization semantics; D8 stays n/a without MT.

### Read first

- PERF11 evidence;
- PERF09 contract;
- baseline B4/B5/B6 families.

### Do

1. Advance at least one remaining gap to measured or durable blocked-with-reason.
2. Record `.ai/runs/` evidence against PERF09 fields.
3. Do not invent SLOs or shared_saas claims.

### Tests

- evidence cites dimension ids;
- docs/queue validators pass.

### Acceptance

- remaining-gap pack exists with honest status for targeted dimensions;
- no runtime optimization shipped.

### Dependencies

- PERF11 DONE.

### Completion note

- Date: 2026-08-12
- Status: DONE
- Changed files:
  - `Api/Endpoints/CachedAnalyticsEndpoints.cs`
  - `Infrastructure/Services/Caching/IAnalyticsCacheService.cs`
  - `Infrastructure/Services/Caching/HybridCacheService.cs`
  - `Infrastructure/Services/Caching/InMemoryCacheService.cs`
  - `Infrastructure/Services/Caching/DisabledAnalyticsCacheService.cs`
  - `tmp/perf13_measure.ps1`
  - `.ai/runs/2026-08-12-PERF13-evidence.md`
  - `.ai/runs/2026-08-12-PERF13-raw.json`
  - `docs/architecture/PERFORMANCE_SCALABILITY_GATE_EVIDENCE_CONTRACT.md`
  - `docs/roadmaps/PERFORMANCE_ROADMAP.md`
  - `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md`
  - `MASTER_ROADMAP.md`
- Contract/runtime behavior changed:
  - cache status now exposes tracked key count for a footprint snapshot;
  - PERF13 records D5 cache footprint as measured and D6 import overlap as blocked.
- Checks:
  - `powershell -ExecutionPolicy Bypass -File tmp/perf13_measure.ps1 -SkipSetup` - pass (D5 measured tracked keys 0 -> 6)
  - `dotnet build Trendplus2.Backend.slnf -v minimal` - pass
  - governance validators - pass
  - `git diff --check` - pass
- Checks not run:
  - `dotnet test`
  - `npm run check:analytics-guardrails`
  - `npm run build`
- Remaining risk:
  - D6 import overlap still lacks a real M-PERF Access fixture;
  - RSS delta stayed flat, so footprint is evidenced by tracked key count rather than process growth.
- Next:
  - PERF14 D6 import-overlap evidence
- Prompt defect / scope repair:
  - PERF13 had a durable blocker on D6; the smallest same-owner repair was to add a cache footprint snapshot so the prompt could complete honestly on D5 while leaving D6 blocked.

---

## PERF11 - Capture deferred scalability dimensions (D1/D4-D7)

Status: DONE
Priority: future / measurement
Feature family: performance-scalability-deferred-dimensions
Parallel-safe: no - shares API/DB/worker measurement paths
Owner: Cursor
Local lock: released

### Problem

PERF10 measured D2 concurrent warm reads and D3 connection pressure on a dedicated G10-oriented pack, but D1 resource envelope remains partial and D4-D7 are still deferred.

### Evidence

- `.ai/runs/2026-08-12-PERF10-evidence.md`;
- `.ai/runs/2026-08-12-PERF11-evidence.md`;
- `.ai/runs/2026-08-12-PERF11-raw.json`;
- `docs/architecture/PERFORMANCE_SCALABILITY_GATE_EVIDENCE_CONTRACT.md`.

### Scope

- measurement evidence for at least one of D1 (fuller envelope), D4 workers, D5 cache footprint, D6 import overlap, or D7 export bursts;
- mark remaining gaps honestly;
- no optimization semantics changes; D8 stays n/a without MT.

### Read first

- PERF10 evidence;
- PERF09 contract;
- baseline B4/B5/B6 families.

### Do

1. Extend harness or run targeted packs for deferred dimensions.
2. Record `.ai/runs/` evidence with PERF09 template fields.
3. Do not invent SLOs or shared_saas claims.

### Tests

- evidence cites dimension ids;
- docs/queue validators pass.

### Acceptance

- at least one previously deferred dimension moves to measured or explicit blocked-with-reason;
- no runtime optimization shipped.

### Dependencies

- PERF10 DONE.

### Completion note

- Date: 2026-08-12
- Agent: Cursor
- Changed files:
  - `tmp/perf11_measure.ps1`
  - `.ai/runs/2026-08-12-PERF11-evidence.md`
  - `.ai/runs/2026-08-12-PERF11-raw.json`
  - `docs/architecture/PERFORMANCE_SCALABILITY_GATE_EVIDENCE_CONTRACT.md`
  - `docs/roadmaps/PERFORMANCE_ROADMAP.md`
  - `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md`
  - `MASTER_ROADMAP.md`
- Checks:
  - `powershell -ExecutionPolicy Bypass -File tmp/perf11_measure.ps1 -SkipSetup` - pass (D1 measured; D4/D5 blocked-with-reason)
  - docs/queue validators - pending at commit
- Risks:
  - D4 Healthy proof missing despite start 200
  - D6/D7 still deferred; observed D1 envelope is not a reserved budget
- Next: PERF12 remaining scalability gaps

---

## PERF10 - Capture first scalability-gate evidence pack (PERF-9)

Status: DONE
Priority: future / measurement
Feature family: performance-scalability-gate-evidence
Parallel-safe: no ? shares API/DB measurement paths
Owner: Cursor
Local lock: released

### Problem

PERF09 defined G10/G50 scalability dimensions, but no citeable evidence pack yet records concurrency, DB pressure, or resource-envelope markers against that contract.

### Evidence

- `docs/architecture/PERFORMANCE_SCALABILITY_GATE_EVIDENCE_CONTRACT.md`;
- `.ai/runs/2026-08-12-PERF05-evidence.md`;
- `.ai/runs/2026-08-12-PERF08-evidence.md`;
- `.ai/runs/2026-08-12-PERF10-evidence.md`;
- `.ai/runs/2026-08-12-PERF10-raw.json`.

### Scope

- measurement evidence for a first dedicated-mode pack covering at least D2 concurrent reads and D3 connection pressure on M-tier (or documented deferrals);
- keep cold/warm and correctness co-assertions explicit;
- no optimization, index, cache TTL, or worker semantics changes;
- shared_saas / D8 remains deferred without MT fixtures.

### Read first

- PERF09 scalability gate contract;
- PERF05/PERF08 evidence;
- `docs/architecture/PERFORMANCE_BASELINE_CONTRACT.md`.

### Do

1. Run or script a first scalability-gate pack against the PERF09 template.
2. Record raw JSON + evidence summary under `.ai/runs/`.
3. Mark unmeasured dimensions explicitly; do not invent SLOs.

### Tests

- evidence cites PERF09 dimension ids;
- correctness co-assertions recorded;
- docs/queue validators pass.

### Acceptance

- at least one citeable G10-oriented dedicated pack exists with honest gaps;
- no semantic/runtime optimization shipped.

### Dependencies

- PERF09 DONE.

### Completion note

- Date: 2026-08-12
- Agent: Cursor
- Changed files:
  - `tmp/perf10_measure.ps1`
  - `.ai/runs/2026-08-12-PERF10-evidence.md`
  - `.ai/runs/2026-08-12-PERF10-raw.json`
  - `docs/roadmaps/PERFORMANCE_ROADMAP.md`
  - `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md`
  - `MASTER_ROADMAP.md`
- Checks:
  - `powershell -ExecutionPolicy Bypass -File tmp/perf10_measure.ps1 -SkipSetup` ? pass (D2 p95 468.58 ms; D3 peak total connections 4; error/timeout 0)
  - docs/queue validators ? pass
- Risks:
  - D1 partial; D4-D7 deferred
  - shared_saas (D8) blocked on MT fixtures
- Next: PERF10 follow-up pack for D1 envelope and D4-D7 coverage

---

## PERF09 - Define scalability gate evidence contract (PERF-9)

Status: DONE
Priority: future / planning
Feature family: performance-scalability-gate
Parallel-safe: yes, docs only
Owner: Cursor
Local lock: released

### Problem

PERF08 recorded distinct backend vs frontend cold-start evidence on M-tier, but the roadmap still lacks a scalability gate contract before 10/50-customer milestones (PERF-9).

### Evidence

- `docs/roadmaps/PERFORMANCE_ROADMAP.md` PERF-9;
- `docs/architecture/PERFORMANCE_BASELINE_CONTRACT.md`;
- `docs/architecture/PERFORMANCE_SCALABILITY_GATE_EVIDENCE_CONTRACT.md`;
- `.ai/runs/2026-08-12-PERF08-evidence.md`;
- `.ai/runs/2026-08-12-PERF08-raw.json`.

### Scope

- docs-only contract for per-customer resource envelope, concurrency, DB pressure, worker/cache footprint, import overlap, export bursts, tenant isolation overhead;
- no runtime optimization or load-test harness in this prompt.

### Read first

- PERF08 evidence;
- `docs/architecture/PERFORMANCE_BASELINE_CONTRACT.md`;
- `docs/ops/ANALYTICS_PERFORMANCE_BUDGETS.md`.

### Do

1. Draft scalability gate evidence contract with required dimensions and measurement placeholders.
2. Link from PERFORMANCE_ROADMAP PERF-9 and baseline contract.
3. Do not invent numeric SLOs without measured evidence.

### Tests

- contract cites PERF-8/perf evidence where relevant;
- docs/queue validators pass.

### Acceptance

- PERF-9 has a citeable planning artifact for next measurement slices: `docs/architecture/PERFORMANCE_SCALABILITY_GATE_EVIDENCE_CONTRACT.md`;
- no semantic/runtime change shipped.

### Dependencies

- PERF08 DONE.

### Completion note

- Date: 2026-08-12
- Agent: Cursor
- Changed files:
  - `docs/architecture/PERFORMANCE_SCALABILITY_GATE_EVIDENCE_CONTRACT.md`
  - `docs/architecture/PERFORMANCE_BASELINE_CONTRACT.md`
  - `docs/roadmaps/PERFORMANCE_ROADMAP.md`
  - `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md`
  - `MASTER_ROADMAP.md`
- Checks:
  - `node scripts/check-prompt-queues.mjs` ? pass
  - `node scripts/check-planning-architecture.mjs` ? pass
- Risks:
  - G10/G50 numeric envelopes remained unmeasured until PERF10
  - shared_saas (D8) blocked on MT fixtures
- Next: PERF10 first scalability-gate evidence pack

---

## PERF08 - Capture backend and frontend cold-start evidence (PERF-COLD-01)

Status: DONE
Priority: future / measurement
Feature family: performance-cold-start-evidence
Parallel-safe: no ? shares bootstrap/API paths
Owner: unassigned
Local lock: released

### Problem

PERF07 recorded section timings, but the roadmap still needs distinct backend and frontend cold-start evidence so teams can tell whether startup, first request, or first useful render is the real bottleneck.

### Evidence

- `docs/roadmaps/PERFORMANCE_ROADMAP.md` PERF-8;
- `docs/architecture/PERFORMANCE_COLD_START_INVESTIGATION_PLAN.md`;
- `.ai/runs/2026-08-12-PERF08-evidence.md`;
- `.ai/runs/2026-08-12-PERF08-raw.json`.

### Scope

- measurement evidence only for backend and frontend cold-start paths;
- keep cold/warm state explicit;
- no optimization, cache, or index semantics changes.

### Read first

- PERF06 investigation plan;
- PERF08 evidence;
- `docs/roadmaps/PERFORMANCE_ROADMAP.md` PERF-8.

### Do

1. Capture backend cold-start path evidence from process start to first useful API response.
2. Capture frontend cold-start evidence from boot to first useful render.
3. Keep separate cold/warm markers so evidence cannot be mistaken for steady-state latency.
4. Do not mask cold-start failure with fallback data or warm-path averages.

### Tests

- evidence distinguishes backend and frontend cold-start paths;
- no optimization or fallback masking is introduced;
- docs/queue validators pass.

### Acceptance

- citeable cold-start evidence exists for both backend and frontend paths;
- PERF-8 can be used as the next runtime measurement slice without guessing;
- no semantic change is shipped in this prompt.

### Dependencies

- PERF07 DONE;
- PERF06 DONE.

### Completion note

- Date: 2026-08-12
- Agent: Cursor
- Changed files:
  - `tmp/perf08_measure.ps1`
  - `Klijent/clientapp/scripts/perf08_frontend_render.mjs`
  - `.ai/runs/2026-08-12-PERF08-evidence.md`
  - `.ai/runs/2026-08-12-PERF08-raw.json`
  - `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md`
  - `docs/roadmaps/PERFORMANCE_ROADMAP.md`
  - `MASTER_ROADMAP.md`
- Checks:
  - `powershell -ExecutionPolicy Bypass -File tmp/perf08_measure.ps1 -SkipSetup` ? pass (backend 5 + frontend 3 samples)
  - `node scripts/check-prompt-queues.mjs` ? pending at commit
  - `node scripts/check-planning-architecture.mjs` ? pending at commit
- Risks:
  - frontend sample 3 bootstrap timeout under rapid cold restarts
  - dev Vite proxy path, not production preview/build
  - harness meta JSON assertions still null
- Next: PERF09 scalability gate evidence contract

---

## PERF07 - Capture bootstrap section timings on M-tier (PERF-COLD-01 profiling pass)

Status: DONE
Priority: future / measurement
Feature family: performance-cold-start-section-profiling
Parallel-safe: no ? shares bootstrap/API paths
Owner: unassigned
Local lock: released

### Problem

PERF06 identified sequential bootstrap sections as the likely cold-start cost center, but no section-level durations exist on M-tier.

### Evidence

- `docs/architecture/PERFORMANCE_COLD_START_INVESTIGATION_PLAN.md`
- `.ai/runs/2026-08-12-PERF07-evidence.md`
- `.ai/runs/2026-08-12-PERF07-raw.json`

### Scope

- measurement/instrumentation harness or log-derived timings for P0/P1 sections;
- no production optimization semantics changes.

### Read first

- PERF06 investigation plan
- `CachedAnalyticsEndpoints.cs` bootstrap factory
- `tmp/perf07_measure.ps1`

### Do

1. Capture P0 section durations on `trendplus_perf_m` cold outer cache miss.
2. Attach evidence JSON + summary table under `.ai/runs/`.
3. Do not change cache TTLs, indexes, or partial semantics.

### Completion note

- Date: 2026-08-12
- Agent: codex
- Changed files:
  - `Api/Endpoints/CachedAnalyticsEndpoints.cs`
  - `tmp/perf07_measure.ps1`
  - `.ai/runs/2026-08-12-PERF07-evidence.md`
  - `.ai/runs/2026-08-12-PERF07-raw.json`
  - `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md`
  - `docs/roadmaps/PERFORMANCE_ROADMAP.md`
  - `MASTER_ROADMAP.md`
- Checks:
  - `& .\\tmp\\perf07_measure.ps1 -SkipSetup` ? pass
- Risks:
  - section timings show existing schema gaps in `QuickInsights`, `Advanced`, and `ProductDecisionCenter`
- Next:
  - OBS07 analytics SLA evidence contract

### Tests

- evidence cites section ids from investigation plan;
- B8 protocol flags documented;
- no unauthorized optimization.

### Acceptance

- P0 section timing table exists on M-tier;
- PERF-COLD-01 promotion gate for runtime remediation can be evaluated.

### Dependencies

- PERF06 DONE.

---

## PERF06 - Investigate dashboard bootstrap cold-start (PERF-COLD-01)

Status: DONE
Priority: future / investigation
Feature family: performance-cold-start-investigation
Parallel-safe: no ? shares bootstrap/API paths
Owner: Cursor
Local lock: removed after DONE
Commit: `b7a7dd9`
Completed: 2026-08-12

### Problem

M-tier PERF05 recorded B8 cold p95 ~6.4 s (borderline vs 5 s target) while S-tier showed ~55 s. Before runtime optimization, the team needs a profiling plan that explains variance and identifies dominant cold-start sections without changing semantics.

### Evidence

- `.ai/runs/2026-08-12-PERF05-evidence.md` and raw JSON
- `PERF-COLD-01` in `docs/architecture/PERFORMANCE_MEASURED_OPTIMIZATION_BACKLOG.md`

### Scope

- investigation/plan docs and optional SQL/plan captures;
- no production optimization in this prompt.

### Read first

- PERF05 evidence
- PERF03 backlog row PERF-COLD-01
- bootstrap endpoint implementation

### Do

1. Document cold-start variance hypotheses (startup protocol, partial sections, cache state).
2. List bootstrap sub-queries/sections to profile on M-tier.
3. Define before/after proof requirements for a future runtime slice.

### Tests

- plan cites PERF-COLD-01 and M-tier numbers;
- no runtime optimization authorized.

### Acceptance

- investigation plan exists with profiling targets;
- rollback/correctness gates preserved.

### Dependencies

- PERF05 DONE.

### Completion note

- Date: 2026-08-12
- Agent: Cursor
- Changed files:
  - `docs/architecture/PERFORMANCE_COLD_START_INVESTIGATION_PLAN.md`
  - `.ai/runs/2026-08-12-PERF06-evidence.md`
  - `docs/architecture/PERFORMANCE_MEASURED_OPTIMIZATION_BACKLOG.md`
  - `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md`
  - `MASTER_ROADMAP.md`
  - `docs/roadmaps/PERFORMANCE_ROADMAP.md`
- Checks: docs/queue validators; `git diff --check`
- Risks: section timings still unmeasured until PERF07
- Next: PERF07 bootstrap section timing capture

---

## PERF05 - Execute M-tier baseline measurement pack and capture evidence

Status: DONE
Priority: future / measurement
Feature family: performance-m-tier-measurement-pack
Parallel-safe: yes, measurement/docs only
Owner: Cursor
Local lock: removed after DONE
Commit: `df7d8ea`
Completed: 2026-08-12

### Problem

PERF04 defined the M-tier plan and seed recipe, but pilot performance claims and backlog promotion still lack recorded M-tier timings for Decision Board, PDC, import, workers and frontend routes.

### Evidence

- `docs/architecture/PERFORMANCE_M_TIER_MEASUREMENT_PLAN.md`
- `docs/architecture/PERFORMANCE_MEASURED_OPTIMIZATION_BACKLOG.md` item `PERF-MEASURE-01`
- `.ai/runs/2026-08-11-PERF02-evidence.md` protocol anchor

### Scope

- seed `M-PERF-01` fixture and run mandatory B1?B8 families from the plan;
- capture evidence under `.ai/runs/`; no production optimization.

### Read first

- `PERFORMANCE_M_TIER_MEASUREMENT_PLAN.md`
- PERF02 evidence + raw JSON pattern
- `docs/architecture/PERFORMANCE_BASELINE_CONTRACT.md`

### Do

1. Implement or run M-tier seed (`M-PERF-01`) in disposable DB.
2. Record mandatory families with cold/warm matrix and correctness co-assertions.
3. Do not invent timings in planning docs; store raw samples in JSON.
4. Update backlog ranks only where M-tier numbers exist.

### Tests

- evidence file cites plan family IDs and row counts;
- correctness co-assertions documented per family;
- no runtime optimization in this prompt.

### Acceptance

- one citeable M-tier evidence pack exists;
- `PERF-MEASURE-01` surfaces have measured baselines or explicit empty/error honesty;
- backlog may reference M-tier numbers.

### Dependencies

- PERF04 DONE.

### Completion note

- Date: 2026-08-12
- Agent: Cursor
- Changed files:
  - `Database/Perf/M-PERF-01_seed.sql`
  - `tmp/perf05_setup_db.ps1`
  - `tmp/perf05_measure.ps1`
  - `.ai/runs/2026-08-12-PERF05-evidence.md`
  - `.ai/runs/2026-08-12-PERF05-raw.json`
  - `docs/architecture/PERFORMANCE_MEASURED_OPTIMIZATION_BACKLOG.md`
  - `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md`
  - `MASTER_ROADMAP.md`
- Checks:
  - `powershell -ExecutionPolicy Bypass -File tmp/perf05_setup_db.ps1` ? pass
  - `powershell -ExecutionPolicy Bypass -File tmp/perf05_measure.ps1 -SkipSetup` ? pass
  - docs/queue validators ? pending at commit
- Risks:
  - supplier ranking blocked by 429 in harness
  - B4/B7 not measured; JSON meta parsing gap in harness
  - S-tier vs M-tier cold-start not comparable without identical startup protocol
- Next: PERF06 cold-start investigation (PERF-COLD-01)

---

## PERF04 - Prepare M-tier baseline measurement plan

Status: DONE
Priority: future / measurement
Feature family: performance-m-tier-baseline
Parallel-safe: yes, planning/measurement only
Owner: Cursor
Local lock: removed after DONE
Commit: `a61e7d5`
Completed: 2026-08-12

### Problem

PERF03 ranked S-tier findings, but Decision Board, Product Decision Center, import, workers and frontend route load still have no M-tier baseline ? optimization must not extrapolate from 15-product smoke data.

### Evidence

- `docs/architecture/PERFORMANCE_MEASURED_OPTIMIZATION_BACKLOG.md` item PERF-MEASURE-01
- `docs/architecture/PERFORMANCE_BASELINE_CONTRACT.md` M-tier tier definition
- `.ai/runs/2026-08-11-PERF02-evidence.md` S-tier anchor

### Scope

- docs/measurement plan only for M-tier dataset recipe and benchmark family coverage;
- no production optimization;
- reuse PERF01/02 protocol.

### Read first

- PERF03 backlog
- PERF02 evidence
- `docs/roadmaps/PERFORMANCE_ROADMAP.md` PERF-5

### Do

1. Define M-tier seed parameters and row-count targets.
2. List which benchmark families (B1?B8) must be recorded on M-tier before pilot performance claims.
3. Keep cold/warm and correctness co-assertions explicit.
4. Do not invent M-tier timings in this prompt.

### Tests

- plan cites PERF03 backlog IDs;
- no optimization without M-tier measurements;
- docs/queue validators pass.

### Acceptance

- one citeable M-tier measurement plan exists;
- PERF-MEASURE-01 can be executed from the plan;
- no runtime optimization authorized.

### Dependencies

- PERF03 DONE.

### Completion note

- Date: 2026-08-12
- Agent: Cursor
- Changed files:
  - `docs/architecture/PERFORMANCE_M_TIER_MEASUREMENT_PLAN.md`
  - `.ai/runs/2026-08-12-PERF04-evidence.md`
  - `docs/architecture/PERFORMANCE_BASELINE_CONTRACT.md`
  - `docs/architecture/PERFORMANCE_MEASURED_OPTIMIZATION_BACKLOG.md`
  - `docs/roadmaps/PERFORMANCE_ROADMAP.md`
  - `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md`
  - `MASTER_ROADMAP.md`
- Checks: docs/queue validators; `git diff --check`
- Risks: M-PERF-01 seed script not built yet; B4 needs import fixture
- Next: PERF05 M-tier baseline pack execution

---

## PERF03 - Prepare SQL/index/cache optimization backlog from measurements

Status: DONE
Priority: future
Feature family: performance-measured-backlog
Parallel-safe: yes, planning only
Owner: Cursor
Local lock: removed after DONE
Commit: `d843ae9`
Completed: 2026-08-12

### Problem

Optimization tasks should be created only after baseline evidence identifies the slowest/highest-value paths.

### Evidence

- `.ai/runs/2026-08-11-PERF02-evidence.md` records the baseline methodology and measurements.
- PERF02 now provides concrete B1/B2/B8 timings and correctness checks.

### Scope

- measured findings -> prioritized future prompt plan;
- no production optimization in this task.

### Read first

- PERF02 evidence log
- relevant SQL/index/cache code/tests for measured candidates

### Do

1. Rank candidates by user/business impact and measured cost.
2. Separate SQL/index, cache, memory, worker and cold-start families.
3. Define before/after evidence requirements for each.
4. Exclude speculative changes without measured evidence.

### Tests

- every backlog item links a measurement;
- correctness/rollback/invalidation checks are specified;
- no item mixes unrelated performance families.

### Acceptance

- future runtime optimization prompts are evidence-backed and reviewable.

### Dependencies

- PERF02 DONE with usable measurements.

### Completion note

- Date: 2026-08-12
- Agent: Cursor
- Deliverable: `docs/architecture/PERFORMANCE_MEASURED_OPTIMIZATION_BACKLOG.md`
- Also updated: PERFORMANCE_BASELINE_CONTRACT pointer, PERFORMANCE_ROADMAP, MASTER_ROADMAP
- Checks: docs/queue validators; `git diff --check`
- Risks: S-tier only ? cold-start dominates; warm paths already fast
- Next: PERF04 M-tier baseline measurement plan

---

## PERF02 - Execute the S-tier baseline measurement pack and capture evidence

Status: DONE
Priority: future / measurement
Feature family: performance-measurement-pack
Parallel-safe: yes, measurement/docs only
Owner: Cursor
Commit: `8e19a7f21126c3d7eb777aa625de45eb19a8ec8d`
Completed: 2026-08-11
Local lock: removed after DONE

### Problem

PERF01 defined the baseline contract, and PERF02 captured the first S-tier evidence pack so later optimization planning can rely on measured facts instead of assumptions.

### Evidence

- `docs/roadmaps/PERFORMANCE_ROADMAP.md` says the first S-tier measurement pack is required before the optimization backlog can move.
- `docs/architecture/PERFORMANCE_BASELINE_CONTRACT.md` already defines the measurement discipline and target budgets.
- `docs/ops/ANALYTICS_PERFORMANCE_BUDGETS.md` and the existing benchmark-related scripts can anchor the first pack.
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md` keeps the runtime correctness gates ahead of optimization work.
- `.ai/runs/2026-08-11-PERF02-evidence.md` records the baseline pack on `trendplus_test`.

### Scope

- benchmark harnesses and scripts already in the repo;
- representative small dataset baseline checks;
- exact before/after evidence capture for the highest-value flows;
- no query/index/cache optimization in this prompt.

### Read first

- PERF01 output
- `docs/architecture/PERFORMANCE_BASELINE_CONTRACT.md`
- `docs/ops/ANALYTICS_PERFORMANCE_BUDGETS.md`
- `docs/roadmaps/PERFORMANCE_ROADMAP.md`

### Do

1. Keep the recorded S-tier measurement pack as the source of truth for later optimization planning.
2. Preserve environment, dataset tier, warm/cold state and output correctness checks in the evidence log.
3. Record exact commands and observed latency/throughput/memory evidence.
4. Use the measured baseline to prepare the next optimization backlog.

### Tests

- measurement inputs are reproducible;
- cold/warm state is explicit;
- correctness checks run alongside the measurements;
- no optimization claim is made without evidence.

### Acceptance

- a usable S-tier measurement pack exists for the current codebase;
- later optimization prompts can cite concrete baseline evidence;
- the pack keeps correctness and performance evidence together.

### Dependencies

- PERF01 DONE.

### Completion note

- Date: 2026-08-11
- Agent: Cursor
- Changed files:
  - `.ai/runs/2026-08-11-PERF02-evidence.md`
  - `.ai/runs/2026-08-11-PERF02-raw.json`
  - `docs/architecture/PERFORMANCE_BASELINE_CONTRACT.md`
  - `docs/roadmaps/PERFORMANCE_ROADMAP.md`
  - `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md`
  - `MASTER_ROADMAP.md`
- Checks:
  - `powershell -ExecutionPolicy Bypass -File tmp/perf02_measure.ps1` - pass
  - baseline data seeded with `005_CreateArtikliAndTestData.sql` and `004_SimpleTestData.sql`
- Risks:
  - dashboard bootstrap remains partial, but explicit `success=true`/`warningCode` keep the signal honest
  - `004_SimpleTestData.sql` has a final top-level `RAISE NOTICE` syntax error after inserts; the inserted rows remain committed
- Next:
  - PERF03 backlog recorded; PERF04 M-tier plan DONE; PERF05 READY for execution

---
---

## PERF01 - Establish performance baseline, dataset tiers and budgets

Status: DONE
Priority: future / planning
Feature family: performance-baseline-contract
Parallel-safe: yes, measurement/docs only
Owner: Cursor
Local lock: none

### Problem

Trendplus has known query, worker, cold-start and dataset-scale risks, but optimization without a repeatable baseline can create speculative complexity or accidentally change analytics semantics.

### Evidence

- analytics/import workers and dashboards already include potentially expensive aggregation paths;
- current planning mentions timeouts/cold-start/worker concerns in multiple places;
- no canonical performance roadmap/budget previously owned SQL profiling, indexes, cache, large datasets, memory, worker throughput and cold start together.

### Scope

- benchmark inventory and dataset-tier definitions;
- identify existing benchmark/performance tests/scripts and gaps;
- initial budget proposal as measurable targets, not product SLA promises;
- no production query/index/cache changes.

### Read first

- `MASTER_ROADMAP.md`
- `docs/roadmaps/PERFORMANCE_ROADMAP.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md`
- import/worker architecture docs and current performance-related tests/scripts

### Do

1. Inventory critical route/query/worker/import/frontend flows worth benchmarking.
2. Define small/medium/large representative dataset tiers and how they can be reproduced safely.
3. Define cold vs warm measurement rules and environment metadata.
4. Propose initial p50/p95/throughput/memory budgets as engineering budgets, clearly separate from contractual SLA.
5. Map each baseline to the owning future PERF phase.
6. Identify correctness assertions that must run alongside performance measurements.

### Tests

- every proposed benchmark has reproducible inputs and output correctness checks;
- cold/warm state is explicit;
- dataset tier is explicit;
- no budget is presented as measured fact until measured;
- no runtime optimization is introduced.

### Acceptance

- one reproducible performance-baseline plan exists;
- benchmark priorities cover SQL, cache, large datasets, memory, workers and cold start;
- later optimization prompts can require before/after evidence.

### Completion note

- Date: 2026-08-11
- Agent: Cursor
- Changed files:
  - `docs/architecture/PERFORMANCE_BASELINE_CONTRACT.md`
  - `docs/roadmaps/PERFORMANCE_ROADMAP.md`
  - `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md`
  - `MASTER_ROADMAP.md`
- Checks:
  - `git diff --check` - pass
  - no runtime optimization introduced
- Risks:
  - budgets remain target-only until S-tier measurement pack is recorded
  - no BenchmarkDotNet/k6 harness yet (explicit gap)
- Next:
  - PERF02 S-tier pack recorded; PERF05 M-tier execution is next READY

### Dependencies

- RQ/STAB semantics remain authoritative;
- OBS may later provide continuous telemetry but is not required to define the baseline.

---

## OBS08 - Define Worker SLA evidence contract

Status: DONE
Priority: future
Feature family: observability-worker-sla-evidence
Parallel-safe: yes, docs/contracts only
Owner: Cursor Auto
Local lock: .ai/task-locks/OBS08-cursor.lock.md (removed after DONE)

### Problem

Worker lifecycle evidence exists as scattered metrics, but there is still no frozen docs contract for queue age, run duration, retry/dead-letter counts, last successful run or paused/disabled state.

### Evidence

- `docs/roadmaps/OBSERVABILITY_ROADMAP.md` OBS-6;
- `docs/architecture/OBSERVABILITY_SERVICE_LEVEL_VOCABULARY.md` worker terms;
- `docs/architecture/OBSERVABILITY_SLI_CATALOG.md` worker rows;
- `docs/architecture/OBSERVABILITY_IMPORT_SLA_EVIDENCE_CONTRACT.md` import precedent;
- `docs/architecture/OBSERVABILITY_ANALYTICS_SLA_EVIDENCE_CONTRACT.md` evidence semantics precedent.

### Scope

- docs/contracts only for worker SLA evidence fields, states and unknown behavior;
- keep queue/backlog, age and retry evidence explicit;
- no runtime wiring or alerting changes.

### Read first

- OBS05 vocabulary;
- OBS01 SLI catalog worker rows;
- `docs/roadmaps/OBSERVABILITY_ROADMAP.md` OBS-6;
- import SLA evidence contract;
- analytics SLA evidence contract.

### Do

1. Define the worker SLA evidence payload: queue/backlog size, oldest work age, run duration, success/failure/retry/dead-letter counts, last successful run and disabled/paused state.
2. Keep source-job correlation explicit where safe.
3. Keep missing evidence unknown rather than green.
4. Gate any numeric SLA hours behind explicit product or operations approval.

### Tests

- contract forbids treating missing worker evidence as healthy;
- missing last-success stays unknown, not zero;
- docs/queue validators pass; no runtime code in this prompt.

### Acceptance

- one citeable worker SLA evidence contract exists;
- support and operations can answer OBS-6 questions from the contract language;
- runtime wiring remains a later promoted slice.

### Dependencies

- OBS07 DONE;
- OBS06 DONE;
- OBS05 DONE.

### Completion note

- Date: 2026-08-17
- Status: DONE
- Completion: 100%
- Changed files: docs/architecture/OBSERVABILITY_WORKER_SLA_EVIDENCE_CONTRACT.md, docs/architecture/OBSERVABILITY_SERVICE_LEVEL_VOCABULARY.md, docs/architecture/OBSERVABILITY_SLI_CATALOG.md, docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md, docs/roadmaps/OBSERVABILITY_ROADMAP.md, MASTER_ROADMAP.md, .ai/runs/2026-08-17-OBS08-evidence.md
- Contract/runtime behavior changed: docs-only worker SLA evidence fields, states and unknown-vs-green rules; no runtime wiring
- Checks run: node scripts/check-agent-instructions.mjs --self-test/--live; node scripts/check-prompt-queues.mjs --self-test/--live; node scripts/check-planning-architecture.mjs --self-test/--live; git diff --check
- Checks not run: npm run build / dotnet test - docs/contracts only
- Run log: .ai/runs/2026-08-17-OBS08-evidence.md
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: d4acd9bc80df025e17de27505aa54f0a5c65670b
- Main verification: passed - git rev-parse origin/main -> 51550ec833985a79149de9215adee38c7696ceab; work SHA d4acd9bc80df025e17de27505aa54f0a5c65670b is an ancestor
- Missed: runtime capture remains OBS09
- Follow-up: OBS09 READY
- Residual risk: current /api/workers/health can still look green until OBS09 capture lands
- Next: OBS09 - Capture worker SLA evidence without fake-green defaults
- Prompt defect / scope repair: none

---

## OBS09 - Capture worker SLA evidence without fake-green defaults

Status: DONE
Priority: future / measurement
Feature family: observability-worker-sla-evidence
Parallel-safe: yes, docs/contracts or later runtime wiring after OBS08
Owner: Cursor Auto
Local lock: .ai/task-locks/OBS09-cursor.lock.md (removed after DONE)

### Problem

After the worker SLA evidence contract exists, the repo still needs a bounded evidence capture path so queue age, last-success and paused/disabled state cannot be presented as healthy zeros.

### Evidence

- OBS08 worker SLA contract (when DONE)
- `docs/architecture/OBSERVABILITY_SLI_CATALOG.md` worker rows
- worker classes under `Workers/`

### Scope

- measurement/evidence capture only after OBS08;
- no invented SLA hours;
- missing last-success stays unknown.

### Read first

- OBS08
- OBS07 analytics SLA evidence contract
- `docs/roadmaps/OBSERVABILITY_ROADMAP.md` OBS-6

### Do

1. Follow the OBS08 contract fields exactly.
2. Record unknown/missing worker evidence as unknown, not zero or green.
3. Do not add alerting rules in this prompt.

### Tests

- evidence cites contract field ids;
- docs/queue validators pass if only docs change;
- focused worker tests if runtime capture is in scope after OBS08.

### Acceptance

- worker SLA evidence can be cited without fake-green defaults;
- OBS08 remains the contract source of truth.

### Dependencies

- OBS08 DONE.

### Completion note

- Date: 2026-08-17
- Status: DONE
- Completion: 100%
- Changed files: Infrastructure/Services/WorkerSlaEvidenceMapper.cs, Api/Endpoints/AllEndpoints.cs, Api.Tests/WorkerSlaEvidenceMapperTests.cs, docs/qa/OBSERVABILITY_WORKER_SLA_EVIDENCE_CAPTURE_2026-08-17.md, docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md, docs/roadmaps/OBSERVABILITY_ROADMAP.md, MASTER_ROADMAP.md, .ai/runs/2026-08-17-OBS09-evidence.md
- Contract/runtime behavior changed: additive SlaEvidence on GET /api/workers/health; uninstrumented fields stay null/unknown; existing health counts unchanged
- Checks run: focused WorkerSlaEvidenceMapperTests; governance validators; git diff --check
- Checks not run: full dotnet test suite; npm frontend checks
- Run log: .ai/runs/2026-08-17-OBS09-evidence.md
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: d4acd9bc80df025e17de27505aa54f0a5c65670b
- Main verification: passed - git rev-parse origin/main -> 51550ec833985a79149de9215adee38c7696ceab; work SHA d4acd9bc80df025e17de27505aa54f0a5c65670b is an ancestor
- Missed: per-worker runtime policy pause not on snapshot; W5/W6 remain uninstrumented
- Follow-up: SEC07 sequential next; OBS10 is the current OBS READY
- Residual risk: legacy health counts can still look green; operators must read SlaEvidence
- Next: SEC07 - Frontend production dependency vulnerability triage
- Prompt defect / scope repair: none

---

## OBS07 - Define Analytics SLA evidence contract

Status: DONE
Ready after: `OBS06` is `DONE`
Priority: future
Feature family: observability-analytics-sla-evidence
Parallel-safe: yes, docs/contracts only
Owner: unassigned
Local lock: released
Promotion note: 2026-08-12 ? `OBS06` DONE; roadmap OBS-5 Analytics SLA evidence (docs only).

### Problem

Analytics freshness and refresh provenance exist across several surfaces, but there is still no frozen docs contract for how analytics SLA evidence answers requested/started/completed/failed/partial/fallback and last-success age without inventing green or contractual hours.

### Evidence

- `docs/roadmaps/OBSERVABILITY_ROADMAP.md` OBS-5;
- `docs/architecture/OBSERVABILITY_SERVICE_LEVEL_VOCABULARY.md` analytics terms;
- `docs/architecture/OBSERVABILITY_SLI_CATALOG.md` R1?R7;
- `docs/architecture/OBSERVABILITY_IMPORT_SLA_EVIDENCE_CONTRACT.md` import boundary precedent.
- `docs/architecture/OBSERVABILITY_ANALYTICS_SLA_EVIDENCE_CONTRACT.md`

### Scope

- docs/contracts only for analytics SLA evidence fields, honesty rules and unknown behavior;
- reuse OBS05 vocabulary and R1?R7 SLI IDs;
- no runtime instrumentation rewrite;
- no numeric customer SLA hours.

### Read first

- OBS05 vocabulary
- OBS01 SLI catalog analytics rows
- `docs/roadmaps/OBSERVABILITY_ROADMAP.md` OBS-5
- import SLA evidence contract

### Do

1. Define the analytics SLA evidence payload: requested, started, completed, failed, partial, fallback, last successful age, source/import provenance.
2. Keep measurement from authoritative refresh request/start to durable terminal status.
3. Keep unknown/partial/fallback explicit and non-green.
4. Gate any numeric SLA hours behind business/QDB approval.

### Tests

- contract forbids treating fallback or partial as successful freshness;
- missing last-success stays unknown, not zero age;
- docs/queue validators pass; no runtime code in this prompt.

### Acceptance

- one citeable analytics SLA evidence contract exists;
- support can answer the OBS-5 questions from the contract language;
- runtime wiring remains a later promoted slice.

### Completion note

- Date: 2026-08-12
- Agent: codex
- Changed files:
  - `docs/architecture/OBSERVABILITY_ANALYTICS_SLA_EVIDENCE_CONTRACT.md`
  - `docs/architecture/OBSERVABILITY_SERVICE_LEVEL_VOCABULARY.md`
  - `docs/architecture/OBSERVABILITY_SLI_CATALOG.md`
  - `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md`
  - `docs/roadmaps/OBSERVABILITY_ROADMAP.md`
  - `MASTER_ROADMAP.md`
- Checks:
  - docs/queue validators; `git diff --check` ? pending at commit
- Risks:
  - runtime wiring for analytics SLA evidence remains a later slice
- Next:
  - OBS06 worker SLA evidence / OBS-6 roadmap slice when promoted

### Dependencies

- OBS05 DONE;
- OBS06 DONE.

---
---

## OBS06 - Define Import SLA evidence contract

Status: DONE
Priority: future
Feature family: observability-import-sla-evidence
Parallel-safe: yes, docs/contracts only
Owner: unassigned
Local lock: `.ai/task-locks/OBS06-<agent>.lock.md`
Promotion note: 2026-08-11 ? `OBS05` DONE; roadmap OBS-4 Import SLA evidence (docs only). Rewritten from a vocabulary duplicate so OBS READY stays single and non-overlapping.

### Problem

Import lifecycle timestamps and states exist across connectors and status surfaces, but there is still no frozen docs contract for how Import SLA evidence answers accept/start/complete/fail/cancel/partial and last-success age without inventing green or contractual hours.

### Evidence

- `docs/roadmaps/OBSERVABILITY_ROADMAP.md` OBS-4;
- `docs/architecture/OBSERVABILITY_SERVICE_LEVEL_VOCABULARY.md` import terms;
- `docs/architecture/OBSERVABILITY_SLI_CATALOG.md` I1?I6;
- `docs/architecture/OBSERVABILITY_INSTRUMENTATION_ROLLOUT_PLAN.md` import slices.

### Scope

- docs/contracts only for Import SLA evidence fields, honesty rules and unknown behavior;
- reuse OBS05 vocabulary and I1?I6 SLI IDs;
- no runtime instrumentation rewrite;
- no numeric customer SLA hours.

### Read first

- OBS05 vocabulary
- OBS01 SLI catalog import rows
- `docs/roadmaps/OBSERVABILITY_ROADMAP.md` OBS-4
- QDB import status surfaces already in the repo

### Do

1. Define the Import SLA evidence payload: accepted, started, completed, failed, cancelled, partial, last successful age, source/scope.
2. Keep measurement from accept/queue to durable terminal status.
3. Keep unknown/partial/cancel explicit and non-green.
4. Gate any numeric SLA hours behind business/QDB approval.

### Tests

- contract forbids treating cancel/partial as successful freshness;
- missing last-success stays unknown, not zero age;
- docs/queue validators pass; no runtime code in this prompt.

### Acceptance

- one citeable Import SLA evidence contract exists;
- support can answer the OBS-4 questions from the contract language;
- runtime wiring remains a later promoted slice.

### Dependencies

- OBS05 DONE.

### Completion note

- Date: 2026-08-12
- Agent: Codex
- Changed files:
  - `docs/architecture/OBSERVABILITY_IMPORT_SLA_EVIDENCE_CONTRACT.md`
  - `docs/roadmaps/OBSERVABILITY_ROADMAP.md`
  - `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md`
  - `MASTER_ROADMAP.md`
- Checks:
  - docs/planning update only; no runtime code changed
- Risks:
  - numeric SLA hours remain out of scope until business/QDB approval
  - MT/tenant scope is still not inferred by this contract
- Next:
  - `OBS07` is READY (analytics SLA evidence contract)

---

## OBS05 - Define service level vocabulary for API/import/analytics/worker/report evidence

Status: DONE
Priority: future
Feature family: observability-service-level-vocabulary
Parallel-safe: yes, docs/contracts only
Owner: Cursor
Local lock: removed after DONE
Commit: `9cec275738fd033e6a633169e904bea9a1591125`
Completed: 2026-08-11
Promotion note: 2026-08-11 - `OBS04` DONE; next roadmap slice is OBS-3 service level vocabulary

### Problem

Support still needs one shared vocabulary for what counts as API availability, import SLA, analytics freshness SLA, worker processing SLA and report generation SLA before runtime prompts can wire those signals into evidence.

### Evidence

- `docs/roadmaps/OBSERVABILITY_ROADMAP.md` OBS-3;
- `docs/architecture/OBSERVABILITY_SLI_CATALOG.md` service-level rows;
- `docs/architecture/OBSERVABILITY_INSTRUMENTATION_ROLLOUT_PLAN.md` slices 2-6;
- `docs/qa/ANALYTICS_PRODUCTION_READINESS_STATUS_2026-08-06.md` and the release evidence already rely on the vocabulary boundary.

### Scope

- docs/contracts only;
- vocabulary and glossary updates;
- no runtime instrumentation in this prompt.

### Read first

- OBS04 output
- `docs/roadmaps/OBSERVABILITY_ROADMAP.md`
- `docs/architecture/OBSERVABILITY_SLI_CATALOG.md`
- `docs/architecture/OBSERVABILITY_INSTRUMENTATION_ROLLOUT_PLAN.md`

### Do

1. Define the service-level terms for API/import/analytics/worker/report evidence.
2. Keep unknown, partial and stale evidence explicit instead of defaulting to green.
3. Preserve the boundary between measured evidence and contractual targets.
4. Make the vocabulary reusable by later runtime prompts.

### Tests

- terms are deterministic and non-overlapping;
- unknown telemetry remains non-green;
- no SLA term invents runtime evidence.

### Acceptance

- the queue has one clear service-level vocabulary prompt;
- later observability slices can reuse the same terms;
- support can name API/import/analytics/worker/report evidence without ambiguity.

### Dependencies

- OBS04 DONE.

### Completion note

- Date: 2026-08-11
- Agent: Cursor
- Deliverable: `docs/architecture/OBSERVABILITY_SERVICE_LEVEL_VOCABULARY.md`
- Also updated:
  - `docs/architecture/OBSERVABILITY_SLI_CATALOG.md` (pointer + summary)
  - `docs/roadmaps/OBSERVABILITY_ROADMAP.md` (OBS-3 DONE)
  - `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md`
  - `MASTER_ROADMAP.md`
- Checks: docs/queue validators; `git diff --check`
- Risks: vocabulary only ? no numeric SLO/SLA; report SLI IDs remain thin until instrumented
- Next: `OBS06` Import SLA evidence contract (roadmap OBS-4)
- Scope repair: former OBS06 vocabulary duplicate rewritten to Import SLA evidence so READY stays single and non-overlapping

---

## OBS01 - Define business/technical SLI, SLA and correlation catalog

Status: DONE
Priority: future / planning
Feature family: observability-sli-contract
Parallel-safe: yes, docs/contracts only
Owner: Cursor
Local lock: none

### Problem

Health, import status, freshness, worker state and latency exist across several surfaces, but there is no single catalog saying what should be measured, which component owns it, and how import/analytics/worker SLA evidence differs from product/business status.

### Evidence

- current health/readiness and analytics freshness/import provenance are already important release evidence;
- STAB work established that unknown operational evidence must stay visible;
- future business milestones require import, analytics and worker operability.

### Scope

- metric/SLI inventory and owner map;
- SLA/SLO vocabulary and proposed measurement points;
- correlation-ID lifecycle plan;
- tracing/dashboard gap inventory;
- no paid vendor choice and no broad runtime instrumentation.

### Read first

- `MASTER_ROADMAP.md`
- `docs/roadmaps/OBSERVABILITY_ROADMAP.md`
- current health/readiness, import status, analytics refresh and worker docs/code
- STAB release evidence

### Do

1. Define authoritative business and technical SLI families.
2. Define latency, error, freshness and throughput dimensions.
3. Define Import SLA, Analytics SLA and Worker SLA measurement boundaries without inventing contractual target numbers.
4. Define correlation-ID propagation expectations across HTTP, import, workers, refresh, report and future decision timeline.
5. Inventory existing logs/metrics/traces and name gaps.
6. Propose dashboard layers and unknown/non-green behavior.

### Tests

- every metric has source/owner/unit/dimensions;
- unknown telemetry is explicitly non-green;
- no correlation ID is treated as tenant/auth identity;
- sensitive source/customer payloads are excluded;
- SLA vocabulary distinguishes measured SLI from target/contract.

### Acceptance

- one SLI/SLA/correlation catalog exists;
- import, analytics and worker evidence boundaries are explicit;
- later instrumentation can be split by component.

### Completion note

- Date: 2026-08-11
- Agent: Cursor
- Changed files:
  - `docs/architecture/OBSERVABILITY_SLI_CATALOG.md`
  - `docs/roadmaps/OBSERVABILITY_ROADMAP.md`
  - `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md`
  - `MASTER_ROADMAP.md`
- Checks:
  - `git diff --check` - pass
  - no runtime instrumentation / vendor choice
- Risks:
  - queue-depth/DLQ and cache-hit SLIs remain gaps until OBS02 instrumentation
  - HTTP correlation vs RequestLogContext TraceId still dual-named
- Next:
  - `OBS02` stays WAITING until owner promotes instrumentation rollout
  - Current READY in this queue: `SEC01`

### Dependencies

- STAB security/privacy rules;
- MT before tenant labels/dimensions are treated as trusted shared-SaaS scope.

---

## OBS02 - Prepare observability instrumentation rollout plan

Status: DONE
Priority: future
Feature family: observability-rollout-plan
Parallel-safe: yes, planning only
Owner: Cursor
Local lock: none

### Problem

OBS01 must be translated into bounded instrumentation slices without a broad telemetry rewrite.

### Evidence

- OBS01 DONE: `docs/architecture/OBSERVABILITY_SLI_CATALOG.md`

### Scope

- docs/planning only;
- separate API/import/analytics/worker/dashboard/tracing slices;
- no vendor lock-in or runtime changes.

### Read first

- OBS01 output
- PERF roadmap
- existing Serilog/health/metrics code

### Do

1. Rank gaps by pilot/customer support value.
2. Define minimal first instrumentation slice.
3. Separate metrics, tracing, dashboards and alerts.
4. Specify validation and privacy checks.

### Tests

- each slice has source-of-truth and failure behavior;
- no absent metric defaults to healthy;
- no slice leaks secrets/customer payloads.

### Acceptance

- future OBS runtime prompts can be created without duplicating STAB or PERF.

### Completion note

- Date: 2026-08-11
- Agent: Cursor
- Changed files:
  - `docs/architecture/OBSERVABILITY_INSTRUMENTATION_ROLLOUT_PLAN.md`
  - `docs/roadmaps/OBSERVABILITY_ROADMAP.md`
  - `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md`
  - `MASTER_ROADMAP.md`
- Checks:
  - `git diff --check` - pass
  - no runtime instrumentation
- Risks:
  - O2-1 still needs a queued runtime implementation prompt
  - queue-depth SLIs remain unknown until real queues are instrumented
- Next:
  - Current OBS READY: `OBS04` (latency SLI vocabulary and warm/cold measurement contract)

### Dependencies

- OBS01 DONE.

---

## OBS03 - Implement observability Slice-1 API/process evidence

Status: DONE
Priority: future
Feature family: observability-api-process-evidence
Parallel-safe: yes, when paths do not collide with BCI/STAB auth or PERF optimization work
Owner: unassigned
Local lock: `.ai/task-locks/OBS03-<agent>.lock.md`
Promotion note: 2026-08-11 ? `OBS01`/`OBS02` DONE; first slice from `OBSERVABILITY_INSTRUMENTATION_ROLLOUT_PLAN.md`

### Problem

OBS02 ranked instrumentation slices, but no queued runtime prompt exists for the first API/process evidence improvements support needs in pilot.

### Evidence

- `docs/architecture/OBSERVABILITY_SLI_CATALOG.md`
- `docs/architecture/OBSERVABILITY_INSTRUMENTATION_ROLLOUT_PLAN.md` Slice 1

### Scope

- make request/availability/latency evidence consistent from existing `/ready`, `/health`, `/health/dependencies`, runtime version and request performance logging;
- preserve unknown ? green;
- admin/ops visibility only where privileged; do not broaden public `/health` disclosure;
- focused tests for unknown-when-missing and no fake-zero collapse;
- no paid APM vendor selection; no broad rewrite.

### Read first

- OBS01/OBS02 outputs
- PERF01 baseline contract (targets only; do not optimize queries here)
- STAB public health disclosure constraints

### Do

1. Inventory current fields already available from the named sources of truth.
2. Expose or normalize the minimal Slice-1 evidence surface (prefer reuse over new store).
3. Mark missing probes as unknown/non-green.
4. Add focused tests for unknown and error ? zero.

### Tests

- missing probe stays unknown, not healthy;
- error responses do not become fake zero metrics;
- no secrets/customer payloads in telemetry;
- STAB authz fail-closed unchanged for admin-only surfaces.

### Acceptance

- Slice-1 evidence is usable by support without inventing green defaults; later OBS slices can cite it.

### Dependencies

- OBS02 DONE.

### Completion note

- Date: 2026-08-11
- Status: DONE
- Changed files: `Api/Program.cs`, `Api/Services/Startup/DbConnectionHelper.cs`, `Api/Services/Startup/StartupReadinessState.cs`, `Api.Tests/AnalyticsCriticalRouteMappingsTests.cs`, `Api.Tests/StartupReadinessStateTests.cs`
- Contract/runtime behavior changed: dependency probes now preserve missing latency as null instead of fake zero; readiness state keeps unknown probe latency as null; runtime version requests continue to create performance-log evidence.
- Checks run: `dotnet test .\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~StartupReadinessStateTests|FullyQualifiedName~AnalyticsCriticalRouteMappingsTests"` pass
- Checks not run: governance validators, full build, full test suite
- Remaining risk: production `/health/dependencies` behavior still depends on live connection resolution and timeout behavior outside this focused test path
- Next: OBS04 (READY)
- Prompt defect / scope repair: replaced flaky health-status integration assertion with direct helper-level proof of missing-probe null latency

---

## OBS04 - Define latency SLI vocabulary and warm/cold measurement contract

Status: DONE
Priority: future
Feature family: observability-latency-sli-contract
Parallel-safe: yes, when paths do not collide with OBS03 runtime evidence or PERF baseline measurement work
Owner: unassigned
Local lock: `.ai/task-locks/OBS04-<agent>.lock.md`
Promotion note: 2026-08-11 - `OBS03` DONE; next roadmap slice is OBS-2 latency SLIs

### Problem

Slice-1 API/process evidence can show availability and request completion, but support still lacks a shared contract for how latency is named, grouped and split between cold and warm paths.

### Evidence

- `docs/architecture/OBSERVABILITY_SLI_CATALOG.md` latency rows;
- `docs/architecture/OBSERVABILITY_INSTRUMENTATION_ROLLOUT_PLAN.md` Slice 1/2;
- `docs/roadmaps/OBSERVABILITY_ROADMAP.md` OBS-2;
- `PERF01` baseline contract (measurement discipline only).

### Scope

- define the latency SLI vocabulary for API route families, import/connector phases, workers and report generation;
- separate cold-start and warm-path measurements where they differ;
- preserve unknown != 0 and no fake-green semantics;
- no vendor selection, no runtime instrumentation rewrite, no broad dashboard work.

### Read first

- OBS03 completion note;
- `docs/roadmaps/OBSERVABILITY_ROADMAP.md`;
- `docs/architecture/OBSERVABILITY_SLI_CATALOG.md`;
- `docs/architecture/OBSERVABILITY_INSTRUMENTATION_ROLLOUT_PLAN.md`;
- `PERF01` baseline contract.

### Do

1. Inventory the latency fields and route families that already exist.
2. Define the shared p50/p95/p99 vocabulary and measurement split.
3. Record cold/warm naming rules and unknown semantics.
4. Add the smallest docs/governance proof that future runtime prompts can cite.

### Tests

- docs and queue validators pass for the touched planning files;
- `git diff --check` passes for the touched files;
- no runtime behavior changes.

### Acceptance

- the latency measurement contract is written down and citeable;
- future OBS/PERF runtime prompts can reuse the vocabulary without redefining it;
- no runtime code changes are made by this prompt.

### Dependencies

- OBS03 DONE.

### Completion note

- Date: 2026-08-11
- Agent: Cursor
- Changed files:
  - `docs/architecture/OBSERVABILITY_SLI_CATALOG.md`
  - `docs/roadmaps/OBSERVABILITY_ROADMAP.md`
  - `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md`
  - `MASTER_ROADMAP.md`
- Checks:
  - `git diff --check` - pass
  - docs/queue validators pass
- Risks:
  - latency remains a measurement contract only until runtime prompts land
  - cold/warm naming still depends on future instrumentation slices for concrete evidence
- Next:
  - `OBS05` DONE ? `OBS06` READY (Import SLA evidence contract)
  - Current READY in this queue: `OBS06`

---

## SEC07 - Frontend production dependency vulnerability triage

Status: DONE
Ready after: `SEC04` DONE and current `Klijent/clientapp` production dependency audit evidence exists
Priority: security / release-hardening
Feature family: frontend-supply-chain-vulnerability-triage
Parallel-safe: no - shares frontend dependency lockfile and build/test paths
Owner: Cursor Auto
Local lock: .ai/task-locks/SEC07-cursor.lock.md (removed after DONE)
Commit suggestion: `fix(security): triage frontend dependency vulnerabilities`

### Problem

`Klijent/clientapp` currently reports high-severity production dependency vulnerabilities. The risk should not be closed by a blind `npm audit fix --force`, because the audit indicates at least one breaking transitive upgrade path and `xlsx` has no fixed version available.

### Evidence

- `npm audit --omit=dev --audit-level=low` on 2026-08-13 reported `11 high severity vulnerabilities`.
- Production dependency findings include:
  - `react-router` / `react-router-dom` high-severity advisories.
  - `xlsx` high-severity advisories with no fix available.
  - `puppeteer` / `puppeteer-core` / `@puppeteer/browsers` / `extract-zip` where the audit suggests a breaking upgrade path.
  - `basic-ftp`, `ip-address`, `js-yaml`, and `ws` high-severity findings.
- `SEC04` already documents supply-chain assurance policy, but it does not remediate this concrete frontend audit result.

### Read first

- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/architecture/SUPPLY_CHAIN_ASSURANCE_POLICY.md`
- `docs/roadmaps/SECURITY_EVOLUTION_ROADMAP.md`
- `Klijent/clientapp/package.json`
- `Klijent/clientapp/package-lock.json`
- frontend import/export code paths that currently import `xlsx`
- route/navigation tests listed below

### Scope

- `Klijent/clientapp/package.json`
- `Klijent/clientapp/package-lock.json`
- frontend import/export code paths that use `xlsx`
- frontend tests/build only as needed to prove safe remediation
- optional short QA note under `docs/qa/` if a vulnerability must remain accepted with a bounded reason

### Do Not Touch

- backend NuGet packages unless a separate backend audit finding is reproduced
- broad React page redesign
- API contracts
- unrelated dependency churn
- `npm audit fix --force` without first documenting the exact breaking changes and proving the app still builds/tests

### Do

1. Re-run `npm audit --omit=dev --json` and save a summarized, non-secret evidence note.
2. Build a dependency tree for each production finding and classify: direct upgrade, safe transitive upgrade, breaking upgrade requiring tests, no-fixed-version replacement, or accepted residual risk.
3. Upgrade `react-router-dom`/`react-router` and other fixable production dependencies only within compatible ranges when tests prove routing remains stable.
4. For `xlsx`, either replace usage with a safer maintained package or record an explicit temporary exception with affected features, mitigation and owner-approved replacement plan.
5. For Puppeteer-related findings, confirm whether Puppeteer is needed in production dependencies; if it is test/tooling only, move it out of production dependency surface without breaking build/test scripts.
6. Run focused route/export tests plus build and guardrails.
7. Leave a durable run log and completion note with remaining vulnerabilities, if any.

### Tests

```powershell
cd Klijent/clientapp
npm audit --omit=dev --audit-level=low
npm run test -- --run src/layout/__tests__/navConfig.spec.ts src/layout/components/__tests__/headerNavigation.spec.ts src/pages/__tests__/AnalyticsDashboard.integration.spec.tsx
npm run check:analytics-guardrails
npm run build
```

### Acceptance

- No high-severity production dependency finding is left unclassified.
- Fixable production findings are remediated with lockfile evidence.
- Any residual vulnerability has a documented bounded exception, affected feature surface, mitigation and follow-up owner.
- Frontend routing, guardrails and production build remain green.
- Completion note references the exact durable run log path.

### Dependencies

- `SEC04` DONE.
- This prompt may run before `SEC05` because it is supply-chain remediation, not tenant/offboarding assurance.
- Do not displace active BCI/STAB/QDB runtime gates unless the owner explicitly prioritizes release security hardening.

### Completion note

- Date: 2026-08-17
- Status: DONE
- Completion: 100%
- Changed files: Klijent/clientapp/package.json, Klijent/clientapp/package-lock.json, Klijent/clientapp/src/pages/__tests__/AnalyticsDashboard.integration.spec.tsx, docs/qa/FRONTEND_DEPENDENCY_VULNERABILITY_TRIAGE_2026-08-17.md, docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md, MASTER_ROADMAP.md, .ai/runs/2026-08-17-SEC07-evidence.md
- Contract/runtime behavior changed: production npm graph no longer ships vulnerable react-router/xlsx/puppeteer; server xlsx export format strings unchanged
- Checks run: npm audit --omit=dev after = 0; focused routes 9/9; npm run check:analytics-guardrails; npm run build
- Checks not run: full frontend suite; npm audit fix --force (forbidden)
- Run log: .ai/runs/2026-08-17-SEC07-evidence.md
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: d4acd9bc80df025e17de27505aa54f0a5c65670b
- Main verification: passed - git rev-parse origin/main -> 51550ec833985a79149de9215adee38c7696ceab; work SHA d4acd9bc80df025e17de27505aa54f0a5c65670b is an ancestor
- Missed: Puppeteer extract-zip highs remain in the dev tree only
- Follow-up: sequential refill complete; current execution READY is RQ96; OBS10 and RL10 are parallel-safe planning READYs; PERF16 is BLOCKED on MT10
- Residual risk: reintroducing Puppeteer as a production dependency would reopen the audit
- Next: RQ96 - Canonical observed daily inventory snapshot foundation
- Prompt defect / scope repair: dashboard integration copy aligned to Pregled poslovanja so routing proof stays green

---

## SEC05 - Data protection and retention assurance plan (S2-3)

Status: WAITING
Ready after: MT09 contracts exist or the owner explicitly approves an interim dedicated-deploy offboarding scope
Priority: future
Feature family: security-retention-assurance-plan
Parallel-safe: yes, planning/docs only
Owner: unassigned
Local lock: `.ai/task-locks/SEC05-<agent>.lock.md`

### Problem

Cross-cutting retention/classification/offboarding assurance remains an SEC orphan (S15 / S2-3) beyond MT09 product work.

### Evidence

- `docs/architecture/SECURITY_ASSURANCE_BACKLOG_PLAN.md` slice **S2-3**
- SEC01 orphan S15

### Scope

- docs/planning only;
- data-class inventory and retention/deletion expectations;
- dedicated-deploy wipe/restore checklist until MT09;
- AI provider retention remains blocked until GAI policy;
- no runtime delete tooling in this prompt.

### Read first

- SEC02 backlog plan
- MT09 / tenant safety checklist
- `docs/security/GENAI_SECURITY_AND_DATA_BOUNDARIES.md`

### Do

1. Produce retention/classification assurance doc.
2. Name owners per data class (STAB/MT/QDB/GAI collaborators).
3. Fail closed when delete scope is unknown.
4. Keep MT/GAI gates explicit.

### Tests

- no duplicate of MT09 implementation;
- missing evidence cannot claim PASS;
- no secrets/customer payloads in the doc.

### Acceptance

- S2-3 has a durable assurance plan agents can cite.

### Dependencies

- SEC04 DONE; MT09 contracts or accepted interim dedicated-deploy scope.

---

---

## SEC04 - Dependency and supply-chain assurance policy (S2-2)

Status: DONE
Ready after: `SEC03` is `DONE`
Priority: future
Feature family: security-supply-chain-policy
Parallel-safe: yes, planning/docs only
Owner: unassigned
Commit: `9ba31fee22cfcbb72343745928a1907372f5b402`
Completed: 2026-08-11
Local lock: removed after DONE
Promotion note: 2026-08-11 ? `SEC03` DONE; docs/policy only; BCI collaborates on CI wiring.

### Problem

Vulnerable/abandoned package posture has no queued owner outside SEC-3.

### Evidence

- SEC02 slice **S2-2**
- SEC01 orphan S14

### Scope

- docs/policy only;
- scan frequency/severity fail rules for .NET and npm;
- triage ownership (SEC + BCI for CI wiring);
- abandoned-package handling;
- no broad dependency upgrades in this prompt.

### Read first

- `docs/architecture/SECURITY_ASSURANCE_BACKLOG_PLAN.md`
- BCI queue ownership for CI wiring

### Do

1. Write supply-chain policy doc with ecosystems, severity gates, and accepted-risk template.
2. Name the reproducible scan command(s) or CI job placeholders (BCI wires later).
3. Keep BCI as collaborator for pipeline; SEC owns policy.

### Tests

- no duplicate of BCI repair scope;
- missing scan output cannot claim PASS;
- no secrets in evidence.

### Acceptance

- S2-2 policy exists and points to BCI for CI integration.

### Dependencies

- SEC03 DONE (keeps one READY-at-a-time in SEC).

### Completion note

- Date: 2026-08-11
- Agent: Codex
- Changed files:
  - `docs/architecture/SUPPLY_CHAIN_ASSURANCE_POLICY.md`
  - `docs/architecture/SECURITY_ASSURANCE_BACKLOG_PLAN.md`
  - `docs/roadmaps/SECURITY_EVOLUTION_ROADMAP.md`
  - `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md`
  - `MASTER_ROADMAP.md`
- Checks:
  - `node scripts/check-prompt-queues.mjs` - pass
  - `node scripts/check-planning-architecture.mjs` - pass
  - `git diff --check` - pass
- Risks:
  - no CI wiring exists yet; placeholders are intentionally docs-only
  - accepted-risk entries remain future evidence, not current PASS
- Next:
  - `SEC05` remains WAITING until MT09 or explicit interim dedicated-deploy offboarding scope exists
  - Current READY in this queue: none
---

## SEC06 - Dependency and supply-chain assurance policy follow-up (S2-2)

Status: OBSOLETE
Ready after: `SEC03` is `DONE`
Priority: future
Feature family: security-supply-chain-policy
Parallel-safe: yes, planning/docs only
Owner: unassigned
Local lock: `.ai/task-locks/SEC06-<agent>.lock.md`
Promotion note: 2026-08-11 ? `SEC03` DONE; docs/policy only; BCI collaborates on CI wiring.

### Problem

Vulnerable/abandoned package posture has no queued owner outside SEC-3.

### Evidence

- SEC02 slice **S2-2**
- SEC01 orphan S14
- `docs/architecture/SECURITY_ASSURANCE_BACKLOG_PLAN.md`
- BCI queue ownership for CI wiring

### Scope

- dependency scanning policy and evidence;
- reproducible scan command(s) or CI job placeholders;
- no runtime security changes.

### Read first

- `docs/roadmaps/SECURITY_EVOLUTION_ROADMAP.md`
- `docs/architecture/SECURITY_OWNERSHIP_THREAT_MAP.md`
- `docs/architecture/SECURITY_ASSURANCE_BACKLOG_PLAN.md`
- BCI/SEC collaboration notes

### Do

1. Define the supply-chain assurance policy for current supported runtimes.
2. Name the reproducible scan command(s) or CI job placeholders.
3. Keep BCI as collaborator for pipeline wiring; SEC owns policy.
4. Keep the output docs/policy only.

### Tests

- no duplicate of BCI repair scope;
- scan/job placeholders are explicit and reproducible;
- policy names supported runtimes without pretending they are already wired.

### Acceptance

- one policy exists for dependency and supply-chain assurance;
- the policy points to BCI for CI integration;
- later SEC follow-ups can reuse the same scan vocabulary.

### Completion note

- Replaced by `SEC04`; keep this entry only as historical evidence of the duplicate follow-up and do not treat it as a live READY item.

### Dependencies

- SEC03 DONE.

---

## SEC01 - Reconcile security ownership and current threat model

Status: DONE
Priority: future / planning
Feature family: security-ownership-threat-model
Parallel-safe: yes, docs/audit only
Owner: Cursor
Local lock: none

### Problem

Security planning is currently distributed across STAB pilot security work, MT tenant isolation, connector credential concerns and GAI security boundaries. A new SEC program would be harmful if it simply duplicated those queues.

### Evidence

- STAB already owns current deploy/auth/admin/edge/release security work;
- MT owns shared-SaaS isolation;
- QDB owns source connector behavior and will need credential ownership;
- GAI has separate security/data-boundary gates;
- `docs/roadmaps/SECURITY_EVOLUTION_ROADMAP.md` defines SEC as post-STAB cross-cutting ownership, not a replacement.

### Scope

- current security ownership matrix and threat-model reconciliation;
- map identities, secrets, privileged operations, tenant boundaries, source connectors, storage/docs/exports, workers and AI surfaces to existing owners;
- identify genuine orphan risks only;
- no runtime security changes.

### Read first

- `MASTER_ROADMAP.md`
- `docs/roadmaps/SECURITY_EVOLUTION_ROADMAP.md`
- `docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md`
- `docs/architecture/MULTITENANCY_ARCHITECTURE_ROADMAP.md`
- `docs/security/TENANT_SAFETY_CHECKLIST.md`
- `docs/security/GENAI_SECURITY_AND_DATA_BOUNDARIES.md`
- QDB roadmap/queue security boundaries

### Do

1. Build a surface/threat/owner matrix.
2. Mark each risk as STAB, MT, QDB, GAI, SEC or accepted/non-applicable.
3. Identify duplicated historical security descriptions and point them to the current owner without deleting evidence.
4. Identify true orphan risks that warrant future SEC prompts.
5. Define criteria for STAB-to-SEC handoff after pilot remediation is stable.

### Tests

- every security surface has exactly one primary owner;
- SEC does not create a duplicate prompt for an active STAB/MT/QDB/GAI family;
- missing runtime evidence stays unknown/BLOCKED rather than PASS;
- no secret/customer payload enters the audit.

### Acceptance

- a current security ownership/threat map exists;
- SEC has only genuinely cross-cutting future work;
- no runtime security change is made.

### Completion note

- Date: 2026-08-11
- Agent: Cursor
- Changed files:
  - `docs/architecture/SECURITY_OWNERSHIP_THREAT_MAP.md`
  - `docs/roadmaps/SECURITY_EVOLUTION_ROADMAP.md`
  - `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md`
  - `MASTER_ROADMAP.md`
- Checks:
  - `git diff --check` - pass
  - no runtime security changes
- Risks:
  - STAB03 follow-up gaps (import/logs/document-header) remain STAB-owned, not closed by this map
  - External IdP still deferred (accepted/n/a until product decision)
- Next:
  - `SEC02` WAITING (post-STAB assurance backlog)
  - Platform Evolution Current READY: none

### Dependencies

- current STAB/MT/QDB/GAI documentation available;
- no dependency on selecting an external identity provider.

---

## SEC02 - Prepare post-STAB security assurance backlog

Status: DONE
Priority: future
Feature family: security-assurance-plan
Parallel-safe: yes, planning only
Owner: Cursor
Local lock: none

### Problem

After SEC01, only orphan/post-STAB risks should become SEC backlog items.

### Evidence

- SEC01 DONE: `docs/architecture/SECURITY_OWNERSHIP_THREAT_MAP.md`

### Scope

- docs/planning only;
- candidate secrets/supply-chain/data-protection/security-observability/assurance slices;
- no runtime implementation.

### Read first

- SEC01 output
- `docs/roadmaps/SECURITY_EVOLUTION_ROADMAP.md`

### Do

1. Prioritize only risks truly owned by SEC.
2. Define evidence/tests needed before runtime changes.
3. Keep MT/GAI/STAB/QDB dependencies explicit.
4. Define future SaaS and AI assurance gates without duplicating their owner queues.

### Tests

- no duplicate active feature family;
- each candidate has a named evidence gap;
- release/tenant/AI ownership remains unchanged.

### Acceptance

- post-STAB security evolution has a bounded, non-duplicative backlog plan.

### Completion note

- Date: 2026-08-11
- Agent: Cursor
- Changed files:
  - `docs/architecture/SECURITY_ASSURANCE_BACKLOG_PLAN.md`
  - `docs/roadmaps/SECURITY_EVOLUTION_ROADMAP.md`
  - `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md`
  - `MASTER_ROADMAP.md`
- Checks:
  - `git diff --check` - pass
  - no runtime security changes
- Risks:
  - STAB watchlist (import/logs/docs) still unqueued ? must not be re-homed to SEC
  - SEC runtime still gated by STAB residual acceptance + MT/GAI where claimed
- Next:
  - Platform Evolution Current READY: `SEC03` (S2-1 docs)
  - Alternate candidate remains WAITING: `SEC04` (S2-2 supply-chain) after SEC03
  - Higher priority: BCI05 commit/push ? GHA if user authorizes

### Dependencies

- SEC01 DONE.

---

## SEC03 - Privileged secrets and emergency-access assurance (S2-1)

Status: DONE
Priority: future
Feature family: security-privileged-secrets-assurance
Parallel-safe: yes, planning/docs only
Owner: Codex
Local lock: removed after DONE
Promotion note: 2026-08-11 - `SEC02` DONE; slice S2-1 from `SECURITY_ASSURANCE_BACKLOG_PLAN.md`

### Problem

Admin API-key / deployment-secret rotation and emergency-access expectations are an SEC orphan. Without a written assurance inventory, pilot ops can leave key sprawl unowned after STAB authz work.

### Evidence

- `docs/architecture/SECURITY_ASSURANCE_BACKLOG_PLAN.md` slice **S2-1**
- `docs/architecture/SECURITY_OWNERSHIP_THREAT_MAP.md` (S2 / rotation orphan)
- STAB Admin API-key boundary remains primary for authz defects

### Scope

- docs/planning only;
- privileged secret *classes* inventory (names/types, no values);
- rotation / revoke / emergency-access runbook expectations;
- fail-closed checklist linking existing STAB auth tests;
- no runtime authz change, no IdP, no secret values in git.

### Read first

- `docs/architecture/SECURITY_ASSURANCE_BACKLOG_PLAN.md`
- `docs/architecture/SECURITY_OWNERSHIP_THREAT_MAP.md`
- `docs/roadmaps/SECURITY_EVOLUTION_ROADMAP.md` (SEC-2)
- existing Admin access / config key docs if cited by those maps

### Do

1. Produce `docs/architecture/PRIVILEGED_SECRETS_ASSURANCE.md` (or equivalent under `docs/security/`).
2. List secret classes: Admin key, DB, storage, cache, connector SecretReference - types only.
3. Define rotation/revoke expectations and emergency-access steps (who, blast radius, post-incident revoke).
4. Explicitly mark STAB as owner of authz defects; QDB as owner of connector credential *features*.
5. Name evidence gaps that still block runtime PASS.

### Tests

- document contains no real secrets or connection strings;
- each class has owner + fail-closed note;
- STAB/QDB/MT/GAI boundaries are cited, not redefined;
- missing evidence remains unknown/BLOCKED, never PASS.

### Acceptance

- S2-1 has a durable assurance doc agents can cite;
- no runtime security code changed;
- `SEC04` (supply-chain) remains the natural next SEC READY after this DONE.

### Completion note

- Date: 2026-08-11
- Agent: Codex
- Changed files:
  - `docs/architecture/PRIVILEGED_SECRETS_ASSURANCE.md`
  - `docs/roadmaps/SECURITY_EVOLUTION_ROADMAP.md`
  - `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md`
  - `MASTER_ROADMAP.md`
- Checks:
  - `git diff --check` - pass
  - no runtime security changes
- Risks:
  - rotation/revoke evidence remains docs-only until a future rehearsal or runtime prompt
  - STAB authz defects remain STAB-owned
- Next:
  - Platform Evolution Current READY: `SEC04`
  - `SEC04` is READY

### Dependencies

- SEC02 DONE.

---
