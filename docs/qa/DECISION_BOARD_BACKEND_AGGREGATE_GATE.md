# Decision Board Backend Aggregate Readiness Gate

Date: 2026-06-22
Local HEAD: `c42fea76ba936aee01fd7efc43a370a4e89ba3f9`

## Scope

- [docs/Analytics/ANALYTICS_DECISION_OS_ROADMAP.md](../Analytics/ANALYTICS_DECISION_OS_ROADMAP.md)
- [docs/Analytics/EXECUTIVE_DECISION_BOARD_PLAN.md](../Analytics/EXECUTIVE_DECISION_BOARD_PLAN.md)
- [docs/qa/EXECUTIVE_DECISION_BOARD_QUALITY_AUDIT.md](./EXECUTIVE_DECISION_BOARD_QUALITY_AUDIT.md)
- [docs/qa/ANALYTICS_PRODUCTION_READINESS_STATUS.md](./ANALYTICS_PRODUCTION_READINESS_STATUS.md)
- [docs/qa/ANALYTICS_LIVE_SMOKE_RESULT.md](./ANALYTICS_LIVE_SMOKE_RESULT.md)
- [docs/qa/CONFIDENCE_CALIBRATION_AUDIT.md](./CONFIDENCE_CALIBRATION_AUDIT.md)
- [docs/qa/DECISION_BOARD_CANDIDATE_CONTRACT_AUDIT.md](./DECISION_BOARD_CANDIDATE_CONTRACT_AUDIT.md)
- [docs/qa/DECISION_BOARD_DEDUPE_RULES.md](./DECISION_BOARD_DEDUPE_RULES.md)
- [docs/qa/DECISION_BOARD_RANKING_PARITY_PLAN.md](./DECISION_BOARD_RANKING_PARITY_PLAN.md)
- [docs/qa/DECISION_BOARD_FRESHNESS_CONTRACT.md](./DECISION_BOARD_FRESHNESS_CONTRACT.md)
- [docs/qa/DECISION_BOARD_AGGREGATE_PERFORMANCE_BUDGET.md](./DECISION_BOARD_AGGREGATE_PERFORMANCE_BUDGET.md)

## Gate Verdict

NOT READY

Q63 may not proceed.

The rerun required by Q63F is complete. Q63A-Q63E clarified the blocking semantics, but they did not close them. The current Executive Decision Board remains strong enough for pilot use as a frontend-composed read layer, not yet strong enough to freeze into a backend aggregate contract.

## Why This Gate Exists

The backend aggregate endpoint is desirable only if it improves:

- quality
- trust semantics
- cache/freshness handling
- dedupe consistency
- ranking stability
- operability

If it lands too early, it will centralize unstable behavior instead of stabilizing it.

## Q63F Rerun Summary

### What changed since the original Q62 gate

Q63A-Q63E are now complete and they materially improved decision clarity:

- Q63A documented the candidate/card contract split between transport DTO, render DTO, and shadow composition helpers.
- Q63B documented canonical dedupe vocabulary and the intended recommendation identity key.
- Q63C documented exact-parity ranking rules and lane-specific acceptance criteria.
- Q63D documented snapshot, source, and candidate freshness/warning layers plus the missing fields.
- Q63E documented the minimum cache, latency, invalidation, and partial-failure budget.

### What did not change yet

The underlying runtime architecture is still not ready to move server-side:

- candidate identity is still not stable enough
- dedupe policy is documented but not yet proven in shared contract/tests
- ranking parity is documented but not yet enforced by parity fixtures
- freshness/warning semantics are documented but still under-expressed in the active card contract
- performance budget exists, but no evidence shows backend aggregation should replace the current composition model now

## Evidence Summary

### What is already good

- Live analytics smoke is passing on the production pilot surfaces.
- Executive Decision Board quality hardening exists and is covered by targeted tests.
- `insufficient_data` is capped and does not outrank strong recommendations.
- Missing expected impact stays nullable instead of becoming fake `0 RSD`.
- Partial/stale/error states remain visible.
- Blocker categories are now explicit enough to evaluate instead of guess.

