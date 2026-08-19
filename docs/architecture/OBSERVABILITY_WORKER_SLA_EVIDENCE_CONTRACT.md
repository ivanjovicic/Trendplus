# Worker SLA Evidence Contract

Status: authoritative OBS08 contract
Date: 2026-08-17
Roadmap: `docs/roadmaps/OBSERVABILITY_ROADMAP.md` (OBS-6)
Related:

- `docs/architecture/OBSERVABILITY_SERVICE_LEVEL_VOCABULARY.md`
- `docs/architecture/OBSERVABILITY_SLI_CATALOG.md`
- `docs/architecture/OBSERVABILITY_IMPORT_SLA_EVIDENCE_CONTRACT.md`
- `docs/architecture/OBSERVABILITY_ANALYTICS_SLA_EVIDENCE_CONTRACT.md`
- `docs/roadmaps/OBSERVABILITY_ROADMAP.md`

## Purpose

Define a citeable worker SLA evidence contract for Trendplus support and future OBS prompts.

This document is documentation only. It does **not**:

- invent numeric SLA hours or retry budgets;
- treat missing queue depth, missing last-success, or missing heartbeat as healthy zeros;
- treat disabled/paused workers as successful processing;
- collapse retry or dead-letter counts into success counts;
- replace STAB ownership of worker runtime control;
- add runtime instrumentation, alerting rules, or workflow code;
- claim tenant-scoped shared-SaaS behavior without MT gates.

## Contract boundary

Worker SLA evidence answers a narrow question:

> For a named worker, is required background work enabled, progressing, and completing without silent backlog growth — and how old is the latest durable success?

The boundary starts at enqueue/oldest work (or the control-plane enable/pause decision when no queue exists) and ends at a durable successful completion, an explicit retry, or an explicit dead-letter.

It is not answered by:

- a process being alive without a recent heartbeat;
- an empty last-error field while the heartbeat is stale;
- UI health tiles that default to green when inventory is missing;
- collapsing “no jobs observed” into “queue size 0” when depth is not instrumented;
- treating a paused or policy-disabled worker as healthy silence.

Cite SLI IDs **W1–W6** from `docs/architecture/OBSERVABILITY_SLI_CATALOG.md`. This contract is the OBS-6 honesty layer over those rows.

## Required evidence fields

Every worker evidence record should be able to name:

| Field | Meaning | SLI |
|---|---|---|
| `workerName` | Stable worker identity | W1–W6 |
| `workersGloballyEnabled` | Control-plane global workers switch | W1 |
| `executionState` | `enabled`, `paused`, `disabled`, or `unknown` | W1, W3 |
| `pauseReason` | Why execution is paused or disabled, when known | W3 |
| `lastHeartbeatAtUtc` | Last observed heartbeat | W2 |
| `heartbeatAgeSeconds` | Derived from `lastHeartbeatAtUtc`; null/unknown when no heartbeat exists | W2 |
| `queueDepth` | Current backlog size when the worker has a queue; unknown when not instrumented | W5 |
| `oldestWorkAgeSeconds` | Age of the oldest queued/claimed work; unknown when not instrumented | W5 |
| `runDurationSeconds` | Duration of the current or last observed run | — |
| `successCount` | Durable successful completions in the evidence window | W6 |
| `failureCount` | Durable failed attempts in the evidence window | W4, W6 |
| `retryCount` | Explicit retry attempts in the evidence window | W6 |
| `deadLetterCount` | Explicit dead-letter / terminal poison counts | W6 |
| `lastSuccessfulRunAtUtc` | Timestamp of the most recent durable successful run | W2 |
| `lastSuccessfulRunAgeSeconds` | Derived from `lastSuccessfulRunAtUtc`; null/unknown when no success exists | W2 |
| `lastErrorPresent` | Whether a last-error record exists | W4 |
| `sourceJobId` | Source job/batch identifier when correlation is safe | OBS-9 |
| `sourceSystem` | Human-readable upstream job family when known | — |
| `correlationId` | Flow identifier for diagnosis | OBS-9 |
| `warningCodes` | Explicit warning or unknown-instrumentation codes | — |
| `dataQualityStatus` | Honest quality state for the evidence record | — |

`sourceJobId` and `correlationId` identify a flow. They are not authorization, tenant identity, or a substitute for STAB runtime-policy truth. Do not attach secrets, raw connection strings, or row payloads.

