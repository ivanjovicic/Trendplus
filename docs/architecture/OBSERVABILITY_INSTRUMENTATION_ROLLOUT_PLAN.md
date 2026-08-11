# Observability Instrumentation Rollout Plan

Status: authoritative OBS02 rollout plan
Date: 2026-08-11
Roadmap: `docs/roadmaps/OBSERVABILITY_ROADMAP.md`
Source contract: `docs/architecture/OBSERVABILITY_SLI_CATALOG.md`

## Purpose

Translate the OBS01 SLI/SLA/correlation catalog into bounded runtime instrumentation slices.

This plan keeps the rollout narrow:

- no vendor lock-in;
- no broad telemetry rewrite;
- no new trust semantics;
- no treating unknown telemetry as healthy;
- no mixing observability work with PERF benchmarking targets.

The goal is to make the future runtime prompts reviewable and small enough that each one can ship with its own evidence.

## Current evidence

Trendplus already has several observability anchors that future slices can build on:

- HTTP request/performance logging already exists;
- handled error logging can carry correlation IDs;
- analytics refresh status is already persisted and surfaced;
- worker health, worker policies and worker enable/disable states already exist;
- frontend analytics error and refresh banners already surface correlation-aware warnings;
- OBS01 already defined the authoritative SLI families and unknown/non-green rules.

The remaining gap is not "no telemetry at all". The gap is bounded instrumentation coverage that is still split across API, import, analytics, worker, dashboard and tracing concerns.

## Rollout order

Prioritize slices by pilot/support value and by how much they reduce blind spots for operations.

1. API health and latency visibility.
2. Import and connector lifecycle evidence.
3. Analytics freshness and refresh evidence.
4. Worker backlog and execution evidence.
5. Dashboard summary views that fail closed on unknown.
6. Correlation/tracing cleanup across process boundaries.

## Slice 1 - API and process evidence

### Goal

Make basic request, availability and latency evidence consistent enough that support can tell healthy, warming, degraded and unknown apart.

### Source of truth

- `GET /ready`
- `GET /health`
- `GET /health/dependencies`
- `GET /api/runtime/version`
- request performance logging

### Minimum fields

- route family;
- status class;
- cold/warm state;
- correlation ID when safe;
- dependency latency where available;
- unknown/non-green reason when a dependency or probe is missing.

### Validation

- missing probe data stays unknown, not green;
- error responses do not collapse into a fake zero;
- process and dependency health are visible separately.

## Slice 2 - Import and connector evidence

### Goal

Answer when a source import started, how long it took, whether it completed, and what the latest successful scope was.

### Source of truth

- import batch lifecycle timestamps;
- source connector session/read results;
- cursor and checkpoint history;
- import validation failures;
- source-specific connection-test results when available.

### Minimum fields

- accepted/started/completed timestamps;
- source system and source scope;
- failure or partial reason category;
- latest success age;
- correlation ID;
- row-count truth when the connector can prove it.

### Validation

- no successful import is inferred from a queue entry alone;
- incomplete jobs remain incomplete, not green;
- missing source evidence stays unknown;
- source credentials and row payloads are excluded.

## Slice 3 - Analytics freshness and refresh evidence

### Goal

Make analytics freshness, stale fallback and refresh failure visible without relying on page render time.

### Source of truth

- analytics refresh status endpoints;
- analytics response meta;
- refresh run history;
- freshness/fallback warnings in UI banners;
- cached vs live provenance.

### Minimum fields

- last successful refresh age;
- refresh duration;
- refresh failure category;
- partial/fallback state;
- data-scope;
- correlation ID;
- freshness age from authoritative timestamps only.

### Validation

- page render time is never used as freshness evidence;
- stale or partial responses stay visibly non-green;
- cache hits do not erase freshness provenance;
- unknown metadata cannot masquerade as success.

## Slice 4 - Worker execution and backlog evidence

### Goal

Let support see whether workers are enabled, healthy, backed up or silently failing.

### Source of truth

- worker registry and runtime policy;
- worker health service;
- worker enable/disable state;
- queue depth and oldest work age where the worker owns a queue;
- retry/dead-letter evidence where available;
- worker iteration logs and last error state.

### Minimum fields

- worker name;
- enabled/disabled/paused state;
- last successful run or heartbeat age;
- last error category;
- backlog or queue age;
- correlation ID where the worker processes a traced flow.

### Validation

- disabled-by-policy is explicit, not healthy;
- missing backlog metrics remain unknown until instrumented;
- worker failure does not disappear into a generic success state;
- correlation IDs are diagnosis-only.

## Slice 5 - Dashboards and alert surfaces

### Goal

Summarize the above evidence for operators without hiding gaps behind green defaults.

### Source of truth

- the OBS01 SLI catalog;
- the API/import/analytics/worker evidence slices above;
- health/readiness endpoints;
- warning states already shown in analytics UI.

### Minimum fields

- business readiness layer;
- API latency/error layer;
- import/connectors layer;
- workers/runtime layer;
- database/cache layer;
- decision/action/outcome layer only after those surfaces exist.

### Validation

- missing panel data is WARN or UNKNOWN;
- dashboards do not invent healthy metrics;
- dashboard summaries retain stale/partial/blocked states;
- tenant-specific dimensions stay blocked until MT authorizes them.

## Slice 6 - Correlation and tracing cleanup

### Goal

Reduce ambiguity around flow correlation so support can trace a request through API, import, worker and refresh steps.

### Source of truth

- HTTP correlation header;
- request logs/problem details where safe;
- import batch/job metadata;
- worker/outbox processing;
- analytics refresh/report/export runs;
- future decision timeline/outcome events.

### Minimum fields

- one flow identifier per hop;
- safe propagation across process boundaries;
- explicit separation between correlation, tenant identity and authorization;
- documentation alignment where `TraceId` and correlation naming diverge today.

### Validation

- correlation is never used as auth;
- correlation is never used as tenant identity;
- sensitive payloads are not attached to traces;
- the end-to-end chain is evidence, not assumption.

## Privacy and safety checks

Every rollout slice must keep the following rules:

- unknown telemetry is not green;
- secrets, raw connection strings and customer payloads stay out of logs and traces;
- tenant labels are not trusted shared-SaaS scope until MT authorizes them;
- operational evidence and business analytics remain separate concerns;
- runtime changes must stay within the slice they were approved for.

## Future prompt contract

The next runtime observability prompts should be created as small, bounded slices with one clear owner:

- API/process probes and latency;
- import lifecycle evidence;
- analytics freshness and refresh lifecycle;
- worker backlog and failure evidence;
- dashboard summaries;
- correlation/tracing cleanup.

Each future prompt should be able to point back to this rollout plan and the OBS01 catalog without re-explaining the entire observability model.

## Acceptance

- the OBS01 catalog has a bounded rollout plan;
- the rollout order is explicit and support-focused;
- each slice has a source of truth, minimum fields and validation rules;
- no vendor choice or runtime instrumentation was introduced by this document;
- future OBS runtime prompts can be created without duplicating STAB or PERF ownership.