### What is still unstable

- Duplicate source recommendations are still intentionally repeated across sections with context, not yet enforced through a stable shared identity contract.
- Cache/freshness evidence is still warning-like in production readiness.
- Confidence calibration is still partial and action-sample based.
- The canonical Phase 1 action ledger contract exists, but source modules are not yet uniformly writing the full creation snapshot.
- Current ranking semantics still live partly in frontend composition rather than one proven shared backend policy.

## Readiness Matrix

| Gate area | Current state | Evidence | Verdict |
| --- | --- | --- | --- |
| Candidate contract clarity | Candidate shape is now documented, but the board still mixes aggregate DTOs, local render fields, and shadow helpers | `DECISION_BOARD_CANDIDATE_CONTRACT_AUDIT.md` | NOT READY |
| Dedupe strategy | Canonical key and collision policy are documented, but `sourceType`, `sourceKey`, and `recommendationType` are not yet strong enough everywhere to enforce safely | `DECISION_BOARD_DEDUPE_RULES.md`, `DECISION_BOARD_CANDIDATE_CONTRACT_AUDIT.md` | NOT READY |
| Ranking stability | Parity rules are now explicit, but they are not yet locked by fixtures proving backend parity against current board semantics | `DECISION_BOARD_RANKING_PARITY_PLAN.md` | NOT READY |
| Freshness / warning contract | Snapshot, source, and candidate trust layers are documented, but section warnings, candidate freshness, and warning provenance are still thin in the active contract | `DECISION_BOARD_FRESHNESS_CONTRACT.md` | NOT READY |
| Cache / performance budget | Budget is documented conservatively, but there is still no measured evidence that current board architecture must be replaced for performance or operability reasons | `DECISION_BOARD_AGGREGATE_PERFORMANCE_BUDGET.md`, `ANALYTICS_PRODUCTION_READINESS_STATUS.md` | WARN |
| Confidence / trust contract | Product trust is strongest, but cross-module calibration and action/outcome lineage are still incomplete | `CONFIDENCE_CALIBRATION_AUDIT.md`, `ANALYTICS_DECISION_OS_ROADMAP.md` | NOT READY |
| Data quality semantics | Honest and visible for pilot use, but still not fully encoded as a stable aggregate server contract | `EXECUTIVE_DECISION_BOARD_QUALITY_AUDIT.md`, `ANALYTICS_PRODUCTION_READINESS_STATUS.md` | WARN |

## Detailed Assessment

### 1. Candidate contract is clearer, not yet stable enough

Q63A proved that the board still depends on three overlapping layers:

- transport DTO
- local render DTO
- shadow composition helpers

That means the repo still lacks one clean candidate contract that a backend endpoint can safely promise long term.

Still missing or too thin:

- lane-independent recommendation identity
- mandatory `sourceType`
- mandatory `sourceKey`
- mandatory `recommendationType`
- candidate-level freshness provenance
- candidate-owned source navigation
- explicit warning severity/provenance

### 2. Dedupe policy is documented, not yet enforceable

Q63B established the correct target rule:

- same section + same `sourceType + sourceKey + recommendationType` => dedupe
- different sections + same identity => preserve only when lane meaning is intentionally different
- recommendation vs action vs outcome => keep separate lifecycle cards

That is the right policy direction.

It is still not enough to unblock Q63 because the live candidate contract can still omit pieces needed to apply the rule deterministically.

### 3. Ranking parity is documented, not yet proven

Q63C made the most important backend parity rules explicit:

- insufficient-data caps must survive
- missing impact must remain nullable and non-advantaged
- stale/partial trust must downgrade rank or stay visibly warning-like
- blockers can outrank raw impact
- action lane remains workflow-first

Those rules are now clear.

What is still missing:

- parity fixtures
- exact tie-break evidence
- proof that backend ordering would match the documented policy instead of only resembling it loosely

### 4. Freshness and warning semantics are still too lossy for server centralization

