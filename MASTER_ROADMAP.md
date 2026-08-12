# Trendplus Master Roadmap

Updated: 2026-08-12
Repository: `ivanjovicic/Trendplus`
Status: canonical planning entry point

This file is the single routing entry point for Trendplus planning. It does not replace detailed roadmaps, audits, queues, or historical evidence. It tells an agent which program owns a topic, what is currently runnable, what is blocked, what may run in parallel, and what milestone comes next.

## Canonical planning rule

1. Read `AGENTS.md` and `.github/copilot-instructions.md`.
2. Read `docs/ai/AGENT_START_HERE.md`.
3. Read this file.
4. Follow the owner queue named here.
5. Read only the target prompt plus its `Read first` documents.

If an older queue addendum, audit, status report, or completion note conflicts with this file and the current owner queue header, treat the older statement as historical evidence, not current routing.

## Existing program priority

The existing execution priority is preserved:

1. Backend CI Repair (`BCI`)
2. Stabilization / Release / Security (`STAB`)
3. Analytics Reliability (`RQ`)
4. remaining STAB work
5. remaining analytics correctness
6. Data Connector (`QDB`)
7. Multi-Tenant (`MT`)
8. GenAI (`GAI`)

The existing Premium UI program (`P-UI`) is a supplemental presentation lane. Its current task may run only when path-safe and must never displace the priority chain above or repair analytics correctness through frontend invention.

A historical task ID does not become READY merely because it appears in this priority list. Always use the current queue status. If a higher-priority program has no READY task, do not invent one; use its documented blocker or owner-gated promotion rule.

The new DEX/RL/DT/PERF/OBS/SEC programs are future planning lanes. Their first READY prompts are planning/contract tasks only. They do not authorize lower-priority runtime implementation ahead of the existing priority chain.

## Program routing matrix

| Program | Owner queue / roadmap | Current READY | Blocked by / current truth | Parallel-safe planning | Next milestone |
|---|---|---|---|---|---|
| BCI | `MASTER_ROADMAP.md` / `docs/ai/BACKEND_CI_REPAIR_PROMPT_QUEUE.md` + `docs/ai/BACKEND_CI_REPAIR_EVIDENCE_ADDENDUM.md` | `BCI05` | `BCI01` is PARTIAL. `BCI08` DONE (CI env isolation). `BCI05` READY for green GHA re-entry after commit/push. | Evidence/docs only | BCI05 green GHA -> BCI01 DONE |
| STAB | `MASTER_ROADMAP.md` / `docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md` | none | `STAB09` DONE. Residual authz watchlist (import/logs/docs) unqueued — STAB-owned, not SEC. | Evidence/docs when paths clear | Queue STAB follow-ups or accept residual risk |
| RQ | `docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md` + `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md` + source addenda | none (`RQ95` DONE) | BCI05 override active until GHA green; do not promote RQ WAITING over BCI05. | Selected docs/tests only | BCI05 GHA reconciliation |
| P-UI | `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md` + least-improved addendum + `docs/roadmaps/ANALYTICS_UI_PREMIUM_ROADMAP.md` | none | P-UI-16 DONE (no fake reliability + empty/copy on PreNivelacijaPriorityPage). P-UI-17 DONE (ControlBar + DataTable chrome). P-UI-18 DONE (SupplierFootwearAnalyticsPage chrome). | Yes, when paths clear | Await owner-promoted next P-UI prompt |
| QDB | `docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md` + `docs/architecture/DATA_SOURCE_CONNECTOR_ROADMAP.md` | none (`QDB01`/`QDB02` DONE) | `QDB03` WAITING until backend CI executes real tests successfully (`BCI05`/`BCI01` still open). | Docs/tests when paths clear | Green BCI -> promote QDB03 |
| MT | `docs/ai/MULTITENANCY_PROMPT_QUEUE.md` + `docs/architecture/MULTITENANCY_ARCHITECTURE_ROADMAP.md` | none (`MT01` DONE) | `MT02` WAITING on owner approval of identity/membership source or single-tenant API-key binding. | Contract docs when paths clear | Owner decision -> MT02 |
| GAI | `docs/ai/GENAI_PRODUCT_PROMPT_QUEUE.md` + `docs/ai/GENAI_COPILOT_ROADMAP.md` | none | Blocked by current core-pilot/release evidence | Planning/audit only | Core pilot ready, then explicit GenAI entry gate |
| DEX | `docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md` + `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md` | `DEX11` | `DEX10` DONE = immutable evidence snapshot on action. Next is docs-only cross-family readiness. | Yes, docs/contracts | Cross-family explainability readiness |
| RL | same queue/roadmap as DEX | `RL05` | `RL04` DONE = lifecycle + learning eligibility. Next is docs-only measurement-only statistics contract. | Yes, docs/contracts | Measurement-only statistics projection |
| DT | same queue/roadmap as DEX | `DT06` | `DT05` DONE = Slice-2 filtered timeline. Next is docs-only export/report contract. | Yes, docs/contracts | Timeline export/retrospective contract |
| PERF | `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md` + `docs/roadmaps/PERFORMANCE_ROADMAP.md` | `PERF10` | PERF10 evidence index captured in `.ai/runs/2026-08-12-PERF10-evidence.md` | Yes, measurement when paths clear | first G10 dedicated evidence pack |
| OBS | `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md` + `docs/roadmaps/OBSERVABILITY_ROADMAP.md` | `OBS08` | `OBS01`-`OBS07` DONE; analytics SLA evidence exists and next task is worker-SLA contract/evidence. | Yes, docs/contracts | Worker SLA evidence contract |
| SEC | `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md` + `docs/roadmaps/SECURITY_EVOLUTION_ROADMAP.md` | none (`SEC04` DONE; `SEC05` WAITING) | `SEC05` waits on MT09 or an explicitly approved interim dedicated-deploy offboarding scope. | Yes, docs only when promoted | Data protection/retention assurance plan after dependency decision |

