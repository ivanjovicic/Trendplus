# Decision Timeline Slice-5 Hardening â€” DT08

Date: 2026-08-17
Repo: `ivanjovicic/Trendplus`
Prompt: `DT08`
Contract: `docs/architecture/DECISION_TIMELINE_EXPORT_REPORT_CONTRACT.md`
Rollout: `docs/architecture/DECISION_TIMELINE_ROLLOUT_PLAN.md` Slice 5

## Decision

Timeline export and Product Decision Center labels stay in parity with the DT05 Slice-2 projection. Rejected is not done. `not_measured` is not success or failure. Empty and failed CSV still omit fake zero rates.

## Proven cases

| Case | Proof |
|---|---|
| Rejected vs done | Separate events and funnel counts; rejected row has no `action_executed` |
| Executed but not measured | `notMeasuredCount=1`, `successCount=0`, `successRate` null |
| Delayed pending outcome | No invented `outcome_measured` / `outcome_measurement_started` |
| Missing measurement time on attempted success | Funnel does not count measured success |
| Full lifecycle | Stage timestamps stay distinct; `successRate=1` only over measured |
| Empty / error CSV | No `successRate=0` or `0%` KPI table |
| Export/UI parity | Export copies Slice-2 event/gap codes; UI labels map `action_rejected` â‰  executed and `outcome_not_measured` â‰  measured |

## Out of scope

- Excel / PDF / print CSS
- new event store or schema migration
- PERF16 / shared-SaaS measurement
