# Decision Board Backend Aggregate Readiness Gate

Date: 2026-06-21
Local HEAD: `c9a18b6757f1cec2c03fb5c87a271675da928294`

## Scope

- [docs/Analytics/ANALYTICS_DECISION_OS_ROADMAP.md](../Analytics/ANALYTICS_DECISION_OS_ROADMAP.md)
- [docs/Analytics/EXECUTIVE_DECISION_BOARD_PLAN.md](../Analytics/EXECUTIVE_DECISION_BOARD_PLAN.md)
- [docs/qa/EXECUTIVE_DECISION_BOARD_QUALITY_AUDIT.md](./EXECUTIVE_DECISION_BOARD_QUALITY_AUDIT.md)
- [docs/qa/ANALYTICS_PRODUCTION_READINESS_STATUS.md](./ANALYTICS_PRODUCTION_READINESS_STATUS.md)
- [docs/qa/ANALYTICS_LIVE_SMOKE_RESULT.md](./ANALYTICS_LIVE_SMOKE_RESULT.md)
- [docs/qa/CONFIDENCE_CALIBRATION_AUDIT.md](./CONFIDENCE_CALIBRATION_AUDIT.md)

## Gate Verdict

NOT READY

Q63 may not proceed yet.

The current Executive Decision Board is good enough for pilot use as a frontend-composed read layer, but the evidence does not yet support freezing that composition into a backend aggregate contract.

## Why This Gate Exists

The backend aggregate endpoint is desirable, but only if it improves:

- quality
- trust semantics
- cache/freshness handling
- dedupe consistency
- ranking stability
- operability

If it lands too early, it will centralize unstable behavior instead of stabilizing it.

## Evidence Summary

### What is already good

- Live analytics smoke is passing on the production pilot surfaces.
- Executive Decision Board quality hardening exists and is covered by targeted tests.
- `insufficient_data` is capped and does not outrank strong recommendations.
- Missing expected impact stays nullable instead of becoming fake `0 RSD`.
- Partial/stale/error states remain visible.

### What is still unstable

- Duplicate source recommendations are still intentionally repeated across sections with context, not deduped globally.
- Cache/freshness evidence is still warning-like in production readiness.
- Confidence calibration is still partial and action-sample based.
- The canonical Phase 1 action ledger contract exists, but source modules are not yet uniformly writing the full creation snapshot.
- Current ranking semantics still live partly in frontend composition rather than one proven shared backend policy.

## Readiness Matrix

| Gate area | Current state | Evidence | Verdict |
| --- | --- | --- | --- |
| Data quality semantics | Honest and visible, but still evolving | `EXECUTIVE_DECISION_BOARD_QUALITY_AUDIT.md`, `ANALYTICS_PRODUCTION_READINESS_STATUS.md` | WARN |
| Ranking stability | Frontend quality guards exist, but no server-side parity contract yet | `EXECUTIVE_DECISION_BOARD_QUALITY_AUDIT.md`, `EXECUTIVE_DECISION_BOARD_PLAN.md` | NOT READY |
| Dedupe strategy | Repetition is intentional by section; no canonical aggregate dedupe rule yet | `EXECUTIVE_DECISION_BOARD_QUALITY_AUDIT.md` | NOT READY |
| Cache / freshness | Honest warning behavior exists, but freshness is not clean enough to centralize snapshot composition | `ANALYTICS_PRODUCTION_READINESS_STATUS.md`, `ANALYTICS_LIVE_SMOKE_RESULT.md` | WARN |
| Confidence / trust contract | Product is strongest, but cross-module calibration is incomplete | `CONFIDENCE_CALIBRATION_AUDIT.md`, `ANALYTICS_DECISION_OS_ROADMAP.md` | NOT READY |
| Performance need | No evidence yet that frontend fan-out is the active pilot bottleneck | current docs set | WARN |
| Aggregate contract clarity | Plan exists, but parity requirements are not yet locked | `EXECUTIVE_DECISION_BOARD_PLAN.md` | NOT READY |

## Detailed Assessment

### 1. Data quality and trust are stable enough for pilot use, not yet for a frozen aggregate contract

The board already protects against the most dangerous trust failures:

- `insufficient_data` does not rank as high-confidence urgent
- stale and partial source states remain visible
- missing impact stays unavailable

That is a strong pilot baseline.

It is not enough by itself to justify a backend aggregate endpoint because the endpoint would need to encode:

- how warning severity changes ranking
- how partial source failure affects section membership
- how missing module inputs affect snapshot-level trust

Those rules are not yet documented as a stable server contract.

### 2. Dedupe policy is not ready

