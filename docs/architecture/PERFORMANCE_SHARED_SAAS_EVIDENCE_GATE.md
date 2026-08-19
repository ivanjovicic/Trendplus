# Performance Shared-SaaS Evidence Gate

Status: authoritative PERF15 docs-only gate
Date: 2026-08-14
Related contract: `docs/architecture/PERFORMANCE_SCALABILITY_GATE_EVIDENCE_CONTRACT.md` (PERF09 D8)
Related roadmap: `docs/roadmaps/PERFORMANCE_ROADMAP.md`
Related MT owner: `docs/ai/MULTITENANCY_PROMPT_QUEUE.md` + `docs/architecture/MULTITENANCY_ARCHITECTURE_ROADMAP.md`
Related dedicated evidence: `.ai/runs/2026-08-12-PERF14-evidence.md`

## Purpose

Freeze when a performance pack may, and may not, claim `shared_saas` scalability.

This gate exists so later PERF work cannot promote G10/G50 shared-SaaS readiness from dedicated-host measurements, missing tenant fixtures, or invented isolation overhead.

It is **documentation only**. It does **not**:

- invent numeric SLOs, overhead milliseconds, or customer-count promises;
- authorize runtime optimization, harness changes, or a shared-SaaS measurement run;
- promote `MT02` or any later MT prompt;
- treat `n/a_dedicated` as a shared-SaaS pass.

## Current supported mode

The live pilot model remains **dedicated deploy per customer**.

Until the MT owner proves otherwise:

- one customer per deployment/database/storage/cache scope;
- PERF packs that ran on a single host are `deploymentMode=dedicated`;
- D8 status for those packs is `n/a_dedicated`;
- `shared_saas` remains unclaimed.

Citeable current MT truth:

- MT current READY: none;
- `MT02` WAITING on owner identity/membership or single-tenant API-key binding;
- `MT10` WAITING two-tenant isolation suite and shared-SaaS go/no-go;
- until `MT10` is `DONE`, supported isolation is dedicated deploy.

## D8 ownership

| Field | Owner | PERF may |
|---|---|---|
| `tenantCountInFixture` | MT fixture / `MT10` suite | record the MT-cited count, never invent one |
| `crossTenantLeakChecks` | MT isolation assertions | cite pass/fail from MT evidence only |
| `overheadP95Ms` | PERF, only after MT fixtures exist | measure extra latency vs single-tenant on the same hardware |
| `mtGateIds` | MT prompt IDs that authorize the claim | copy the MT IDs; do not invent a substitute gate |
| `status` | PERF pack author | `n/a_dedicated` on dedicated packs; `unmeasured`/`deferred` until MT fixtures; `measured` only after MT-authorized fixtures |

PERF does not own tenant identity, membership, cache/job/storage isolation, or the shared-SaaS release verdict. Those stay in the MT program.

## Status rules

| Pack deployment mode | Required D8 status | Meaning |
|---|---|---|
| `dedicated` | `n/a_dedicated` | Isolation overhead is out of scope. Not a shared-SaaS pass. |
| `shared_saas` without MT fixtures/`MT10` | forbidden as a claimed mode | Do not label a dedicated pack `shared_saas`. |
| `shared_saas` after MT fixtures exist, before measurement | `unmeasured` or `deferred` | Mode is authorized to be measured; numbers stay null. |
| `shared_saas` after MT leak checks pass and overhead is measured | `measured` | Cite `mtGateIds` and the PERF pack. |

Null numeric D8 fields stay null. Missing overhead is not `0 ms`. Missing leak checks are not `pass`.

## Forbidden claims

Do not write any of the following from dedicated PERF10–PERF14 evidence:

- “ready for 10/50 shared-SaaS customers”;
- “tenant isolation overhead is negligible / 0 ms”;
- “cross-tenant leak checks passed” without MT `MT10` evidence;
- promoting G10/G50 `shared_saas` because G10 `dedicated` dimensions D1–D7 were measured;
- using store id, user id, or source key as tenant identity in a PERF pack.

Dedicated evidence remains valid for dedicated-mode planning only.

## Reopen conditions

A later PERF prompt may measure D8 only when all of the following are true:

1. MT current truth no longer forbids shared data-plane customers. The expected owner gate is `MT10` `DONE`, or an explicit owner decision recorded in `MASTER_ROADMAP.md`.
2. A two-tenant fixture exists with colliding business/source identifiers.
3. Cross-tenant leak checks are MT-owned and executable.
4. The PERF pack header says `deploymentMode=shared_saas` and lists `mtGateIds`.
5. Overhead compares the shared fixture against a single-tenant baseline on the same hardware.

Until those conditions exist, keep D8 `n/a_dedicated` on dedicated packs and do not start a shared-SaaS measurement harness in PERF.

## Mapping to existing packs

| Pack | Mode | D8 |
|---|---|---|
| PERF10 G10 dedicated | `dedicated` | `n/a_dedicated` |
| PERF11 G10 dedicated | `dedicated` | `n/a_dedicated` |
| PERF12 G10 dedicated | `dedicated` | `n/a_dedicated` |
| PERF13 G10 dedicated | `dedicated` | `n/a_dedicated` |
| PERF14 G10 dedicated | `dedicated` | `n/a_dedicated` (explicit in `.ai/runs/2026-08-12-PERF14-evidence.md`) |

No later dedicated pack may change those D8 rows to `measured` without an MT-authorized shared fixture.

## Acceptance (PERF15)

- D8 is explicitly MT-owned;
- dedicated packs stay `n/a_dedicated`;
- shared-SaaS claims stay blocked until MT fixtures or an owner-recorded gate;
- no invented measurements or runtime optimization shipped by this gate.
