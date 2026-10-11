# RQ612 — shadow-control feasibility and RQ592 pilot design

Date: 2026-10-11
Status: exploratory screen complete; no controls qualified for a causal/pilot conclusion
Source query: `tools/markdown-pilot-shadow-control-candidates.sql`

## Read-only batch-23 result

The new query ran against the local `trendplus` PostgreSQL 16.13 primary with the session forced read-only. It reused the treated-event values from the frozen RQ602 manifest and reserved all 40 frozen member article IDs. No import, write, migration, price action, or manifest change occurred.

Across 20 treated events, the query found 1,758 candidate article/event pairs in the exact same store + supplier + shoe-type stratum. Its configurable exploratory screen requires at least one gross pre-period unit, a pre-price within 25% of the frozen treated pre-price, signed net-unit activity between 0.5× and 2× the treated pre-period signed units, and no markdown event in the 56-day `[action_date − 28, action_date + 28)` window. The thresholds are discovery defaults, not statistical or business acceptance cutoffs.

Four one-to-one exploratory candidates remained after those checks. A candidate is assigned to at most one treated event; RQ602's frozen members are excluded from this new pool.

| Frozen cohort | Treated article | Exploratory control | Control pre-period sales | Price difference vs treated baseline | Signed-demand ratio | Markdown overlap |
|---:|---:|---:|---:|---:|---:|---:|
| 1 | 21609 | 21610 | 4 units / 4 active days | +20.0% | 1.33× | 0 |
| 2 | 21621 | 21492 | 3 units / 2 active days | −11.1% | 0.75× | 0 |
| 11 | 21460 | 21306 | 2 units / 2 active days | −2.7% | 1.00× | 0 |
| 17 | 21482 | 21597 | 2 units / 2 active days | 0.0% | 2.00× | 0 |

The event-scoped diagnostics report 1,609 zero-signal candidates, 65 pre-price mismatches, 12 pre-demand mismatches, one markdown-confounded candidate, and eight reuse-conflicted rows. These are separate reason counters; reuse can overlap with otherwise passing comparisons and the counters should not be summed as a partition.

**Fully qualified pairs: 0.** All four exploratory candidates carry `unverified_no_historical_daily_stockout_or_availability_evidence` and `unverified_batch23_per_store_day_source_coverage`. Therefore the result is `insufficient_control_population_due_to_unverified_availability_and_source_coverage`, not a recommendation to use these controls. Current `Artikli.Kolicina` was not read as historical availability. Signed returns remain included in net quantity/revenue, and unknown cost remains outside any margin conclusion.

RQ611 now supports future import day-coverage capture after its migration is deployed; it cannot retroactively certify batch 23. RQ610 is blocked on a reliable stock mutation-generation/provenance contract. No daily availability or stockout proof exists for these historical windows.

## Exploratory screening assumptions

- Source batch is fixed to 23; local time windows are half-open and use `Europe/Belgrade`.
- `DUG` and `KOREKCIJA` receipts are excluded. Sale quantities stay signed; positive sold units establish evidence of pre-period demand, while signed net units determine relative demand.
- Candidates must match the treated store, supplier and shoe type. Supplier-only matches are counted as rejected strata.
- Candidate pre-price uses positive-quantity sales; gross prices are not inferred from today's product master.
- Any observed price reduction in the candidate's 56-day comparison window excludes it. This catches overlap/repeated markdown events in available `DnevnikPromena` data, but missing event provenance still limits confidence.
- Controls are unique across the treated events. A future design that reuses controls must preregister explicit multiplicity/weighting.
- Exploratory thresholds can be varied and re-run. They are not evidence of balance, statistical power, causal identification, or campaign readiness.

## RQ592 prospective pilot candidate design

This is a design proposal only. It does not authorize a price change, recommendation, customer communication, or production experiment.

### Eligibility and freeze

1. Business owner and store operator approve the participating stores, SKUs, supplier/shoe-type strata, price action, exact start/end timestamps, and customer/tenant scope. Obtain any required consent or operational sign-off before enrollment.
2. Select one treated SKU and one unique same-store, same-supplier, same-shoe-type shadow control with measured nonzero pre-period sales, comparable pre-price and signed demand, and no overlapping promotion or markdown. Freeze both IDs, matching rationale, source batch/generation, analysis windows, and exclusions before the action. Do not reuse a control unless multiplicity and weights are preregistered.
3. Require reliable source coverage for every requested store/business day and prospective on-hand/availability observations through both windows. A missing day, failed import, unknown stock, or unverified stockout is not a zero and makes the affected pair/window ineligible for a comparable outcome.

### Measurement and validity

- Use a 28-day pre-window `[action_date − 28 days, action_date)` and a 28-day outcome window `[action_date, action_date + 28 days)` in `Europe/Belgrade`, unless the owner preregisters a more defensible seasonal horizon before assignment.
- Record daily signed units, returns, revenue and documented availability for both articles. Report window totals and observed-day coverage separately; do not divide by assumed days or fill missing days with zero.
- Report gross margin only when each sale line has confirmed cost provenance under RQ601. Otherwise use units/revenue descriptors and label margin unknown.
- A control is contaminated by its own price change, overlapping campaign, assortment transfer, or out-of-stock/missing-availability period; flag or stop the pair according to the frozen protocol.
- Treat the first pilot as feasibility evidence. Do not infer uplift or causal markdown effect from the historical RQ602 cohort or from a small exploratory screen. Preregister the primary estimand, sample-size/power method, analysis and exclusions before a causal readout.

### Stop conditions and readiness gates

Pause enrollment or mark the pair non-comparable if a source batch fails, store/day coverage is incomplete, stock availability cannot be observed, the control receives a price action/promotion, product identity changes, returns cannot be reconciled, or the frozen assignment is altered. Resume only after the data/owner issue is resolved and documented before looking at outcomes.

RQ592 remains gated by STAB16 freshness/provider authority, owner approval, deployed day-coverage/availability evidence, and a preregistered design. This RQ612 artifact is a shadow-only feasibility result; it does not alter the immutable RQ602 manifest or unlock pilot execution.

## Validation

- Read-only batch-23 query: passed on the local business dataset with session-level `default_transaction_read_only=on`.
- Disposable PostgreSQL query fixture: `MarkdownPilotShadowControlSqlTests` covers nonzero vs zero pre-sales, wrong supplier, signed returns, `DUG`/`KOREKCIJA`, half-open date boundaries, price mismatch, repeated markdown, reuse, unknown cost and unverified availability/coverage.
- No historical stockout, per-day sales completeness, confirmed margin, causal effect, production readiness, or owner consent is claimed.
