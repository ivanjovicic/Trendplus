# Trendplus Observability Roadmap

Updated: 2026-08-18
Status: roadmap only; instrumentation implementation is queue-gated  
Owner queue: `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md` (`OBS`)

## Goal

Make Trendplus operable from evidence. A customer-facing warning, an import delay, a slow analytics response, a failed worker or a stale dashboard should be diagnosable without guessing or exposing sensitive customer data.

## Principles

- business health and technical health are different but connected;
- SLIs are measured facts; SLA/SLO targets are explicit product/operating decisions;
- unknown telemetry is not green;
- correlation should connect request, import, worker, analytics refresh, report and later decision/outcome flows;
- logs must not contain secrets, raw connection strings or unnecessary customer payloads;
- dashboards summarize evidence but do not replace source events/traces.

## Roadmap

### OBS-1 - Business and technical metric catalog

Define authoritative metrics and dimensions for:

- active data source/import status;
- freshness and data-quality state;
- actionable recommendation counts by trust state;
- action/outcome lifecycle coverage;
- failed/blocked workflow counts;
- customer/tenant scope only where server-authoritative;
- request rate/error rate/latency;
- database/query health;
- worker queue depth/failure/retry;
- cache hit/miss/invalidation;
- report/export success/failure.

**Status:** OBS01 catalog complete — `docs/architecture/OBSERVABILITY_SLI_CATALOG.md`.
OBS02 instrumentation plan complete and OBS04 latency contract complete. OBS05 service-level vocabulary complete — `docs/architecture/OBSERVABILITY_SERVICE_LEVEL_VOCABULARY.md`. OBS06 import SLA evidence contract complete. OBS07 analytics SLA evidence contract is complete. OBS08 worker SLA evidence contract complete — `docs/architecture/OBSERVABILITY_WORKER_SLA_EVIDENCE_CONTRACT.md`. OBS09 capture complete — `docs/qa/OBSERVABILITY_WORKER_SLA_EVIDENCE_CAPTURE_2026-08-17.md`. Current queue READY: `OBS10` operational dashboard honesty contract.

### OBS-2 - Latency SLIs

Define p50/p95/p99 where useful for:

- API route families;
- analytics aggregations;
- connector/import phases;
- worker processing;
- report generation;
- frontend route/data readiness.

Separate cold-start and warm-path latency.

### OBS-3 - Service level vocabulary

**Status:** DONE (`OBS05`) — `docs/architecture/OBSERVABILITY_SERVICE_LEVEL_VOCABULARY.md`.

Create explicit definitions for:

- API availability SLI/SLO;
- import SLA/SLO;
- analytics freshness/refresh SLA/SLO;
- worker processing SLA/SLO;
- report generation SLA/SLO;
- incident/error-budget treatment when targets are adopted.

Do not publish numerical SLA commitments until the business/customer contract requires them and baseline data exists.

### OBS-4 - Import SLA evidence

**Status:** DONE (`OBS06`) — `docs/architecture/OBSERVABILITY_IMPORT_SLA_EVIDENCE_CONTRACT.md`.

Track import lifecycle timestamps and states so operations can answer:

- when the job was accepted;
- when source reading started/completed;
- whether validation/persistence completed;
- whether it failed/cancelled/partially completed;
- how old the last successful import is;
- which customer/source scope the evidence belongs to.

### OBS-5 - Analytics SLA evidence

**Status:** DONE (`OBS07`) — `docs/architecture/OBSERVABILITY_ANALYTICS_SLA_EVIDENCE_CONTRACT.md`.

Track:

- refresh requested/started/completed;
- source/import provenance used;
- summary/materialization age;
- partial/fallback state;
- failed refresh reason category;
- next retry/backoff where applicable.

The UI must never infer freshness from page render time.

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
  - runtime wiring for analytics SLA evidence remains a later promoted slice
- Next:
  - OBS-6 worker SLA evidence when promoted

### OBS-6 - Worker SLA evidence

**Status:** DONE (`OBS08`) — `docs/architecture/OBSERVABILITY_WORKER_SLA_EVIDENCE_CONTRACT.md`. Runtime capture is DONE (`OBS09`) — `docs/qa/OBSERVABILITY_WORKER_SLA_EVIDENCE_CAPTURE_2026-08-17.md`.

For each important worker:

- queue/backlog size where applicable;
- oldest work age;
- run duration;
- success/failure/retry/dead-letter counts;
- last successful run;
- current disabled/paused state;
- safe correlation to source job/batch.

### OBS-7 - Operational dashboards

Create layered dashboards only after metric ownership is defined:

1. customer/business readiness;
2. API/analytics latency/error;
3. import/connectors;
4. workers/outbox;
5. database/cache;
6. later decision/action/outcome lifecycle.

A dashboard must preserve WARN/BLOCKED/unknown states rather than defaulting to green when telemetry is absent.

**Planning:** dashboard/alert slice order is defined in `docs/architecture/OBSERVABILITY_INSTRUMENTATION_ROLLOUT_PLAN.md` (O2-6).

### OBS-8 - Tracing

Introduce distributed/request tracing where it gives diagnostic value:

- inbound API request;
- backend service/DB spans;
- import job boundaries;
- worker processing;
- external connector calls;
- future AI/provider calls only after GAI gates.

Tracing must sample safely and avoid attaching sensitive row payloads.

**Planning:** tracing is slice O2-7 and must follow correlation unification (O2-4).

### OBS-9 - Correlation IDs

Standardize correlation identifiers across:

- HTTP request/response;
- logs/problem details where safe;
- import batch/job;
- worker/outbox message;
- analytics refresh;
- report/export;
- later Decision Graph / Timeline / outcome evidence.

Correlation IDs identify a flow; they are not authorization or tenant identity.

## Initial SLI families

| Family | Example evidence | Primary owner |
|---|---|---|
| API | request count, error rate, p95 latency | OBS/PERF |
| Import | accepted-to-complete latency, latest success age, failure rate | OBS/QDB |
| Analytics | refresh duration, freshness age, failed refresh rate | OBS/RQ |
| Worker | queue age, duration, retry/dead-letter rate | OBS/STAB |
| Decision | explainability coverage, outcome measurement coverage | OBS/DEX/RL/DT |
| Tenant | cross-tenant negative-test/incident evidence | MT/SEC |

## Dependencies

- existing health/readiness and Serilog behavior;
- STAB security/privacy boundaries;
- QDB for source/import dimensions;
- MT before tenant dimensions are used in shared SaaS;
- PERF for benchmark budgets;
- DEX/DT/RL for future decision lifecycle metrics.

## Non-goals

This roadmap does not choose a paid observability vendor, promise contractual SLA numbers without evidence, log sensitive payloads, or duplicate business analytics as operational telemetry.
