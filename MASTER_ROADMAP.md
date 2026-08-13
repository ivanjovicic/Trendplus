# Trendplus Master Roadmap

Updated: 2026-08-13
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
| BCI | `MASTER_ROADMAP.md` / `docs/ai/BACKEND_CI_REPAIR_PROMPT_QUEUE.md` + `docs/ai/BACKEND_CI_REPAIR_EVIDENCE_ADDENDUM.md` | none | `BCI01`/`BCI05`/`BCI06` DONE. Green GHA `31674533356` on `f1f5a17`; mixed-solution Windows/VS wrappers observed via JavaScript SDK `1.0.3982316`. No remaining BCI READY. | Evidence/docs only | Current execution READY is `RQ104` |
| STAB | `MASTER_ROADMAP.md` / `docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md` | none (`STAB12` DONE) | `STAB12` DONE = unauthenticated `X-User-*` headers no longer grant document generate/list/export privilege. No remaining STAB READY. | Evidence/docs when paths clear | Current execution READY is `RQ104`; `QDB06` still needs migration approval |
| RQ | `docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md` + `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md` + source addenda | `RQ104` | `RQ103` DONE = action outcome not-measured and learning-eligibility proofs. Owner-promoted pack: `RQ104` READY. `RQ105` WAITING = operational fallback honesty. QDB exclusive work is no longer in progress. | Selected docs/tests only | `RQ105` after `RQ104` |
| P-UI | `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md` + least-improved addendum + `docs/roadmaps/ANALYTICS_UI_PREMIUM_ROADMAP.md` | `P-UI-21` | P-UI-20 DONE. Next is empty-success without KPI totals and shared Actions ErrorState. `P-UI-22` WAITING. | Yes, when path-safe vs RQ104 | `P-UI-22` |
| QDB | `docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md` + `docs/architecture/DATA_SOURCE_CONNECTOR_ROADMAP.md` | none (`QDB05` DONE) | `QDB01`-`QDB05` DONE. Mapping preview is request-scoped and secret-safe. `QDB06` WAITING until the owner approves a database migration. | Docs/tests when paths clear | Owner migration approval -> QDB06 |
| MT | `docs/ai/MULTITENANCY_PROMPT_QUEUE.md` + `docs/architecture/MULTITENANCY_ARCHITECTURE_ROADMAP.md` | none (`MT01` DONE) | `MT02` WAITING on owner approval of identity/membership source or single-tenant API-key binding. | Contract docs when paths clear | Owner decision -> MT02 |
| GAI | `docs/ai/GENAI_PRODUCT_PROMPT_QUEUE.md` + `docs/ai/GENAI_COPILOT_ROADMAP.md` | none | Blocked by current core-pilot/release evidence | Planning/audit only | Core pilot ready, then explicit GenAI entry gate |
| DEX | `docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md` + `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md` | `DEX18` | `DEX17` DONE. Next is docs-only Executive Decision Board explainability reuse contract. | Yes, docs/contracts | Executive Board explainability reuse |
| RL | same queue/roadmap as DEX | `RL07` | `RL06` DONE = measurement-only statistics projection on outcome summary. Next is docs-only review-surface contract. Duplicate `RL08` remains OBSOLETE. | Yes, docs/contracts | Measurement statistics review surface |
| DT | same queue/roadmap as DEX | `DT07` | `DT06` DONE = export/retrospective contract. Next is runtime export/report over Slice-2. | Yes, backend/frontend report paths when path-safe | Timeline export runtime |
| PERF | `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md` + `docs/roadmaps/PERFORMANCE_ROADMAP.md` | `PERF15` | PERF14 DONE; D5 cache footprint measured in `.ai/runs/2026-08-12-PERF13-evidence.md`; D6 import overlap measured in `.ai/runs/2026-08-12-PERF14-evidence.md`; D8 still needs MT | Yes, docs/contracts when paths clear | D8 shared-saas evidence gate |
| OBS | `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md` + `docs/roadmaps/OBSERVABILITY_ROADMAP.md` | `OBS08` | `OBS01`-`OBS07` DONE; next is worker-SLA contract. `OBS09` WAITING after OBS08. | Yes, docs/contracts | Worker SLA evidence contract |
| SEC | `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md` + `docs/roadmaps/SECURITY_EVOLUTION_ROADMAP.md` | `SEC07` | `SEC05` waits on MT09 or an explicitly approved interim dedicated-deploy offboarding scope. `SEC07` is a frontend dependency vulnerability triage prompted by current `npm audit --omit=dev` evidence. | Yes, scoped dependency/security work when paths clear | Frontend dependency vulnerability triage |

Owner refill 2026-08-13 keeps a live sequential backlog of 15 prompts. Current execution starts at `RQ104`. Do not promote `QDB06`, `MT02` or `GAI01` without the named migration, tenant-identity or pilot-ready decision.

| # | ID | Status | Program |
|---|---|---|---|
| 1 | `RQ100` | DONE | RQ |
| 2 | `RQ101` | DONE | RQ |
| 3 | `RQ102` | DONE | RQ |
| 4 | `RQ103` | DONE | RQ |
| 5 | `RQ104` | READY | RQ |
| 6 | `RQ105` | WAITING | RQ |
| 7 | `P-UI-21` | READY | P-UI |
| 8 | `P-UI-22` | WAITING | P-UI |
| 9 | `DEX18` | READY | DEX |
| 10 | `RL07` | READY | RL |
| 11 | `DT07` | READY | DT |
| 12 | `PERF15` | READY | PERF |
| 13 | `OBS08` | READY | OBS |
| 14 | `OBS09` | WAITING | OBS |
| 15 | `SEC07` | READY | SEC |

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