When a worker has no queue (heartbeat-only processors), `queueDepth` and `oldestWorkAgeSeconds` stay **unknown** unless a later instrumented source exists. Do not invent `0`.

## State semantics

| State | Meaning | Processing effect |
|---|---|---|
| `unknown` | Control plane, heartbeat inventory, or queue instrumentation is missing | non-green; not healthy |
| `enabled` | Worker is allowed to run | not success by itself; still needs heartbeat and completion evidence |
| `paused` | Runtime policy or global switch is holding execution | explicit paused; not healthy silence |
| `disabled` | Policy disabled this worker | explicit disabled; not healthy |
| `running` | A run is in progress | unknown until durable success, retry, or dead-letter |
| `idle` | Enabled, no current run observed | healthy only if heartbeat is fresh **and** queue evidence is known empty or inapplicable |
| `succeeded` | Durable successful completion recorded | resets the latest-success clock |
| `failed` | Attempt ended unsuccessfully | does not count as processing success |
| `retrying` | Explicit retry scheduled or in flight | not success; keep retry count visible |
| `dead_lettered` | Work was explicitly parked as poison/dead-letter | not success; keep DLQ count visible |

Rules:

1. Missing queue depth is `unknown`, not `0`.
2. Missing last-success is `unknown`, not `0` age and not “never failed”.
3. Missing heartbeat inventory is `unknown`/`stale`, not a green liveness tile.
4. `paused` and `disabled` are explicit operational states, not healthy silence.
5. Empty `lastErrorPresent` is not healthy if the heartbeat is stale or missing.
6. Retry and dead-letter counts must stay separate from `successCount`.
7. Numeric SLA hours, retry budgets, or DLQ thresholds require explicit product or operations approval.

## Measurement rules

1. Start the processing clock at enqueue time or oldest-work timestamp when a queue exists.
2. For heartbeat-only workers, start at the last enable/unpause decision plus heartbeat freshness; do not invent a queue of size zero.
3. Stop the processing clock only at durable success, explicit retry, or explicit dead-letter.
4. Keep previous successful evidence visible when a later attempt fails, retries, dead-letters, pauses, or is disabled.
5. If no durable success exists, report `lastSuccessfulRunAgeSeconds` as unknown instead of fabricating zero age.
6. If queue instrumentation does not exist, report `queueDepth` and `oldestWorkAgeSeconds` as unknown instead of fabricating empty backlog.
7. Correlate to a source job/batch only when the identifier is already durable and non-sensitive.

## How operations should speak about it

Use the contract in this order:

1. Measured evidence: `"last successful run is unknown because no completed worker run exists"`
2. Operational interpretation: `"the worker has a processing or liveness gap"`
3. Only later, with product or operations approval, SLA language: `"this misses the worker processing commitment"`

Do not jump from missing evidence to breach language.

Examples that stay honest:

- `"queue depth is unknown; W5 is not instrumented"` — not `"backlog is 0"`
- `"worker is paused; execution is not healthy silence"` — not `"no errors, therefore healthy"`
- `"retryCount=3, deadLetterCount=1, successCount=0"` — not `"jobs processed"`

## Minimum support answers

Support and operations should be able to answer these from the contract:

- whether workers are globally enabled;
- whether a named worker is enabled, paused, disabled, or unknown;
- how old the last heartbeat is;
- queue/backlog size and oldest work age, or that those fields are unknown;
- duration of the current or last observed run;
- success, failure, retry and dead-letter counts without collapsing them;
- how old the last durable successful run is;
- which source job/batch the work correlates to, when safe;
- whether missing evidence is being shown as unknown rather than green.

## Validation rules

- missing worker evidence is never treated as healthy;
- missing last-success stays unknown, not zero age;
- missing queue depth stays unknown, not zero backlog;
- paused/disabled stays explicit and non-green;
- retry/dead-letter counts stay distinct from success;
- the contract does not invent customer SLA hours;
- the contract does not remove STAB or MT ownership boundaries;
- the contract does not authorize runtime alerting in this slice.

## Acceptance

- one citeable worker SLA evidence contract exists;
- support and operations can answer the OBS-6 questions from the contract language;
- runtime wiring remains a later promoted slice (`OBS09`).
