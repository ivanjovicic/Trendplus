# Trendplus Master Roadmap

Updated: 2026-08-28
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
| BCI | `MASTER_ROADMAP.md` / `docs/ai/BACKEND_CI_REPAIR_PROMPT_QUEUE.md` + `docs/ai/BACKEND_CI_REPAIR_EVIDENCE_ADDENDUM.md` | none | Historical `BCI01`/`BCI05`/`BCI06` remain DONE, and `BCI10` is DONE after re-closing the backend suite with the test-host checkpoint-sync registration fix. | No | queue complete unless a new red current-main run appears |
| STAB | `MASTER_ROADMAP.md` / `docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md` | none | A same-day 2026-08-27 API-only recheck confirmed the canonical Render runtime SHA `6ecbfa67a7304c3cbeeb71755a35255e766c8e24` is contained in current `main`, but refresh workers are still unregistered and direct read-only reconciliation/browser proof is still missing. Current operator evidence also reports Render web SHA `d38aafd405a9213a279bb76664cde4bf69ddf83b`, no worker service, and Neon storage `0.54/0.5 GB`. `STAB16` remains BLOCKED on provider worker access, read-only audit connection and Neon storage capacity; GenAI remains BLOCKED by the core-pilot/release gate. | No | storage triage, then restore worker/reconciliation proof through STAB16 |
| RQ | `docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md` + `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md` + active addenda | `RQ154` | `RQ139` is PARTIAL after bounded numeric-state hardening and `RQ140` is PARTIAL after local pre/post comparability hardening; live database/refresh/browser proof remains with `STAB16`, while cross-surface parity and runtime schema/refresh proof still need follow-up. `RQ144`, `RQ151`, `RQ152` and `RQ153` are DONE for their bounded local contracts. The 2026-09-05 stability audit found bounded Daily Sales, Dashboard trend, pre/post coverage, Product Decision, inventory and Analytics Details gaps; only `RQ154` is promoted, while `RQ155`-`RQ161` remain WAITING. Forecast, Shopify and similar excluded work remains WAITING. | Selected docs/tests only | Execute `RQ154`, then advance `RQ155`-`RQ161`; keep `RQ141`/`RQ145`/`RQ146` and live gates behind dependencies/STAB16 |
| P-UI | `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md` + least-improved addendum + `docs/roadmaps/ANALYTICS_UI_PREMIUM_ROADMAP.md` | none | P-UI-21 DONE. P-UI-22 DONE. Queue complete. | Yes | queue complete |
| QDB | `docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md` + `docs/architecture/DATA_SOURCE_CONNECTOR_ROADMAP.md` | none (`QDB09` is DONE after the 2026-08-22 delivery) | `QDB01`-`QDB06` DONE. Durable checkpoints are `ConnectionId + MappingProfileId + SourceStream` with `TenantScope=n/a_dedicated`, and SQL Server end-to-end checkpoint application plus production caller proof are now delivered. `QDB07` stays WAITING after `QDB09` plus release gates. | Docs/tests when paths clear | SQL Server e2e through the checkpoint engine, then QDB07 |
| MT | `docs/ai/MULTITENANCY_PROMPT_QUEUE.md` + `docs/architecture/MULTITENANCY_ARCHITECTURE_ROADMAP.md` | none (`MT01` DONE) | `MT02` WAITING on owner approval of identity/membership source or single-tenant API-key binding. | Contract docs when paths clear | Owner decision -> MT02 |
| GAI | `docs/ai/GENAI_PRODUCT_PROMPT_QUEUE.md` + `docs/ai/GENAI_COPILOT_ROADMAP.md` | none | Blocked by current core-pilot/release evidence | Planning/audit only | Core pilot ready, then explicit GenAI entry gate |
| DEX | `docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md` + `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md` | none | `DEX19` DONE = Executive Board explainability runtime. `DEX20` DONE = cross-family alternatives contract on main. | Yes, docs/contracts when path-safe | Alternatives contract on main, then optional runtime reuse |
| RL | same queue/roadmap as DEX | none | `RL10` DONE = Slice 4 advisory calibration contract. `RL11` DONE = advisory calibration runtime gate contract. `RL12` is WAITING for a causal outcome-comparison planning gate. Duplicate `RL08` remains OBSOLETE. | Yes, docs/contracts when path-safe | keep RL12 WAITING until its named analytics evidence dependencies and explicit owner promotion |
| DT | same queue/roadmap as DEX | none | `DT08` DONE = Slice-5 hardening. `DT09` DONE = first-class timestamp contract on main. `DT10` DONE = derived-clock honesty on main. | Yes, docs/contracts when path-safe | DT queue complete |
| PERF | `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md` + `docs/roadmaps/PERFORMANCE_ROADMAP.md` | none | `PERF17` DONE = measured frontend bundle baseline and guardrail; `PERF16` remains BLOCKED until `MT10` or an owner-recorded shared-SaaS gate. PERF15 DONE = D8 stays MT-owned and `n/a_dedicated`. | Yes, docs/contracts when paths clear | D8 reopen after MT fixtures |
| OBS | `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md` + `docs/roadmaps/OBSERVABILITY_ROADMAP.md` | none (`OBS11` DONE) | `OBS01`-`OBS11` DONE. Operational dashboard panel inventory / correlation contract frozen. | Yes, docs/contracts | queue complete |
| SEC | `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md` + `docs/roadmaps/SECURITY_EVOLUTION_ROADMAP.md` | none | `SEC08` DONE = reproducible frontend audit gates for both workspace lockfiles; `SEC07` DONE = clientapp production npm audit is 0. `SEC05` waits on MT09 or an explicitly approved interim dedicated-deploy offboarding scope. | Yes, scoped dependency/security work when paths clear | SEC05 after MT09 |

