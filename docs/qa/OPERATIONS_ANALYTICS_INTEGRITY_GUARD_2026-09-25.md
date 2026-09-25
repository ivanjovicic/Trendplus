# Operations analytics integrity guard (RQ413)

Date: 2026-09-25

## Integrity states

| State | Meaning | Decision signals |
| --- | --- | --- |
| `verified` | Bounded probe reconciled live aggregate, raw-fact oracle and optional cache lane | Allowed (subject to existing trust meta) |
| `unverified` | Import/cache invalidation or bootstrap; probe not yet completed | Warning in meta; recommendations follow existing trust meta |
| `degraded` | Probe disabled or failed | Warning in meta; recommendations follow existing trust meta |
| `drift_detected` | Non-zero revenue/qty delta beyond tolerance | Blocked (fail-closed) |

## Surfaces

- Registry: `OperationsAnalyticsIntegrityRegistry` (process-wide snapshot)
- Probe service: `OperationsAnalyticsIntegrityService` (bounded Supplier/Shoe Type window)
- Worker: `OperationsAnalyticsIntegrityWorker` (scheduled bounded probes)
- Operator API: `GET /api/analytics/operations-integrity`, `POST /api/analytics/operations-integrity/probe`
- Supplier/Shoe Type stats meta: `operationsIntegrityStatus`, `operationsIntegrityCheckedAtUtc`, `operationsIntegrityEvidenceId`

## Triggers

- Analytics cache clear (`AnalyticsCacheAdminService`) → `unverified`
- Access import with analytics cache invalidation → `unverified`
- Web host startup (`OperationsAnalyticsIntegrityStartupHostedService`) → one bounded probe
- Worker interval (`OperationsAnalyticsIntegrityWorker`) → periodic bounded probe
- Successful bounded probe → `verified` or `drift_detected`

## Oracle reuse

Raw-fact SQL lives in `Infrastructure/Services/OperationsAnalyticsRawFactOracle.cs` (same semantics as RQ412 test oracle).
