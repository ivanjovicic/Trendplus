# Analytics Decision OS Roadmap

Updated: 2026-06-19

## Purpose

Trendplus Analytics should evolve into a decision operating system for retail:

signal -> recommendation -> confidence -> action -> outcome -> learning

This roadmap exists so future Codex tasks stay aligned with that flow instead of drifting into random dashboard work.

## Current State Summary

- Backend live smoke passed on the Render surfaces that matter for analytics.
- Vercel frontend redeploy proof passed on the required analytics routes.
- Full live analytics smoke passed after the frontend redeploy.
- Product Decision confidence exists and is already guarded by tests.
- Executive Decision Board exists and now has a quality audit.
- Protected analytics action write UX exists and fails safely on 401/403.
- No fake zero and no fake green rules are active in the repo standards.

## Inputs That Anchor This Roadmap

- [Decision Confidence Contract](DECISION_CONFIDENCE_CONTRACT.md)
- [Action Impact Ledger Plan](ACTION_IMPACT_LEDGER_PLAN.md)
- [Executive Decision Board Plan](EXECUTIVE_DECISION_BOARD_PLAN.md)
- [Analytics Regression Risk Audit](../qa/ANALYTICS_REGRESSION_RISK_AUDIT.md)
- [Executive Decision Board Quality Audit](../qa/EXECUTIVE_DECISION_BOARD_QUALITY_AUDIT.md)
- [Analytics Live Smoke Result](../qa/ANALYTICS_LIVE_SMOKE_RESULT.md)
- [Vercel Frontend Redeploy Proof](../qa/VERCEL_FRONTEND_REDEPLOY_PROOF.md)

## Principles

- Backend is the source of truth.
- No fake zero.
- No fake green.
- No frontend-invented confidence.
- Stale, partial, and error states must remain visible.
- Every recommendation should be explainable.
- Every action should eventually have an outcome.
- Missing evidence should stay nullable, not become a fabricated value.

## Phase A: Stabilization and unknown-bug detection

### Goal

Keep the current analytics surfaces honest, observable, and safe to trust.

### Why it matters

If deploy drift, stale state, or silent fallback hides a failure, every later analytics feature becomes less believable.

### Deliverables

- Regression risk audits for high-churn analytics pages.
- Visible warnings when ancillary filter/list refreshes fail.
- Better correlation IDs and error breadcrumbs for live incidents.
- Clear route smoke and live smoke evidence for the main analytics paths.

### Risks

- More diagnostics can be added without actually making the root cause easier to find.
- Observability can be uneven between backend and frontend if the correlation-id path is not standardized.

### Tests

- `git diff --check`
- targeted frontend regression tests on the audited pages
- live smoke rechecks when deploy hashes change
- error-state tests that confirm stale and partial data remain visible

### Suggested commit sequence

1. Audit the highest-risk surfaces.
2. Add the smallest honest warning or traceability fix.
3. Re-run live smoke.
4. Capture the proof in docs before moving on.

## Phase B: Decision quality and confidence expansion

### Goal

Make confidence semantics consistent across product, supplier, inventory, and the executive board.

### Why it matters

Decision surfaces should explain why a recommendation is strong, weak, stale, or insufficient instead of letting each page invent its own semantics.

### Deliverables

- Product Decision confidence calibration review.
- Supplier confidence contract mapping.
- Inventory decision confidence mapping.
- Executive Decision Board quality audit and follow-up checks.
- Shared handling for stale/partial/error warnings near each recommendation.

### Risks

- Module-specific heuristics may drift apart again if the shared contract is not enforced.
- Numeric scores can start to look authoritative even when the data is weak.

### Tests

- contract tests for `insufficient_data`, warning propagation, and nullable impact
- page tests that confirm high-confidence UI does not appear for weak evidence
- regression checks for repeated recommendations and section-level context

### Suggested commit sequence

1. Lock the shared confidence vocabulary.
2. Map each module to the shared contract.
3. Add focused UI tests for the highest-risk decision cards.
4. Re-run board and module smoke tests.

## Phase C: Action Impact Ledger implementation

### Goal

Connect recommendations to outcomes so the system can learn from what happened after the action.

### Why it matters

Without a ledger, Trendplus can recommend actions but cannot close the loop on whether the recommendation worked.

### Deliverables

- Creation snapshot contract for action recommendations.
- Outcome resolution contract with measured impact and evidence.
- Read model for expected vs measured impact.
- Action Queue detail/history view that shows the original recommendation and later outcome.

### Risks

- Workflow resolution can get mixed up with business measurement.
- Missing evidence can be rewritten as fake precision if the ledger contract is too loose.

### Tests

