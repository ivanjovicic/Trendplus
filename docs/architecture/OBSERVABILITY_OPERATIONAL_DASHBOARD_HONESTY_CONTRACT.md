# Operational Dashboard Honesty Contract

Status: authoritative OBS10 contract
Date: 2026-08-19
Roadmap: `docs/roadmaps/OBSERVABILITY_ROADMAP.md` (OBS-7)
Related:

- `docs/architecture/OBSERVABILITY_SLI_CATALOG.md`
- `docs/architecture/OBSERVABILITY_SERVICE_LEVEL_VOCABULARY.md`
- `docs/architecture/OBSERVABILITY_IMPORT_SLA_EVIDENCE_CONTRACT.md`
- `docs/architecture/OBSERVABILITY_ANALYTICS_SLA_EVIDENCE_CONTRACT.md`
- `docs/architecture/OBSERVABILITY_WORKER_SLA_EVIDENCE_CONTRACT.md`
- `docs/architecture/PERFORMANCE_BASELINE_CONTRACT.md`
- `docs/qa/ANALYTICS_PRODUCTION_READINESS_STATUS.md`
- `docs/qa/ANALYTICS_PILOT_RELEASE_CHECKLIST_V2.md`
- `docs/qa/OBSERVABILITY_WORKER_SLA_EVIDENCE_CAPTURE_2026-08-17.md`

## Purpose

Trendplus operational dashboards must summarize evidence without inventing health, freshness or readiness.

This contract freezes the honesty rules for dashboard layers so later UI slices can render operational truth without turning missing evidence into green panels, `0 seconds`, or false readiness.

This document is intentionally narrow:

- it maps each dashboard layer to the existing OBS evidence contract that owns it;
- it keeps business readiness separate from technical health;
- it keeps unknown, partial, fallback and blocked states visible;
- it does not add a new metric catalog, vendor, alerting policy or runtime UI.

## Non-goals

- no new metric catalog
- no vendor choice
- no alerting policy
- no runtime dashboard implementation
- no changing existing backend evidence contracts
- no decision/action/outcome dashboard layer yet

## Contract boundary

An operational dashboard layer may summarize already-authored evidence, but it must not become the source of truth for that evidence.

The dashboard boundary is:

- read evidence from the owning contract or readiness artifact;
- render the evidence state honestly;
- keep missing or uninstrumented values explicit;
- refuse to infer health from unrelated layers.

The dashboard boundary is not:

- a replacement for import, analytics, worker or readiness contracts;
- a place to infer business readiness from technical uptime;
- a place to hide missing evidence behind a green summary;
- a place to turn `unknown` into `0`.

## Dashboard layer contract

| Layer | Source of truth | What may be shown | Unknown / WARN rule | Must not do |
|---|---|---|---|---|
| Business readiness | `docs/qa/ANALYTICS_PRODUCTION_READINESS_STATUS.md`, `docs/qa/ANALYTICS_PILOT_RELEASE_CHECKLIST_V2.md`, `docs/qa/ANALYTICS_PILOT_SMOKE_RESULT.md`, `docs/qa/ANALYTICS_REGRESSION_RISK_AUDIT.md` | readiness status, explicit blockers, evidence links, release notes | If readiness evidence is missing, stale or incomplete, show `unknown` or `warn`; do not infer green from other layers. | Do not label product readiness from worker, API or cache health alone. |
| API / analytics | `docs/architecture/OBSERVABILITY_SLI_CATALOG.md`, `docs/architecture/OBSERVABILITY_SERVICE_LEVEL_VOCABULARY.md`, `docs/architecture/OBSERVABILITY_ANALYTICS_SLA_EVIDENCE_CONTRACT.md` | request/error health, freshness age, partial/fallback state, last durable success age | Missing freshness or partial evidence stays non-green. If the authoritative timestamp is absent, last-success age is `unknown`, not `0 seconds`. | Do not use page render time as freshness evidence. |
| Import / connectors | `docs/architecture/OBSERVABILITY_SLI_CATALOG.md`, `docs/architecture/OBSERVABILITY_IMPORT_SLA_EVIDENCE_CONTRACT.md` | last successful import age, partial/cancelled status, source scope, import warning codes | If no durable success exists, report `unknown`. If a partial/cancelled result exists, keep it visible as non-green. | Do not collapse partial or cancelled import evidence into success. |
| Workers / runtime | `docs/architecture/OBSERVABILITY_SLI_CATALOG.md`, `docs/architecture/OBSERVABILITY_WORKER_SLA_EVIDENCE_CONTRACT.md`, `docs/qa/OBSERVABILITY_WORKER_SLA_EVIDENCE_CAPTURE_2026-08-17.md` | enabled/paused/disabled state, heartbeat age, backlog age, last successful run age, retry/DLQ counts | Missing queue depth, missing last-success or stale heartbeat stays `unknown`, not `0` and not healthy silence. | Do not treat disabled-by-policy as healthy. |
| Database / cache | `docs/architecture/OBSERVABILITY_SLI_CATALOG.md`, `docs/architecture/PERFORMANCE_BASELINE_CONTRACT.md` | dependency DB ok, cache hit/miss/invalidation evidence, prewarm or dependency notes where they already exist | Missing DB/cache evidence stays `unknown`; do not invent cache health or baseline success. | Do not infer cache success from unrelated API success. |

