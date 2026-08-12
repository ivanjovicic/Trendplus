# Import SLA Evidence Contract

Status: authoritative OBS06 contract
Date: 2026-08-12
Roadmap: `docs/roadmaps/OBSERVABILITY_ROADMAP.md` (OBS-4)
Related:

- `docs/architecture/OBSERVABILITY_SERVICE_LEVEL_VOCABULARY.md`
- `docs/architecture/OBSERVABILITY_SLI_CATALOG.md`
- `docs/architecture/OBSERVABILITY_INSTRUMENTATION_ROLLOUT_PLAN.md`

## Purpose

Define a citeable import SLA evidence contract for Trendplus support and future OBS prompts.

This document is documentation only. It does **not**:

- invent numeric SLA hours;
- turn partial/cancelled jobs into freshness success;
- replace QDB ownership of connector behavior;
- add runtime instrumentation or workflow code;
- treat UI render time as import evidence;
- claim tenant-scoped shared-SaaS behavior without MT gates.

## Contract boundary

Import SLA evidence answers a narrow question:

> Did a named source or scope reach a durable successful import, and how old is the latest success?

The boundary starts at accepted/queued ingest and ends at a durable terminal batch status.

It is not answered by:

- clicking an import button;
- rendering a page;
- a transient in-memory state;
- a partial parse without persistence;
- a cancelled job that never completed successfully.

## Required evidence fields

Every import evidence record should be able to name:

| Field | Meaning |
|---|---|
| `sourceSystem` | Human-readable source name or connector family |
| `sourceScope` | Authoritative scope for the evidence, such as source, deployment or tenant scope when allowed |
| `batchId` | Durable batch/job identifier |
| `acceptedAtUtc` | When the job was accepted or queued |
| `startedAtUtc` | When source reading began |
| `completedAtUtc` | When the batch reached durable terminal completion |
| `terminalStatus` | `completed`, `failed`, `cancelled`, or `partial` |
| `lastSuccessfulImportAtUtc` | Timestamp of the most recent durable completed success |
| `lastSuccessfulImportAgeSeconds` | Derived from `lastSuccessfulImportAtUtc`; null/unknown when no success exists |
| `warningCodes` | Explicit warning or fallback reasons |
| `correlationId` | Flow identifier for diagnosis |
| `dataQualityStatus` | Honest quality state for the evidence record |

`sourceScope` must stay explicit. Do not infer shared-tenant identity from this contract; MT remains the owner of shared-SaaS scope rules.

## State semantics

| State | Meaning | Freshness effect |
|---|---|---|
| `accepted` / `queued` | Job has entered the system, but success is not proven yet | unknown until terminal success |
| `started` / `running` | Source reading is in progress | unknown until terminal success |
| `completed` | Durable success recorded | resets the latest-success clock |
| `failed` | Attempt ended unsuccessfully | does not count as freshness success |
| `cancelled` | Job was stopped before durable success | does not count as freshness success |
| `partial` | Some work completed, but the record is explicitly incomplete | not full freshness; keep partial visible |

Rules:

1. `cancelled` is not a fresh success.
2. `partial` is not a full success.
3. `failed` is evidence of attempt, not evidence of freshness.
4. Missing timestamps or missing history are `unknown`, not `0`.

## Measurement rules

1. Start the clock at `acceptedAtUtc` or `queuedAtUtc`, whichever is authoritative for the source.
2. Stop the clock only at durable terminal completion.
3. Do not use page render time or preview time as evidence.
4. Keep previous successful evidence visible when a later attempt fails, cancels or ends partial.
5. If no durable success exists, report the latest-success age as unknown instead of fabricating zero age.

## How operations should speak about it

Use the contract in this order:

1. Measured evidence: `"latest success is unknown because no completed import exists"`
2. Operational interpretation: `"the source has an import gap"`
3. Only later, with business approval, SLA language: `"the source misses its freshness commitment"`

Do not jump from missing evidence to breach language.

## Minimum support answers

Support should be able to answer these from the contract:

- when the source was accepted;
- when reading started;
- when terminal completion happened;
- whether the last record failed, cancelled or ended partial;
- how old the last durable success is;
- which source/scope the evidence belongs to;
- whether the current state is unknown, partial or successful.

## Validation rules

- cancelled and partial states remain explicit;
- missing last-success stays unknown, not zero age;
- the contract does not invent customer SLA hours;
- the contract does not treat UI load as import completion;
- the contract does not remove QDB or MT ownership boundaries.

## Acceptance

- one citeable import SLA evidence contract exists;
- support can answer the OBS-4 questions from the contract language;
- runtime wiring remains a later promoted slice.
