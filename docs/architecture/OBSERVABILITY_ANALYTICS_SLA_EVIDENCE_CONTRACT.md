# Analytics SLA Evidence Contract

Status: authoritative OBS07 contract
Date: 2026-08-12
Roadmap: `docs/roadmaps/OBSERVABILITY_ROADMAP.md` (OBS-5)
Related:

- `docs/architecture/OBSERVABILITY_SERVICE_LEVEL_VOCABULARY.md`
- `docs/architecture/OBSERVABILITY_SLI_CATALOG.md`
- `docs/architecture/OBSERVABILITY_IMPORT_SLA_EVIDENCE_CONTRACT.md`
- `docs/architecture/OBSERVABILITY_INSTRUMENTATION_ROLLOUT_PLAN.md`

## Purpose

Define a citeable analytics SLA evidence contract for Trendplus support and future OBS prompts.

This document is documentation only. It does **not**:

- invent numeric freshness hours or contractual SLA percentages;
- hide stale, partial or fallback states;
- treat UI render time as freshness;
- replace RQ ownership of analytics correctness;
- add runtime instrumentation or workflow code;
- infer tenant scope without MT authorization.

## Contract boundary

Analytics SLA evidence answers a narrow question:

> Is the authoritative analytics read model refreshed from known provenance, and how old is the latest successful refresh?

The boundary starts at authoritative refresh request/start and ends at durable refresh completion or explicit failure/partial meta.

It is not answered by:

- page load time;
- browser navigation time;
- a cache hit with no freshness provenance;
- an empty dataset with no meta;
- a partial refresh that is being hidden as green.

## Required evidence fields

Every analytics evidence record should be able to name:

| Field | Meaning |
|---|---|
| `jobId` | Durable refresh job or run identifier |
| `dataScope` | Authoritative analytics scope, such as global, report family or approved tenant scope |
| `requestedAtUtc` | When the refresh was requested |
| `startedAtUtc` | When the refresh actually started |
| `completedAtUtc` | When the refresh reached durable completion |
| `lastSuccessfulRefreshAtUtc` | Timestamp of the most recent durable successful refresh |
| `lastSuccessfulRefreshAgeSeconds` | Derived from `lastSuccessfulRefreshAtUtc`; null/unknown when no success exists |
| `sourceImportProvenance` | The source/import lineage used for the refresh |
| `summaryMaterializationAgeSeconds` | Age of the materialized summary when applicable |
| `partial` | Whether the refresh completed with explicit partial semantics |
| `fallback` | Whether the response relied on fallback data |
| `failedRefreshReasonCategory` | Normalized failure reason |
| `nextRetryAtUtc` | When a retry is expected, if known |
| `warningCodes` | Explicit warning or fallback reasons |
| `correlationId` | Flow identifier for diagnosis |
| `dataQualityStatus` | Honest quality state for the evidence record |

`dataScope` must stay explicit. Do not infer shared-tenant identity from this contract; MT remains the owner of shared-SaaS scope rules.

## State semantics

| State | Meaning | Freshness effect |
|---|---|---|
| `requested` | Refresh was requested but not yet started | unknown until terminal state |
| `started` | Refresh is in progress | unknown until terminal success |
| `completed` | Durable success recorded | resets the latest-success clock |
| `failed` | Refresh ended unsuccessfully | does not count as freshness success |
| `partial` | Some refresh work completed, but the record is explicitly incomplete | not full freshness; keep partial visible |
| `fallback` | A fallback path was used to answer the surface | not the same as fresh authoritative data |

Rules:

1. `partial` is not a full success.
2. `fallback` is not authoritative freshness evidence.
3. `failed` is evidence of attempt, not evidence of freshness.
4. Missing timestamps or missing history are `unknown`, not `0`.

## Measurement rules

1. Start the clock at the authoritative refresh request/start record.
2. Stop the clock only at durable terminal completion.
3. Do not use page render time, bundle load time or spinner duration as freshness evidence.
4. Keep source/import provenance visible when present.
5. If no durable success exists, report the latest-success age as unknown instead of fabricating zero age.

## How operations should speak about it

Use the contract in this order:

1. Measured evidence: `"latest refresh age is unknown because no completed refresh exists"`
2. Operational interpretation: `"the analytics read model is stale or incomplete"`
3. Only later, with business approval: `"the surface misses its freshness commitment"`

Do not jump from missing evidence to breach language.

## Minimum support answers

Support should be able to answer these from the contract:

- when the refresh was requested;
- when it started;
- when terminal completion happened;
- whether the latest run was failed, partial or fallback-based;
- how old the last durable success is;
- which source/import lineage was used;
- whether the current state is unknown, partial, fallback or successful.

## Validation rules

- partial and fallback states remain explicit;
- missing last-success stays unknown, not zero age;
- the contract does not invent customer SLA percentages;
- the contract does not treat UI load as refresh evidence;
- the contract does not remove RQ, QDB or MT ownership boundaries.

## Acceptance

- one citeable analytics SLA evidence contract exists;
- support can answer the OBS-5 questions from the contract language;
- runtime wiring remains a later promoted slice.
