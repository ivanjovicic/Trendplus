# Confidence Calibration Audit

Date: 2026-06-21
Local HEAD: `43f5ca9`

## Scope

- [docs/Analytics/DECISION_CONFIDENCE_CONTRACT.md](../Analytics/DECISION_CONFIDENCE_CONTRACT.md)
- [docs/Analytics/ANALYTICS_DECISION_OS_ROADMAP.md](../Analytics/ANALYTICS_DECISION_OS_ROADMAP.md)
- [docs/Analytics/ACTION_OUTCOME_ANALYTICS_PLAN.md](../Analytics/ACTION_OUTCOME_ANALYTICS_PLAN.md)
- [docs/Analytics/ACTION_IMPACT_LEDGER_PLAN.md](../Analytics/ACTION_IMPACT_LEDGER_PLAN.md)
- [docs/Analytics/EXECUTIVE_DECISION_BOARD_PLAN.md](../Analytics/EXECUTIVE_DECISION_BOARD_PLAN.md)
- [docs/qa/PRODUCT_DECISION_CONFIDENCE_AUDIT.md](PRODUCT_DECISION_CONFIDENCE_AUDIT.md)
- [docs/qa/SUPPLIER_CONFIDENCE_CONTRACT_AUDIT.md](SUPPLIER_CONFIDENCE_CONTRACT_AUDIT.md)
- [docs/qa/INVENTORY_DECISION_CONTRACT_AUDIT.md](INVENTORY_DECISION_CONTRACT_AUDIT.md)
- [docs/qa/EXECUTIVE_DECISION_BOARD_QUALITY_AUDIT.md](EXECUTIVE_DECISION_BOARD_QUALITY_AUDIT.md)

## Purpose

Q60 is an audit, not an implementation task. The goal is to separate:

- surfaces that already expose enough evidence to be calibrated
- surfaces that only expose descriptive confidence
- surfaces that depend on outcome history before calibration can be trusted

This keeps us from treating recommendation confidence as if it were already outcome-validated.

## Current calibration-ready surfaces

### 1. Product Decision Center

Product Decision Center is the strongest candidate for confidence calibration because it already exposes:

- `confidencePct`
- `reliabilityPct`
- `reasonCodes`
- `dataQualityStatus`
- `expectedImpactRsd`
- `impactWindowDays`
- `inputFreshnessStatus`

It is already the closest thing in the repo to a decision surface with explainability plus numeric trust signals.

What still blocks full calibration:

- the confidence fields are descriptive and signal-based, not yet proven against a stable historical calibration set
- the outcome loop still depends on action outcome data rather than a dedicated calibration report
- missing or partial evidence still appears in several modules, so confidence cannot be treated as a proof score

### 2. Inventory decisions

Inventory is also calibratable in principle because it already carries:

- `signalConfidencePct`
- `recommendationAllowed`
- `reasonCodes`
- `dataQualityStatus`
- stock risk / sell-through indicators

Inventory has enough trust vocabulary to be audited, but the confidence semantics are still mostly descriptive:

- the page tells us how strong the signal is
- it does not yet prove that the signal is historically well calibrated

### 3. Supplier decision surfaces

Supplier summary and report surfaces already expose:

- `confidenceScore`
- `reliabilityPct`
- `reasonCodes`
- `statusReason`
- `dataQualityStatus`
- fallback / recommendation-allowed semantics

This is enough to bucket and compare supplier signals, but not enough to claim outcome calibration by itself.

### 4. Action Outcome Summary and Action Impact Ledger

These are the learning surfaces, not the source of recommendation truth.

They already expose:

- `expectedImpactRsd`
- `measuredImpactRsd`
- `realizationRatio`
- warning codes
- sample size and measured sample size
- pending / not-measured separation

That makes them the right foundation for calibration metrics, but only after enough outcomes exist to make the comparison meaningful.

### 5. Executive Decision Board

The board can surface calibration context, but it should not invent calibration on its own.

It is a composition surface:

- it can display confidence and impact signals from upstream modules
- it can warn when evidence is weak or stale
- it should not compute new calibration semantics in the browser

## What can be measured now

The repo can already measure these calibration-adjacent signals:

