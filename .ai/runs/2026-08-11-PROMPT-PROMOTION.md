# Prompt promotion log - 2026-08-11

Agent: Cursor
Action: promote eligible WAITING prompts (and queue SEC follow-ups) to READY; fix stale MASTER READY pointers; demote unsafe QDB03.

## Promoted to READY

| Prompt | Program | Why eligible |
|---|---|---|
| `RL02` | RL | `RL01` DONE; planning-only |
| `P-UI-09` | P-UI | P-UI-01-08 DONE; copy/UX only (no outcome semantics) |
| `SEC03` | SEC | New from SEC02 slice S2-1; docs-only assurance |

## Queued as WAITING (not READY)

| Prompt | Why not READY |
|---|---|
| `SEC04` (S2-2) | One READY per SEC; after SEC03 |

## Demoted / kept WAITING

| Prompt | Reason |
|---|---|
| `QDB03` | Concurrent READY was unsafe while `BCI05`/`BCI01` PARTIAL; restored WAITING |
| `PERF02` | Needs recorded S-tier measurements |
| `MT02` | Needs owner identity/membership approval |
| `GAI*` | Core pilot / release gate BLOCKED |
| RQ WAITING (e.g. RQ81) | BCI05 GHA override still active |
| OBS O2-1 | Not queued as READY (runtime) |
| `BCI05` | Remains PARTIAL (commit/push/GHA) |

## MASTER corrections

- Replaced stale `P-UI-07` / `MT01` DONE-as-READY claims.
- Aligned QDB/SEC/RL/P-UI with owner queues.