Current board behavior intentionally allows the same source recommendation to appear in multiple sections when the reason is different:

- urgent
- impact
- stock risk
- supplier risk
- blockers
- actions

That is acceptable in the current frontend composition because the section context explains the repetition.

A backend aggregate cannot safely move forward until Trendplus decides one of these explicitly:

1. preserve repeated cards across sections as a first-class rule
2. dedupe globally and attach multi-section reasons
3. dedupe only within selected lanes

Without that decision, a backend aggregate would hard-code behavior that product has not finalized.

### 3. Ranking stability is not yet proven enough for backend centralization

The frontend board currently applies quality safeguards and ordering with localized knowledge.

The roadmap explicitly says the backend aggregate should come only after the Phase 1 board model proves stable.

Current evidence still points to moving parts:

- confidence calibration is incomplete
- source modules do not yet share a fully persisted recommendation calibration contract
- outcome feedback is only partially structured

That means server-side ranking would likely be a premature freeze of logic that is still being learned.

### 4. Cache and freshness discipline are still warning-like

Production readiness explicitly says:

- cache is WARN
- live freshness metadata can still be unknown / warning-like

A backend aggregate endpoint would amplify this risk because it would compose a single snapshot from multiple upstream sources.

Before that happens, Trendplus needs a clearer answer for:

- what freshness threshold invalidates the whole board snapshot
- whether one stale module downgrades one section or the whole board
- how partial refresh failures are represented at aggregate level

### 5. Performance has not yet justified the extra contract

The current plan names one-call aggregation as a possible future benefit.

But the current evidence set does not show that:

- frontend fan-out is currently breaking pilot usability
- request count is the main production bottleneck
- latency is forcing a contract redesign now

Without a demonstrated performance need, the backend aggregate should not outrun the trust/contract work.

## Prerequisites Before READY

Q63 should remain blocked until these prerequisites are satisfied.

### 1. Stable dedupe policy

Trendplus must explicitly choose and document:

- repeated-card policy across sections
- source-key dedupe behavior
- section precedence when one source qualifies for multiple lanes

### 2. Aggregate parity fixtures

Trendplus should have test fixtures that compare:

- current frontend-composed board semantics
- proposed backend aggregate semantics

Required parity areas:

- urgent section ordering
- impact section ordering
- insufficient-data caps
- warning propagation
- nullable impact rendering
- repeated-card handling

### 3. Freshness and partial-failure contract

Before a backend aggregate exists, Trendplus needs a documented server rule for:

- stale source handling
- partial source failure handling
- aggregate-level warnings
- section-level warnings
- snapshot invalidation rules

### 4. Broader ledger adoption

Recommendation/action lineage should be more consistent across source modules before aggregate centralization.

At minimum, main action-producing modules should consistently populate:

- `sourceRecommendationId`
- `recommendationType`
- `confidenceLevel`
- `warningCodes`
- `primaryDrivers`
- `inputFreshnessStatus`

### 5. Confidence calibration follow-through

Confidence calibration does not need to be perfect before Q63, but it should be less partial than it is today.

At minimum, Trendplus should know whether:

- high-confidence decisions actually outperform lower-confidence ones
- source modules are using comparable trust semantics
- action/outcome samples are large enough to support board-level confidence narratives

### 6. Performance baseline

Before changing architecture, collect a small baseline for:

- current board load time
- number of requests
- slowest source dependency
- whether the bottleneck is network fan-out, backend latency, or client rendering

## Conditions That Would Change This Gate to READY

The gate can be revisited as READY only when all of these are true:

1. Board dedupe policy is explicitly documented.
2. Ranking rules are stable enough to be shared server-side.
3. Freshness/partial-failure semantics are documented for an aggregate snapshot.
4. Source modules write enough canonical recommendation context to preserve trust.
5. Parity tests or fixtures exist for the current board semantics.
6. There is a real performance or operability reason to replace frontend composition.

## Q63 Decision

Q63 may not proceed.

Recommended Q63 status after this gate:

- BLOCKED

Blocking reason:

- backend aggregation would currently centralize unstable dedupe, ranking, freshness, and confidence semantics

## Recommended Next Work

Before revisiting Q63, the safer next work is:

1. continue decision learning / outcome/ledger adoption
2. keep confidence calibration evidence growing
3. formalize board dedupe and ranking rules in docs/tests
4. gather actual performance evidence from the current board

## Conclusion

The Executive Decision Board is strong enough to use.
It is not yet strong enough to freeze into a backend aggregate endpoint.

That is a healthy result, not a failure:

- the current frontend composition remains the safer architecture for now
- the gate prevents Trendplus from locking in semantics that are still being validated