- backend unit tests for snapshot and resolution semantics
- integration tests for action -> outcome -> summary flow
- frontend tests that show missing impact as unknown instead of zero

### Suggested commit sequence

1. Lock the snapshot fields.
2. Lock the resolution fields.
3. Add the summary/read model.
4. Expose the ledger in the UI.

## Phase D: Executive Decision Board backend aggregate endpoint

### Goal

Move the board composition from frontend orchestration to a dedicated backend aggregate once the phase 1 model is proven stable.

### Why it matters

A server-side aggregate can reduce request fan-out and keep ranking semantics in one place, but only after the current board behavior is stable.

### Deliverables

- Read-only aggregate endpoint for the board.
- Server-side section model and ranking logic.
- Honest metadata for partial, stale, or missing source modules.

### Risks

- Doing this too early can freeze bad semantics into a server contract.
- A premature aggregate can hide the current phase 1 truth instead of formalizing it.

### Tests

- aggregate endpoint contract tests
- null/insufficient-data handling tests
- source-link and section-order tests

### Suggested commit sequence

1. Keep the frontend board stable.
2. Design the aggregate shape.
3. Implement the endpoint.
4. Switch the board only after the aggregate matches the phase 1 model.

## Phase E: Supplier and Inventory decision confidence

### Goal

Apply the same confidence contract to supplier and inventory decision surfaces without creating new semantics.

### Why it matters

These are high-volume operational signals; if they drift from the shared contract, the executive board and action queue become harder to trust.

### Deliverables

- Supplier confidence contract mapping.
- Inventory decision confidence mapping.
- Shared warning and freshness semantics for both surfaces.
- Consistent `insufficient_data` handling across list, detail, and report views.

### Risks

- Fallback logic can become the de facto algorithm if it is not documented and tested.
- A surface may look complete even when it is only partially supported by evidence.

### Tests

- module-specific confidence mapping tests
- warning-code propagation tests
- empty/partial state tests that keep the UI honest

### Suggested commit sequence

1. Normalize supplier confidence.
2. Normalize inventory confidence.
3. Reuse the shared helpers in the board and action queue.
4. Re-run the regression audits.

## Phase F: Forecasting and scenario simulation

### Goal

Add what-if and forecast capabilities only after data quality, confidence, and outcome learning are trustworthy.

### Why it matters

Forecasts without stable inputs become fake precision and can easily mislead operators.

### Deliverables

- Scenario comparison views.
- Forecast bands or ranges instead of overly precise point estimates.
- Clear separation between projection and fact.

### Risks

- Overfitting to weak history.
- Scenario outputs that look authoritative while the underlying data is still incomplete.

### Tests

- calibration checks against historical outcomes
- null and sparse-data tests
- scenario input validation tests

### Suggested commit sequence

1. Finish the ledger and confidence contracts.
2. Prototype one narrow scenario view.
3. Validate it against outcome history.
4. Expand only if the results are honest and useful.

## Phase G: Admin and operator analytics governance

### Goal

Make analytics safe to operate in production and easy to diagnose when something changes.

### Why it matters

Production analytics needs clear guardrails for refresh, cache, admin workflows, and incident tracing.

### Deliverables

- Correlation-id and traceability standards across backend and frontend.
- Safer admin/operator actions and recovery paths.
- Clear docs for live smoke, redeploy proof, and incident follow-up.
- No destructive demo-reset flow hidden as a convenience feature.

### Risks

- A governance layer can become too permissive if it is only documented and not tested.
- Admin surfaces can accidentally leak operational power into user-facing flows.

### Tests

- auth gate tests for protected admin actions
- traceability and logging tests
- live smoke proof after deployment changes

### Suggested commit sequence

1. Standardize diagnostics.
2. Harden admin/operator guards.
3. Document the recovery flow.
4. Re-run live smoke before calling anything stable.

## Do Not Do Yet

- Do not build the backend aggregate Decision Board endpoint until the current frontend board model is stable.
- Do not add ML forecasting before data quality and the outcome ledger exist.
- Do not hide unknown, stale, partial, or error states.
- Do not add destructive demo reset flows.

## Roadmap to Queue Map

- Q38: Analytics regression risk audit
- Q39: Executive Decision Board quality audit
- Q40: Analytics observability / correlation-id hardening
- Q41: Action Impact Ledger Phase 1 design-to-implementation gap review
- Q42: Product Decision confidence calibration review
- Q43: Supplier confidence contract mapping
- Q44: Inventory decision confidence mapping
- Q45: Decision Board backend aggregate readiness review

## Acceptance

- The current analytics state is summarized in one place.
- The roadmap clearly separates stabilization, confidence, action outcomes, forecasting, and governance.
- The roadmap points future task work toward the right phase.
- No application code is required for this document.
