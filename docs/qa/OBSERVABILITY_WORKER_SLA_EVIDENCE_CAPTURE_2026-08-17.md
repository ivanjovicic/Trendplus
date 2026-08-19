# Worker SLA Evidence Capture â€” OBS09

Date: 2026-08-17
Repo: `ivanjovicic/Trendplus`
Prompt: `OBS09`
Contract: `docs/architecture/OBSERVABILITY_WORKER_SLA_EVIDENCE_CONTRACT.md` (`OBS08`)
Agent: cursor

## Decision

Worker SLA evidence can be cited without fake-green defaults. OBS08 remains the contract source of truth. This slice captures current sources and projects them onto OBS08 field ids. Uninstrumented fields stay **null/unknown**, not `0` or green.

No alerting rules were added. Numeric SLA hours were not invented.

## Capture path

Runtime projector: `Infrastructure/Services/WorkerSlaEvidenceMapper.cs`
Exposed as additive `SlaEvidence` on `GET /api/workers/health`. Existing health counts are unchanged.

## Field capture vs unknown

| OBS08 field | Current source | Capture result |
|---|---|---|
| `workerName` | `WorkerHealthService` in-memory inventory | measured when a worker has reported |
| `workersGloballyEnabled` | `WorkerRuntimeControlService.IsEnabled` | measured |
| `executionState` | global switch + health status/stale flag | `paused` when global switch is off; `unknown` when heartbeat is stale/missing; never healthy silence |
| `pauseReason` | global switch or stopped message | measured when paused/stopped; otherwise null |
| `lastHeartbeatAtUtc` / `heartbeatAgeSeconds` | `LastHeartbeat` | measured when heartbeat is non-default; otherwise unknown |
| `queueDepth` | none (W5 gap) | **unknown** (`null`) + `w5_queue_depth_not_instrumented` |
| `oldestWorkAgeSeconds` | none (W5 gap) | **unknown** (`null`) + `w5_oldest_work_age_not_instrumented` |
| `runDurationSeconds` | none | **unknown** (`null`) |
| `successCount` / `failureCount` / `retryCount` / `deadLetterCount` | none as durable W6 | **unknown** (`null`); in-memory `ErrorCount` is not used as success or DLQ |
| `lastSuccessfulRunAtUtc` / `lastSuccessfulRunAgeSeconds` | none (heartbeat is not success) | **unknown** (`null`) + `last_successful_run_unknown` |
| `lastErrorPresent` | `LastError` | measured bool; empty error is not treated as healthy if stale |
| `sourceJobId` / `sourceSystem` / `correlationId` | not on health payload | **unknown** (`null`) |
| `dataQualityStatus` | derived | `unknown` when inventory missing or globally paused; otherwise `partial` because W5/W6/last-success remain unknown |

Empty worker inventory is `inventoryStatus=unknown` with `worker_inventory_missing`, not â€œ0 workers, therefore healthyâ€.

## Out of scope

- alerting rules
- numeric SLA hours
- per-worker `WorkerRuntimePolicyService` pause on the snapshot
- inventing queue depth from â€œno jobs observedâ€
