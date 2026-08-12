# Observability Service-Level Vocabulary

Status: authoritative OBS05 contract  
Date: 2026-08-11  
Roadmap: `docs/roadmaps/OBSERVABILITY_ROADMAP.md` (OBS-3)  
Related:

- `docs/architecture/OBSERVABILITY_SLI_CATALOG.md`
- `docs/architecture/OBSERVABILITY_ANALYTICS_SLA_EVIDENCE_CONTRACT.md`
- `docs/architecture/OBSERVABILITY_INSTRUMENTATION_ROLLOUT_PLAN.md`
- OBS04 latency measurement contract (inside SLI catalog + PERF01 discipline)

## Purpose

Give support, agents and future OBS runtime prompts a shared vocabulary for:

- API availability;
- import service levels;
- analytics freshness / refresh service levels;
- worker processing service levels;
- report / export generation service levels;
- when error budgets may be discussed.

This document is **documentation only**. It does not invent numerical targets, publish customer SLA percentages, choose a vendor, or change runtime code.

## Core distinctions (must stay separate)

| Word | Meaning | Allowed without baseline? |
|---|---|---|
| **SLI** | Measured evidence with source, unit, owner, dimensions and unknown behavior | Yes — measure first |
| **SLO** | Internal operating target for one or more SLIs | Only after baseline evidence exists and an owner adopts it |
| **SLA** | External / contractual commitment to a customer or partner | Only with explicit business approval **and** baseline evidence |
| **Error budget** | Remaining allowance against an adopted SLO before policy actions | Only after an SLO is adopted |
| **Unknown** | Telemetry absent, stale, unparseable or out of contract window | Always non-green; never treat as success or zero |

Rules:

1. Saying “availability is 99.9%” without an adopted SLO/SLA and measured window is inventing a contract — forbidden here.
2. UI render time is never analytics freshness.
3. Disabled-by-policy workers are explicit, not healthy.
4. Partial / fallback / stale analytics states stay visible; they are not folded into green success.
5. Latency vocabulary remains OBS04 / PERF01; this document names the **service-level wrappers**, not percentile math.

## Service area vocabulary

Each area defines: **what the word means**, **measurement boundary**, **primary SLI IDs to cite**, **what is out of bounds**.

### 1. API availability

| Term | Definition |
|---|---|
| API availability (SLI sense) | Evidence that the process answers readiness/health and completes requests without collapsing missing probes into success |
| API availability (SLO sense) | Internal target on ready/health success and/or request error rate for named route families |
| API availability (SLA sense) | External commitment on availability for a named environment/product surface |

**Measurement boundary**

- Starts: inbound request accepted by the API process (or probe invocation for ready/health).
- Ends: response completed with status class recorded, or probe result recorded.
- Separate from: latency (OBS04), analytics freshness, import completion.

**Cite these SLIs first:** A1, A2, A3, A4 (latency A5 is companion evidence, not availability itself).

**Out of bounds**

- Inferring API “up” from a single cached dashboard tile.
- Treating warming_up / degraded ready as green.
- Using correlation ID as proof of availability.

### 2. Import service level

| Term | Definition |
|---|---|
| Import freshness (SLI) | Age of the latest durable successful import for a named source/scope |
| Import completion (SLI) | Whether an accepted job reached durable completed / failed / cancelled / partial with timestamps |
| Import SLO | Internal target on success age and/or accepted-to-complete duration for a named source |
| Import SLA | External commitment that a source will be ingested within an agreed window |

**Measurement boundary**

- Starts: accept/queue (`QueuedAtUtc` / equivalent).
- Ends: durable terminal status (completed success preferred for freshness SLIs).
- Not ended by: UI navigation, preview render, or partial validation without persistence.

**Cite these SLIs first:** I1–I6.

**Out of bounds**

- Counting a cancelled or partial job as successful freshness.
- Starting the clock at UI “Import” button click without accept record.
- Inventing source-level SLA hours without QDB/business approval.

### 3. Analytics freshness / refresh service level

| Term | Definition |
|---|---|
| Analytics freshness (SLI) | Backend-reported age/status of refresh or data window for a named job/scope |
| Analytics refresh success (SLI) | Whether a refresh attempt completed without treating timeout/partial as full success |
| Analytics freshness SLO | Internal target on last successful refresh age and/or refresh failure rate |
| Analytics freshness SLA | External commitment that decision surfaces reflect data within an agreed freshness policy |