Decision/action/outcome evidence is a later layer. It must stay out of this contract until the DT/RL surfaces that own it are present and explicit.

## Visible status vocabulary

Dashboards using this contract may show only these status words:

- `healthy`
- `warn`
- `blocked`
- `unknown`

Rules:

1. `healthy` is allowed only when the owning evidence contract has a real success state and a valid timestamp or freshness measure.
2. `warn` is allowed when evidence exists but is partial, stale, fallback, incomplete or otherwise non-green.
3. `blocked` is allowed when the layer cannot be populated because the required upstream evidence is intentionally unavailable or gated by a higher-priority owner decision.
4. `unknown` is required when the dashboard lacks authoritative evidence or the source is uninstrumented.

## Layer rules

### Business readiness

- use only readiness artifacts that are already authoritative for the release or pilot state;
- do not convert API uptime or worker liveness into business readiness;
- if the readiness artifact cannot prove success, keep the panel `warn` or `unknown`.

### API / analytics

- freshness is only authoritative when it comes from the analytics freshness contract;
- partial or fallback responses remain visible;
- stale data is not healthy just because the page loaded.

### Import / connectors

- the newest success age comes from the import SLA evidence contract;
- partial or cancelled import evidence remains visible;
- no successful import ever is `unknown`, not `0 seconds`.

### Workers / runtime

- worker health is explicit only when the worker SLA contract names it;
- paused or disabled workers are not healthy silence;
- missing backlog depth or last-success age stays `unknown`.

### Database / cache

- use only the DB/dependency and cache evidence already defined in the catalog or baseline contract;
- if cache hit/miss or invalidation evidence does not exist, the panel stays `unknown`;
- do not infer cache trust from import or analytics success.

## Ignore-safely rules

1. A missing panel must never be rendered as green.
2. A zero value is only allowed when the source contract explicitly supports a true zero.
3. `unknown` is not a failure, but it is not healthy.
4. `warn` is visible evidence, not a hidden background state.
5. `blocked` should identify the owner or dependency that must resolve the gap.
6. The dashboard must not invent data quality or freshness from the time the page was opened.

## Minimum support answers

Operators should be able to tell from the dashboard:

- which layers have authoritative evidence;
- which layers are still unknown;
- whether a panel is warning-like because it is partial, stale or fallback;
- which contract owns the underlying evidence;
- whether a zero shown on the panel is a real zero or an absent-evidence placeholder.

## Acceptance

- one citeable operational dashboard honesty contract exists;
- operators can tell which layers are defined versus still unknown;
- missing panel data stays unknown or warn, never green;
- last-success age stays unknown when the authoritative timestamp is missing;
- the contract does not add a runtime dashboard implementation.