| Measure | Source | What it tells us |
|---|---|---|
| Confidence bucket distribution | Product, Inventory, Supplier | How much of the recommendation pipeline lives in high / medium / low / insufficient data |
| Outcome coverage | Action Outcome Summary | How often closed actions actually have measurable outcomes |
| Realization ratio | Action Outcome Summary / Ledger | Whether expected impact is roughly matched by measured impact |
| Negative outcome share | Action Outcome Summary | Whether weak recommendations are leading to harmful outcomes |
| Data quality cohort | All recommendation surfaces | Whether trust problems cluster around poor inputs |
| Freshness state | All trust headers and board surfaces | Whether stale inputs are dragging confidence down |

These are enough to start a calibration audit, but not enough to claim a fully validated confidence model.

## Missing data and outcome dependencies

### Missing data

- some surfaces still expose confidence as a numeric score without a shared calibration bucket
- some rows still lack stable recommendation identity across all layers
- some source modules can show confidence but not a durable expected-vs-measured outcome pairing
- some empty or partial states are still descriptive, not fully outcome-aware

### Outcome dependencies

- calibration needs a measurable link between recommendation and outcome
- `pending` must stay separate from failure
- `not_measured` must stay separate from `negative`
- `null` measured impact must remain `null`, not `0`
- small samples need an `insufficient_data` bucket instead of fake precision

## Recommended calibration buckets

Use a small set of buckets that are easy to explain and test:

- `well_calibrated`
- `over_confident`
- `under_confident`
- `insufficient_data`

### Suggested meaning

- `well_calibrated`: the stated confidence usually matches the observed result
- `over_confident`: the recommendation sounded stronger than the measured outcome supports
- `under_confident`: the recommendation worked better than its stated confidence suggested
- `insufficient_data`: sample size or denominators are too weak to trust a bucket yet

## Future metrics

These are the metrics the repo should eventually compute once the outcome history is stable enough:

| Metric | Why it matters | Notes |
|---|---|---|
| Calibration bucket rate by confidence band | Shows whether high confidence really behaves better than low confidence | Needs enough sample volume per band |
| Outcome coverage by module | Shows which modules close the learning loop best | Separate from recommendation volume |
| Mean / median realization ratio | Shows whether expected impact is directionally honest | Must ignore null denominators |
| Over-confident share | Flags modules that promise more than they deliver | Needs a stable expected vs measured pairing |
| Under-confident share | Flags modules that are too conservative | Useful for tuning, not for ranking alone |
| Data-quality adjusted calibration | Separates weak-input noise from model weakness | Requires consistent `dataQualityStatus` |

## Where current confidence is still descriptive

Current confidence is still descriptive rather than outcome-validated on most surfaces:

- Product Decision Center confidence explains signal strength, not a historical calibration guarantee
- Inventory signal confidence explains stock-risk strength, not a proven outcome forecast
- Supplier confidence explains recommendation strength, not a completed learning loop
- Executive Decision Board mostly composes upstream confidence and should not be treated as a calibration engine

The important distinction is:

- descriptive confidence = "how strong does this signal look right now"
- calibrated confidence = "how often did signals like this behave as expected in history"

Q60 says we are not fully at the second stage yet.

## Practical gap summary

### Already usable for calibration work

- backend confidence / reliability fields on product, inventory, and supplier surfaces
- action outcome summary with expected vs measured impact
- action impact ledger with snapshot and resolution separation
- executive board as a trust-aware composition layer

### Still blocking trustworthy calibration

- incomplete outcome coverage
- missing or null denominators
- no shared calibration bucket yet on the main recommendation surfaces
- confidence semantics are still spread across several module-specific shapes
- limited ability to prove calibration with stable sample sizes

## Conclusion

The repo is ready for a confidence calibration program in the documentation sense, but not yet for a single canonical calibration engine.

What we can trust today:

- confidence and reliability are already visible on key recommendation surfaces
- outcome learning is already represented through action outcome summary and ledger semantics
- data quality and freshness are already part of the trust story

What we should not claim yet:

- that recommendation confidence is fully outcome-validated across all modules
- that the UI can derive calibration buckets independently
- that small samples are enough to prove model honesty

## Verification

- `git diff --check` - pass

## Next

- Q61 - Pilot operator workflow runbook