**Measurement boundary**

- Starts: refresh requested/started on the authoritative job record.
- Ends: successful refresh timestamp or explicit failure/partial meta.
- UI must consume `refresh-status` / `AnalyticsResponseMetaDto` fields — **never** “page loaded at”.

**Cite these SLIs first:** R1–R7; cache companions C1–C3 when stale-served.

**Out of bounds**

- Equating empty dataset with refresh failure without meta/emptyReason.
- Hiding partial/fallback as healthy.
- Using frontend spinner duration as freshness SLA evidence.

### 4. Worker processing service level

| Term | Definition |
|---|---|
| Worker liveness (SLI) | Enabled state + heartbeat freshness for a named worker |
| Worker backlog (SLI) | Queue depth / oldest work age when instrumented |
| Worker processing SLO | Internal target on heartbeat age, backlog age, retry/DLQ rate |
| Worker processing SLA | External commitment that named background work completes within a policy window |

**Measurement boundary**

- Liveness: control plane enable flag + heartbeat timestamp.
- Processing: enqueue/oldest work → successful completion or explicit retry/DLQ.
- Disabled-by-policy must be labeled disabled, not “healthy silence”.

**Cite these SLIs first:** W1–W6.

**Out of bounds**

- Treating missing heartbeat inventory as zero failures.
- Collapsing DLQ into success counts.
- Promising worker SLAs for workers that still report backlog as unknown.

### 5. Report / export generation service level

| Term | Definition |
|---|---|
| Report generation success (SLI) | Whether a report/export run completed, failed, or completed with partial/export warnings |
| Report freshness (SLI) | Age of the last successful generation for a named report family |
| Report SLO | Internal target on success rate and/or generation age |
| Report SLA | External commitment on delivery of a named report within a window |

**Measurement boundary**

- Starts: generation accepted/started.
- Ends: durable success/failure with warning flags preserved.
- Partial export warnings remain visible evidence, not silent success.

**Cite these SLIs first:** report rows in OBS01 catalog / future report SLI IDs when instrumented; until then treat report evidence as **unknown** rather than inventing green.

**Out of bounds**

- Inferring report SLA from browser print dialog success.
- Dropping export warnings to force a green tile.

## How to speak in tickets and prompts

Use this pattern:

1. **Measured:** “SLI R2 last successful refresh age is unknown because `LastSuccessfulRefreshAtUtc` is null.”
2. **Operating intent (only if adopted):** “We are missing our internal SLO for refresh age on job X.”
3. **Contract (only if approved):** “This breaches the customer SLA for source Y freshness.”

Do not jump from (1) to (3). Do not invent the number that would make (2) or (3) true.

## Error budgets — when they may be discussed

Error budgets may be discussed **only when all** of the following are true:

1. An **SLO is explicitly adopted** (owner, window, SLI set, environment).
2. The SLI has **enough baseline samples** in that window (unknown windows do not create budget).
3. The discussion names the **policy action** if budget burns (alert, freeze feature, incident) — not just a percentage slogan.

Error budgets may **not** be discussed when:

- only SLIs exist and no SLO was adopted;
- telemetry for the window is unknown/partial;
- the topic is really latency cold-start noise without a warm-path contract (use OBS04 first);
- the goal is to invent a customer SLA percentage for sales collateral.

Until an SLO exists, say: “measured SLI only; no error budget yet.”

## Governance proof for future runtime prompts

Future OBS/QDB/RQ/STAB runtime prompts must cite this file when they:

- name availability, import SLA, analytics freshness SLA, worker SLA or report SLA;
- introduce dashboards or alerts that claim “meeting SLA”;
- add error-budget burn alerts.

Minimum checklist for those prompts:

1. Cite the service-area term from this vocabulary.
2. Cite the SLI IDs from `OBSERVABILITY_SLI_CATALOG.md`.
3. State whether the work is SLI-only, SLO adoption, or SLA (business-approved).
4. Keep unknown != green and no fake zero.
5. Do not add contractual numbers in code comments or UI copy without an approved SLA record.

## Non-goals

- no numeric SLO/SLA tables in this prompt
- no vendor selection
- no runtime instrumentation rewrite
- no shared-SaaS tenant dimension until MT authorizes it
- no replacing OBS04 latency percentile rules
