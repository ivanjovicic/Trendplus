# Analytics Pilot Release Checklist v2

Date/time: 2026-06-22 11:51 +02:00  
Repo: `ivanjovicic/Trendplus`  
Review HEAD: `a6d48c4da10490e7e2c0fc76131ce7787c1a26e7`  
Scope: evidence-based pilot release gate for the current analytics phase

## Overall Status

Ready with warnings.

The pilot is usable and the critical live surfaces are proven, but the checklist still carries warnings around cache/freshness discipline, action-ledger completeness, and outcome calibration. Those warnings should remain visible until the next follow-up tasks prove otherwise.

## Checklist

| Area | Status | Evidence | Notes |
| --- | --- | --- | --- |
| Deploy proof | PASS | [VERCEL_FRONTEND_REDEPLOY_PROOF.md](./VERCEL_FRONTEND_REDEPLOY_PROOF.md), [ANALYTICS_LIVE_SMOKE_RESULT.md](./ANALYTICS_LIVE_SMOKE_RESULT.md) | Live Vercel now serves the current analytics bundle and the required routes render real content. |
| Live smoke | PASS | [ANALYTICS_LIVE_SMOKE_RESULT.md](./ANALYTICS_LIVE_SMOKE_RESULT.md) | Backend health, readiness, runtime version, refresh status, actions, and decision-board checks all returned successfully. |
| Data quality | PASS | [ANALYTICS_LIVE_SMOKE_RESULT.md](./ANALYTICS_LIVE_SMOKE_RESULT.md), [ANALYTICS_REGRESSION_RISK_AUDIT.md](./ANALYTICS_REGRESSION_RISK_AUDIT.md), [REPLENISHMENT_OOS_WORKFLOW_AUDIT.md](./REPLENISHMENT_OOS_WORKFLOW_AUDIT.md) | Missing data, stale states, and fallback behavior remain visible instead of being hidden as fake green states. |
| Cache / freshness | WARN | [ANALYTICS_LIVE_SMOKE_RESULT.md](./ANALYTICS_LIVE_SMOKE_RESULT.md), [ANALYTICS_PRODUCTION_READINESS_STATUS.md](./ANALYTICS_PRODUCTION_READINESS_STATUS.md) | Freshness metadata is honest, but still warning-like in live evidence. Cache discipline is not yet fully clean. |
| Action ledger | WARN | [ACTION_IMPACT_LEDGER_GAP_REVIEW.md](./ACTION_IMPACT_LEDGER_GAP_REVIEW.md), [ACTION_IMPACT_LEDGER_PHASE1_SPEC.md](../Analytics/ACTION_IMPACT_LEDGER_PHASE1_SPEC.md), [CONFIDENCE_CALIBRATION_AUDIT.md](./CONFIDENCE_CALIBRATION_AUDIT.md) | Action writes are protected and usable, but canonical ledger fields are still not consistently written by every source flow. |
| Confidence calibration | WARN | [CONFIDENCE_CALIBRATION_AUDIT.md](./CONFIDENCE_CALIBRATION_AUDIT.md), [DECISION_CONFIDENCE_CONTRACT.md](../Analytics/DECISION_CONFIDENCE_CONTRACT.md) | Outcome-level calibration is possible in a limited form, but canonical recommendation-level calibration is still partial. |
| Pilot operator readiness | PASS | [ANALYTICS_PILOT_OPERATOR_RUNBOOK.md](../pilot/ANALYTICS_PILOT_OPERATOR_RUNBOOK.md), [ANALYTICS_PRODUCTION_READINESS_STATUS.md](./ANALYTICS_PRODUCTION_READINESS_STATUS.md) | Daily/weekly workflows, escalation rules, and evidence capture are documented and aligned with the current pilot reality. |
| Production rollback notes | WARN | [ANALYTICS_DEPLOY_RECOVERY.md](./ANALYTICS_DEPLOY_RECOVERY.md), [VERCEL_FRONTEND_REDEPLOY_PROOF.md](./VERCEL_FRONTEND_REDEPLOY_PROOF.md) | Recovery steps are documented, but rollback has not been exercised in this checklist. Keep the route-proof docs handy during deploys. |

## Evidence Notes

### Deploy proof

- Vercel route smoke proves the frontend bundle is current enough for the pilot routes.
- The live smoke doc confirms the required analytics routes no longer collapse to the generic stale shell.

### Live smoke

- Backend health and readiness are good on the checked surfaces.
- Runtime version proof is present on Render.
- Admin demo verification remains auth-gated without credentials.

### Data quality

- No-fake-zero and no-fake-green behavior is still enforced in the audited flows.
- Replenishment and forecast surfaces keep missing baseline states visible instead of fabricating certainty.

### Action ledger

- Protected action writes fail safely when forbidden.
- The remaining risk is consistency of canonical ledger fields at creation time, not basic access safety.

### Confidence calibration

- Current calibration is directional and bucket-based.
- It is not yet safe to market confidence as fully outcome-calibrated across the whole decision system.

### Pilot operator readiness

- The runbook describes daily opening checks, queue review, markdown usage, OOS/replenishment usage, and escalation rules.
- The workflow is usable now, but operators must still respect warnings and keep evidence capture disciplined.

### Production rollback notes

- Recovery steps exist for stale frontend or deploy drift situations.
- Keep the rollback docs linked from release notes when a fresh deploy is triggered.

## Release Decision

Ready with warnings.

Use this checklist as the evidence gate for pilot releases, not as a claim that every analytics subsystem is fully finalized.

### Keep visible

- cache/freshness warnings
- partial calibration
- action-ledger completeness gaps
- rollback expectations

### Do not hide

- stale or partial metadata
- missing measured outcomes
- unknown or warning-like freshness
- any route or deploy drift that reappears after the current proof

## Follow-Up

The next follow-up tasks should keep the warnings explicit while tightening the remaining evidence gaps:

- outcome measurement
- confidence calibration
- action impact ledger completeness
- deploy/rollback hygiene
