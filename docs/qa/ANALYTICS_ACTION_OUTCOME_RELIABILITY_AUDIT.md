# Analytics Action/Outcome Reliability Audit

Date: 2026-06-28
Repo: `ivanjovicic/Trendplus`
Status: audit + agent-rule hardening; no runtime action/outcome code changed

## Scope

This pass focuses on the action queue and outcome feedback loop:

- action list filtering and summary alignment
- outcome write semantics
- outcome summary denominator/measurement semantics
- impact ledger visibility
- why repeated analytics bugs appear and how to harden agents

Reviewed files:

- `Klijent/clientapp/src/pages/AnalyticsActionsPage.tsx`
- `Infrastructure/Services/Analytics/AnalyticsActionItemService.cs`
- `Api/Endpoints/AnalyticsActionsEndpoints.cs`
- `Api.Tests/AnalyticsActionItemServiceTests.cs`
- `docs/ai/AGENT_START_HERE.md`

## Documentation fixed in this pass

### G01 - Agent guardrails were too broad for repeated analytics reliability bugs

File changed:

- `docs/ai/AGENT_START_HERE.md`

Added:

- a root-cause explanation for repeated analytics bugs
- ten non-negotiables instead of five
- an explicit `Analytics safety gate` that agents must answer before coding
- new stop rules for fake zero/fake green/fake measured, frontend-invented semantics, and table/detail/export/action drift

Commit:

- `c7775e42a58c1ff6df554ad001daa10d62fdba5a`

## New findings

### RQ81 - `not_measured` outcome can still get an auto measurement timestamp

Files:

- `Infrastructure/Services/Analytics/AnalyticsActionItemService.cs`
- `Klijent/clientapp/src/pages/AnalyticsActionsPage.tsx`

Observed:

- `UpdateOutcomeAsync` sets `item.OutcomeMeasuredAtUtc = request.OutcomeMeasuredAtUtc ?? now` before only clearing pending outcomes.
- Pending outcomes clear measured impact/date, but `not_measured` does not.
- Frontend disables measured amount/date only for pending, not for `not_measured`.

Risk:

- An action explicitly marked `not_measured` can still receive a measurement timestamp.
- Measured-date filters can then include a row whose outcome says it was not measured.
- This is a fake-measured bug.

Classification: confirmed outcome evidence bug.

Recommended prompt: RQ81.

### RQ82 - Outcome summary counts open actions with outcome status as measured outcomes

File:

- `AnalyticsActionItemService.cs`

Observed:

- `BuildSummaryAggregate` defines `measuredItems` as all items where normalized outcome status is not pending.
- This can include open actions if an outcome is entered before the action status is closed.
- Existing tests intentionally include an open measured action in totals.

Risk:

- The action outcome summary may mix lifecycle states: open action feedback vs closed action outcome.
- Positive/negative outcome rates can include actions that are not actually closed.

Classification: contract gap / likely denominator confusion.

Recommended prompt: RQ82.

### RQ83 - Outcome coverage denominator uses closed actions, but measured count and positive/negative rates use all non-pending actions

File:

- `AnalyticsActionItemService.cs`

Observed:

- `outcomeCoverageRate = closedMeasuredCount / closedCount`.
- `positiveOutcomeRate = successCount / measuredCount`.
- `negativeOutcomeRate = negativeCount / measuredCount`.
- `measuredCount` comes from all non-pending actions, not only closed actions.

Risk:

- Coverage and outcome-rate metrics use different universes in the same summary.
- User may compare them as if they share one denominator.

Classification: denominator mismatch.

Recommended prompt: RQ83.

### RQ84 - Measured impact and realization ratio use only rows with measured impact, but measured count includes rows without measured impact

File:

- `AnalyticsActionItemService.cs`

Observed:

- `measuredCount` includes all non-pending outcomes.
- `measuredImpactRsd` sums only items with `MeasuredImpactRsd`.
- `expectedImpactRsd` sums only those same measured-impact rows where expected impact exists.

Risk:

- Outcome count and money impact sample are different samples.
- Realization ratio can look valid while excluding some measured outcomes without clear user-facing denominator.

Classification: sample/denominator mismatch.

Recommended prompt: RQ84.

### RQ85 - Outcome summary default date window uses created date, not resolved/measured date

Files:

- `AnalyticsActionsEndpoints.cs`
- `AnalyticsActionsPage.tsx`

Observed:

- If no date filters are supplied, endpoint defaults to `createdFrom = now - 90d` and `createdTo = now`.
- UI summary calls this `poslednjih 90 dana created` via period mode.
- This is documented in UI, but users may expect action outcomes to be resolved/measured in last 90 days.

Risk:

- Old actions created outside 90d but resolved/measured recently are excluded from the outcome feedback loop by default.
- Recent outcomes can be missed.

Classification: product contract risk / likely surprising default.

Recommended prompt: RQ85.

### RQ86 - Outcome update can save success/neutral/negative without measured impact or evidence

Files:

- `AnalyticsActionsPage.tsx`
- `AnalyticsActionItemService.cs`

Observed:

- Frontend allows non-pending statuses with empty measured amount/date/evidence.
- Backend accepts non-pending outcome with `MeasuredImpactRsd = null` and auto date.

Risk:

- A row can become `success`, `neutral`, or `negative` without measurement evidence.
- Summary warnings exist, but status itself can look more authoritative than the evidence supports.

Classification: fake-outcome-authority risk.

Recommended prompt: RQ86.

### RQ87 - Impact ledger resolution snapshot omits key measured outcome fields

File:

- `AnalyticsActionItemService.cs`

Observed:

- `BuildResolutionSnapshot` stores optional measured window/evidence/reference/resolution note.
- Outcome status, measured impact and outcome measured date are stored on the action item, but not inside the resolution snapshot.

Risk:

- Ledger snapshot is not self-contained for historical outcome reconstruction.
- Future export/report consumers can read the ledger and miss the actual measured status/value/date unless they also read the action row.

Classification: ledger contract gap.

Recommended prompt: RQ87.

### RQ88 - Action list KPI `Zatvoreno` merges done and rejected without visible split

File:

- `AnalyticsActionsPage.tsx`

Observed:

- KPI bar displays `Zatvoreno = counts.done + counts.rejected`.
- The page later has status filters, but the top KPI itself does not show the split.

Risk:

- Rejected actions can look like completed actions at a glance.
- This is not a calculation bug if intended, but it can mislead operational interpretation.

Classification: UX/count semantics gap.

Recommended prompt: RQ88.

## Recommended order

1. RQ81 - `not_measured` must not auto-fill measurement timestamp.
2. RQ86 - non-pending authoritative outcome must require evidence or be clearly `not_measured`/pending.
3. RQ82/RQ83 - align action lifecycle denominator with outcome-rate denominator.
4. RQ84 - separate measured count from measured-impact sample.
5. RQ85 - decide default outcome window semantics.
6. RQ87 - make resolution snapshot self-contained.
7. RQ88 - split done vs rejected KPI or label it clearly.

## Why there are many bugs

This is expected when a project evolves from dashboards into a decision-support system without first freezing semantic contracts. The code contains several generations of analytics:

- early dashboard summaries
- product/supplier/inventory decision modules
- action queue and outcome ledger
- durable reports and export layer
- fallback frontend-derived data

Each generation added helpful features, but some reused old numeric fields for new meanings. The most common source of wrong results is not a crash; it is a silent semantic conversion: unknown to zero, revenue to impact, ratio to percent, returned count to total, or not-measured to measured.

The mitigation is to force every agent to state contract/unit/denominator/evidence before coding, then test true-zero and unknown separately.
