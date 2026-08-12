# Analytics SLA Evidence Contract

Status: authoritative OBS07 contract
Date: 2026-08-12
Roadmap: `docs/roadmaps/OBSERVABILITY_ROADMAP.md` (OBS-5)
Related:

- `docs/architecture/OBSERVABILITY_SERVICE_LEVEL_VOCABULARY.md`
- `docs/architecture/OBSERVABILITY_SLI_CATALOG.md`
- `docs/architecture/OBSERVABILITY_IMPORT_SLA_EVIDENCE_CONTRACT.md`
- `docs/roadmaps/OBSERVABILITY_ROADMAP.md`

## Purpose

Define a citeable analytics SLA evidence contract for Trendplus support and future OBS prompts.

This document is documentation only. It does **not**:

- invent numeric SLA hours;
- turn partial/fallback states into freshness success;
- replace RQ ownership of refresh behavior or QDB ownership of source/import behavior;
- add runtime instrumentation or workflow code;
- treat UI render time as analytics freshness;
- claim tenant-scoped shared-SaaS behavior without MT gates.

## Contract boundary

Analytics SLA evidence answers a narrow question:

> Did a named analytics scope reach a durable successful refresh/materialization, and how old is the latest success?

The boundary starts at the authoritative refresh request/start record and ends at a durable terminal status or materialized success record.

It is not answered by:

- clicking a refresh button in the UI;
- rendering a page or dashboard;
- a transient in-memory state;
- a partial refresh without durable persistence;
- a fallback path that hides the original failure;
- browser spinner time.

## Required evidence fields

Every analytics evidence record should be able to name:

| Field | Meaning |
|---|---|
| `requestedScope` | Requested analytics scope or route family, with filters/dimensions kept explicit |
| `effectiveScope` | Scope actually used after any allowed fallback or normalization |
| `requestedAtUtc` | When the refresh or evidence request was accepted |
| `startedAtUtc` | When the authoritative refresh/materialization started |
| `completedAtUtc` | When the run reached durable terminal completion |
| `terminalStatus` | `completed`, `failed`, `cancelled`, `partial`, or `fallback` |
| `failedReasonCategory` | Stable reason category for terminal failure or timeout |
| `partialReasonCodes` | Explicit codes when some sections or inputs succeeded but the evidence is incomplete |
| `fallbackReasonCodes` | Explicit codes when the result used a fallback source, cache, or effective scope |
| `nextRetryAtUtc` | When the next retry is planned, if applicable |
| `retryBackoffSeconds` | Backoff window for the next retry, if applicable |
| `sourceSystem` | Human-readable upstream source or connector family |
| `sourceScope` | Authoritative source scope that fed the analytics evidence |
| `upstreamImportBatchId` | Import batch or job that supplied the refreshed scope, when relevant |
| `upstreamImportCompletedAtUtc` | Durable success time of the upstream import that fed analytics |
| `summaryMaterializedAtUtc` | When the analytics summary/materialized view was last durably produced |
| `lastSuccessfulRefreshAtUtc` | Timestamp of the most recent durable successful refresh/materialization |
| `lastSuccessfulRefreshAgeSeconds` | Derived from `lastSuccessfulRefreshAtUtc`; null/unknown when no success exists |
| `materializationAgeSeconds` | Age of the summary/materialized read model when that is the evidence source |
| `correlationId` | Flow identifier for diagnosis |
| `warningCodes` | Explicit warning or fallback reasons that must remain visible |
| `dataQualityStatus` | Honest quality state for the evidence record |

`sourceScope` must stay explicit. Do not infer shared-tenant identity from this contract; MT remains the owner of shared-SaaS scope rules.

## State semantics

| State | Meaning | Freshness effect |
|---|---|---|
| `requested` | A refresh/evidence request exists, but durable completion is not proven yet | unknown until terminal success |
| `started` | Refresh/materialization work is in progress | unknown until terminal success |
| `completed` | Durable success recorded | resets the latest-success clock |
| `failed` | Attempt ended unsuccessfully | does not count as freshness success |
| `cancelled` | Attempt stopped before durable success | does not count as freshness success |
| `partial` | Some work completed, but the record is explicitly incomplete | not full freshness; keep partial visible |
| `fallback` | Evidence was served from a fallback source, scope, or stale path | not full freshness; requested and effective scope both stay visible |

Rules:

1. `fallback` is not a fresh success.
2. `partial` is not a full success.
3. `failed` is evidence of attempt, not evidence of freshness.
4. Missing timestamps or missing history are `unknown`, not `0`.

## Measurement rules

1. Start the clock at `requestedAtUtc` or `startedAtUtc`, whichever is authoritative for the source.
2. Stop the clock only at durable terminal completion.
3. Keep previous successful evidence visible when a later attempt fails, cancels, ends partial, or falls back.
4. Do not use page render time, browser load, or spinner time as evidence.
5. If no durable success exists, report the latest-success age as unknown instead of fabricating zero age.
6. When the summary/materialization is the evidence source, keep its age distinct from upstream import age.

## How operations should speak about it

Use the contract in this order:

1. Measured evidence: "latest success is unknown because no completed analytics refresh exists"
2. Operational interpretation: "the analytics scope has a freshness gap"
3. Only later, with business approval, SLA language: "this misses the freshness commitment"

Do not jump from missing evidence to breach language.

## Minimum support answers

Support should be able to answer these from the contract:

- when the analytics scope was requested;
- when refresh/materialization started;
- when terminal completion happened;
- whether the latest attempt failed, cancelled, partially completed or fell back;
- how old the last durable success is;
- which source/import batch fed the analytics scope;
- whether the evidence came from a summary/materialized read model or a live refresh;
- what retry/backoff is scheduled next, if any;
- which scope was requested versus which effective scope was used.

## Validation rules

- cancelled, partial and fallback states remain explicit;
- missing last-success stays unknown, not zero age;
- the contract does not invent customer SLA hours;
- the contract does not treat UI load as refresh completion;
- the contract does not remove QDB or MT ownership boundaries;
- the contract does not hide stale/cache-served provenance when it is the evidence source.

## Acceptance

- one citeable analytics SLA evidence contract exists;
- support can answer the OBS-5 questions from the contract language;
- runtime wiring remains a later promoted slice.