Owner refill 2026-08-13 sequential backlog of 15 prompts is complete. Owner refill 2026-08-20 promoted/executed `DT09` + `DEX20` + `OBS11` + `STAB13` as docs DONE on main (`docs/planning/QUEUE_REFILL_2026-08-20.md`). Audit follow-up delivery on 2026-08-22 closed `BCI10`, `STAB14`, `STAB15`, `RQ108`, `RQ109`, and `QDB09` on current `main`. `RQ110`, `RQ111`, `RQ112`, `RQ113`, `RQ114`, `RQ115`, `RQ116`, `RQ117`, `RQ118`, `RQ119`, `RQ120`, `RQ121`, `RQ122`, `RQ123`, `RQ124`, `RQ127`, `RQ129`, `RQ134`, `RQ135`, and `RQ63` are DONE; BCI and QDB have no current READY prompt. The first 2026-08-27 production audit created `STAB16` BLOCKED and `RQ128` WAITING; a same-day API-only queue audit then confirmed Render runtime `6ecbfa67a7304c3cbeeb71755a35255e766c8e24` is contained in current `main`, repaired stale STAB/RQ routing truth, and promoted `RQ129` as the live Decision Board fake-confidence cleanup follow-up, which is now landed on `main`. Planning sync on 2026-08-28 added `RQ130` and `RQ131` as later WAITING vendor baseline/trend-truth follow-ups in the active cross-surface addendum without changing the current RQ READY state. The 2026-09-01 cache-invalidation review closed `RQ134` as the supplier-summary freshness follow-up after aggregate refresh and closed `RQ135` as the trust-bearing cache parity follow-up after data-quality snapshot refresh, returning the current RQ READY state to `none`. On 2026-09-02, `PERF17` was explicitly promoted and completed as a parallel-safe frontend bundle measurement/guardrail run; the PERF queue returned to `none`. On 2026-09-02, `SEC08` was explicitly promoted and completed as a parallel-safe reproducible frontend dependency-audit gate; the SEC queue returned to `none`. `QDB07` remains WAITING for release gates. `DT10`, `RQ107`, and `RL11` are DONE. `PERF16` stays BLOCKED on `MT10`. Do not promote `MT02`, `GAI01`, or `SEC05` without their named gates.
Owner refill 2026-08-13 sequential backlog of 15 prompts is complete. Owner refill 2026-08-20 promoted/executed `DT09` + `DEX20` + `OBS11` + `STAB13` as docs DONE on main (`docs/planning/QUEUE_REFILL_2026-08-20.md`). Audit follow-up delivery on 2026-08-22 closed `BCI10`, `STAB14`, `STAB15`, `RQ108`, `RQ109`, and `QDB09` on current `main`. `RQ110`, `RQ111`, `RQ112`, `RQ113`, `RQ114`, `RQ115`, `RQ116`, `RQ117`, `RQ118`, `RQ119`, `RQ120`, `RQ121`, `RQ122`, `RQ123`, `RQ124`, `RQ127`, `RQ129`, `RQ134`, `RQ135`, and `RQ63` are DONE; BCI and QDB have no current READY prompt. The first 2026-08-27 production audit created `STAB16` BLOCKED and `RQ128` WAITING; a same-day API-only queue audit then confirmed Render runtime `6ecbfa67a7304c3cbeeb71755a35255e766c8e24` is contained in current `main`, repaired stale STAB/RQ routing truth, and promoted `RQ129` as the live Decision Board fake-confidence cleanup follow-up, which is now landed on `main`. Planning sync on 2026-08-28 added `RQ130` and `RQ131` as later WAITING cross-surface vendor baseline/trend-truth follow-ups in the active cross-surface addendum without changing the current RQ READY state. The 2026-09-01 cache-invalidation review closed `RQ134` as the supplier-summary freshness follow-up after aggregate refresh and closed `RQ135` as the trust-bearing cache parity follow-up after data-quality snapshot refresh, returning the current RQ READY state to `none`. On 2026-09-02, `PERF17` was explicitly promoted and completed as a parallel-safe frontend bundle measurement/guardrail run; the PERF queue returned to `none`. On 2026-09-02, `SEC08` was explicitly promoted and completed as a parallel-safe reproducible frontend dependency-audit gate; the SEC queue returned to `none`. `QDB07` remains WAITING for release gates. `DT10`, `RQ107`, and `RL11` are DONE. `PERF16` stays BLOCKED on `MT10`. On 2026-09-05, owner-promoted `RQ139` and SQL-owner `Q83` as independently runnable current prompts with disjoint scopes; `RQ140`-`RQ146` remain WAITING behind their declared dependencies. Do not promote `MT02`, `GAI01`, or `SEC05` without their named gates.

| # | ID | Status | Program |
|---|---|---|---|
| 1 | `RQ100` | DONE | RQ |
| 2 | `RQ101` | DONE | RQ |
| 3 | `RQ102` | DONE | RQ |
| 4 | `RQ103` | DONE | RQ |
| 5 | `RQ104` | DONE | RQ |
| 6 | `RQ105` | DONE | RQ |
| 7 | `P-UI-21` | DONE | P-UI |
| 8 | `P-UI-22` | DONE | P-UI |
| 9 | `DEX18` | DONE | DEX |
| 10 | `RL07` | DONE | RL |
| 11 | `DT07` | DONE | DT |
| 12 | `PERF15` | DONE | PERF |
| 13 | `OBS08` | DONE | OBS |
| 14 | `OBS09` | DONE | OBS |
| 15 | `SEC07` | DONE | SEC |

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
