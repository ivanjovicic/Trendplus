# Operations analytics integrity guard (RQ413)

Date: 2026-09-25

## Integrity states

| State | Meaning | Decision signals |
| --- | --- | --- |
| `verified` | Bounded probe reconciled live aggregate, raw-fact oracle and optional cache lane | Allowed (subject to existing trust meta) |
| `unverified` | Import/cache invalidation or bootstrap; probe not yet completed | Warning in meta; recommendations follow existing trust meta |
| `degraded` | Probe disabled or failed | Warning in meta; recommendations follow existing trust meta |
| `drift_detected` | Non-zero revenue/qty delta beyond tolerance | Blocked (fail-closed) |

RQ513 generalizes the guard without replacing the certified Supplier/Shoe Type oracle. The enrolled
families are `supplier_shoe_type`, `sales_dashboard`, `inventory`, `data_quality` and `decision_board`.
Each family has an explicit owner, row/window budget, source generation and context fingerprint.
An absent family-owned independent probe remains `unverified`; it must never be reported as verified
or converted into a zero-valued metric. A probe dependency outage is `degraded`, while only a proven
mismatch is `drift_detected`.

## Surfaces

- Registry: `OperationsAnalyticsIntegrityRegistry` (process-wide snapshot)
- Probe service: `OperationsAnalyticsIntegrityService` (bounded Supplier/Shoe Type window)
- Family probe contract: `IOperationsAnalyticsIntegrityFamilyProbe` (family-owned, independently derived and bounded)
- Worker: `OperationsAnalyticsIntegrityWorker` (scheduled bounded probes)
- Operator API: `GET /api/analytics/operations-integrity`, `POST /api/analytics/operations-integrity/probe`
- Family evidence is exposed in the operator response and persisted with `family`, `context_fingerprint`
  and `source_generation`; stale evidence cannot certify a newly invalidated generation.
- Supplier/Shoe Type stats meta: `operationsIntegrityStatus`, `operationsIntegrityCheckedAtUtc`, `operationsIntegrityEvidenceId`

## Triggers

- Analytics cache clear (`AnalyticsCacheAdminService`, single- or multi-family) → `unverified` + optional background bounded probe
- Access import with analytics cache invalidation → `unverified`
- Web host startup (`OperationsAnalyticsIntegrityStartupHostedService`) → one bounded probe
- Worker interval (`OperationsAnalyticsIntegrityWorker`) → periodic bounded probe
- Successful bounded probe → `verified` or `drift_detected`

## Oracle reuse

Raw-fact SQL lives in `Infrastructure/Services/OperationsAnalyticsRawFactOracle.cs` (same semantics as RQ412 test oracle).