Q63D confirmed that the board trust model already has three layers:

- snapshot
- source/module
- candidate/card

The blocker is that the active aggregate card contract still compresses too much:

- section warnings are dropped in the local board model
- candidate `inputFreshnessStatus` is missing
- warning provenance is not explicit
- unknown vs warning vs partial is not fully separated

A backend aggregate should not centralize this until those layers are preserved more faithfully.

### 5. Performance budget exists, but urgency to centralize is not proven

Q63E is intentionally conservative and that is the right call.

It documents:

- safe latency targets
- cache TTL expectations
- invalidation triggers
- partial-failure behavior
- correlation/error behavior

It does **not** prove that the current board architecture is failing pilot use because of:

- client-side fan-out
- latency
- cache churn
- missing operability

Without that evidence, replacing the current architecture would still be premature.

## Prerequisites Before READY

Q63 should remain blocked until these prerequisites are satisfied.

### 1. Candidate contract closes the current identity gaps

At minimum, board candidates must support a stable recommendation identity with:

- mandatory `sourceType`
- mandatory `sourceKey`
- mandatory `recommendationType`
- clearer action state semantics
- candidate-level freshness and warning provenance

### 2. Dedupe rules are enforceable and testable

Trendplus needs deterministic coverage for:

- same-lane collision collapse
- cross-lane intentional repetition
- recommendation vs action vs outcome separation
- synthetic blocker identity handling

### 3. Ranking parity fixtures exist

Trendplus should have fixtures that compare:

- current frontend-composed board semantics
- proposed backend aggregate semantics

Required parity areas:

- urgent section ordering
- impact section ordering
- insufficient-data caps
- stale/warning downgrades
- blocker precedence
- action workflow ordering
- nullable impact handling

### 4. Freshness and partial-failure contract is preserved end-to-end

Before a backend aggregate exists, Trendplus needs an active contract for:

- stale source handling
- partial source failure handling
- aggregate-level warnings
- section-level warnings
- candidate-level freshness
- snapshot invalidation rules

### 5. Recommendation/action lineage is broader and more consistent

Main action-producing modules should consistently populate:

- `sourceRecommendationId`
- `recommendationType`
- `confidenceLevel`
- `warningCodes`
- `primaryDrivers`
- `inputFreshnessStatus`

### 6. Performance or operability need is proven

Before changing architecture, collect baseline evidence for:

- current board load time
- number of requests
- slowest source dependency
- whether the bottleneck is network fan-out, backend latency, or client rendering
- whether debugging would actually improve with a tighter backend aggregate contract

## Conditions That Would Change This Gate To READY

The gate can be revisited as READY only when all of these are true:

1. Board candidate identity and source identity are explicit and stable.
2. Board dedupe policy is both documented and enforceable in tests.
3. Ranking rules are stable enough to be shared server-side.
4. Freshness/partial-failure semantics are preserved for aggregate, source, and candidate layers.
5. Source modules write enough canonical recommendation context to preserve trust.
6. Parity tests or fixtures exist for the current board semantics.
7. There is a real performance or operability reason to replace frontend composition.

## Q63 Decision

Q63 may not proceed.

Recommended Q63 status after this rerun:

- BLOCKED

Blocking reason:

- backend aggregation would still centralize unstable candidate identity, dedupe enforcement, ranking parity, freshness semantics, and confidence lineage

## Recommended Next Work

The blocker-analysis sequence is complete. The safer next work is outside Q63 itself:

1. continue decision learning / outcome/ledger adoption
2. keep confidence calibration evidence growing
3. convert documented board rules into enforceable parity fixtures and contract tests
4. gather actual performance evidence from the current board before changing architecture

## Conclusion

The Executive Decision Board is strong enough to use.
It is not yet strong enough to freeze into a backend aggregate endpoint.

That is a healthy result, not a failure:

- the current frontend composition remains the safer architecture for now
- Q63F did its job by replacing vague uncertainty with specific documented blockers
- Q63 stays blocked until later evidence changes the verdict