## Product and process documents

- Product direction: `docs/product/PRODUCT_VISION.md`
- Feature flow: `docs/planning/FEATURE_LIFECYCLE.md`
- Business milestones: `docs/roadmaps/BUSINESS_ROADMAP.md`
- Architecture decisions: `docs/architecture/ADRS.md`
- Planning consolidation evidence: `docs/planning/PLANNING_CONSOLIDATION_AUDIT_2026-08-08.md`
- Latest prompt/commit implementation audit: `docs/qa/PROMPT_IMPLEMENTATION_AUDIT_2026-08-10.md`
- Current retail analytics market/capability gap audit: `docs/qa/RETAIL_ANALYTICS_COMPETITIVE_GAP_AUDIT_2026-08-12.md`

## Decision Intelligence boundary

Decision Intelligence is not a synonym for analytics. Analytics describes and measures the business. Decision Intelligence links evidence to a recommended decision, exposes why that decision was made, records alternatives, tracks what happened after action, and learns from outcomes.

The deterministic order is:

`evidence -> decision -> explanation -> alternatives -> action -> execution -> outcome -> learning`

No AI dependency is required for DEX, RL, or DT. LLMs may later explain already-authoritative evidence, but they must not become the source of truth for confidence, recommendation, outcome, or decision history.

## Milestone routing

| Milestone | Must be satisfied primarily by |
|---|---|
| Pilot Ready | BCI, STAB, RQ, OBS evidence |
| First Customer | STAB, QDB, OBS, SEC, deterministic DEX foundations |
| 10 Customers | QDB, MT staged isolation, PERF, OBS, SEC |
| 50 Customers | MT shared-SaaS gates, PERF scalability, OBS SLA evidence, SEC operational hardening, RL/DT evidence |
| SaaS Ready | MT release gate + tenant-owned QDB persistence + PERF/OBS/SEC gates |
| AI Ready | Core pilot ready + GAI security/evaluation gate + tenant boundaries where applicable; deterministic decision evidence remains authoritative |

Detailed milestone acceptance belongs in `docs/roadmaps/BUSINESS_ROADMAP.md`.

## Competitive capability priority

The current market-gap audit confirms that Trendplus should keep its explainable retail-decision positioning instead of chasing generic BI feature parity. Depth-first priorities after the active release gate are:

`release truth -> source adaptability -> observed historical inventory -> exception/digest delivery -> validated forecasting -> controlled scenarios -> shared SaaS/AI later`

The audit is planning evidence only. It does not create a competing READY queue. New work must still map to an existing owner first.

## Historical/current separation

- `docs/ai/NEXT_PROMPT_QUEUE.md` is a historical ledger and is never a current router.
- Dated QA/audit documents remain immutable evidence snapshots unless a document explicitly declares itself current.
- Addendum prose such as "main queue READY RQ01" or old "next READY" completion notes is historical when it conflicts with the current queue header and this master roadmap.
- Do not delete historical evidence to make routing look clean. Add a current pointer or archive classification instead.

## Governance checks

Before claiming planning consolidation complete, run:

```text
node scripts/check-prompt-queues.mjs
node scripts/check-prompt-queues.mjs --self-test
node scripts/check-planning-architecture.mjs
node scripts/check-planning-architecture.mjs --self-test
```

The planning validator owns the new master/roadmap/queue linkage. The prompt validator owns active execution queues, including BCI and its evidence addendum, plus the legacy/current queues it inventories.

## Change rule

When a feature is proposed:

1. map it to an existing program first;
2. create a new program only when ownership is genuinely different;
3. update the roadmap before creating implementation work;
4. expose at most one READY prompt per program;
5. keep all later prompts WAITING until dependencies are met;
6. update this file only when ownership, current READY, blocking relationship, or next milestone changes.

Do not copy implementation detail into this file. The owner queue is the implementation contract.
