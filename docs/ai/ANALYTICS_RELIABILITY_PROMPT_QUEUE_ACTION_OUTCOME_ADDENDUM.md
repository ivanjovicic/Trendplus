# Analytics Reliability Prompt Queue - Action/Outcome Addendum

Date: 2026-06-28
Repo: `ivanjovicic/Trendplus`
Current READY prompt: none in this addendum
Main queue READY prompt: `RQ01` in `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`

Use with:

- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_HARDENING_ADDENDUM.md`
- `docs/qa/ANALYTICS_ACTION_OUTCOME_RELIABILITY_AUDIT.md`

Purpose: queue follow-up fixes for action/outcome semantics, measurement evidence, denominator contracts and impact ledger completeness.

## Status summary

| Task | Status | Feature family | Purpose |
|---|---|---|---|
| RQ81 | WAITING | action-outcome-not-measured-date | Prevent `not_measured` from getting fake measured timestamp |
| RQ82 | WAITING | action-outcome-open-vs-closed-denominator | Decide whether open actions can count as measured outcomes |
| RQ83 | WAITING | action-outcome-rate-denominators | Align outcome coverage and rate denominator semantics |
| RQ84 | WAITING | action-outcome-impact-sample | Separate measured outcome count from measured-impact sample |
| RQ85 | WAITING | action-outcome-default-window | Decide default created/resolved/measured window semantics |
| RQ86 | WAITING | action-outcome-evidence-requirements | Prevent success/neutral/negative without evidence looking authoritative |
| RQ87 | WAITING | action-outcome-resolution-ledger | Make outcome resolution snapshot self-contained |
| RQ88 | WAITING | action-count-closed-kpi-split | Split or relabel done/rejected closed KPI |
| RQ90 | WAITING | analytics-actions-list-contract | Preserve canonical filters, search and priority ordering in action lists |

---

## RQ81 - `not_measured` must not create a measured timestamp

Status: WAITING
Ready after: RQ21 or explicit reprioritization
Priority: P0
Type: backend/frontend/tests
Feature family: action-outcome-not-measured-date
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ81-<agent>.lock.md`
Commit suggestion: `fix(actions): avoid fake measured date for not measured outcomes`

### Why

A `not_measured` outcome explicitly means measurement evidence is not available. It must not receive an automatic measurement timestamp.

### Evidence already found

- Backend `UpdateOutcomeAsync` assigns `OutcomeMeasuredAtUtc = request.OutcomeMeasuredAtUtc ?? now` and only clears pending.
- Frontend disables measured amount/date only for pending, not for `not_measured`.

### Contract

- `pending`: no measured impact, no measured date.
- `not_measured`: no measured impact, no measured date unless a separate resolution timestamp is introduced.
- `success`/`neutral`/`negative`: may have measured date only when measurement/evidence exists, per RQ86.

### Scope only

- `AnalyticsActionItemService.cs`
- `AnalyticsActionsPage.tsx`
- `Api.Tests/AnalyticsActionItemServiceTests.cs`
- frontend tests if available

### Test matrix

- pending clears impact/date.
- not_measured clears impact/date.
- success with explicit measured date preserves it.
- measured-date filter does not include not_measured rows with no date.

### Acceptance

- `not_measured` cannot appear in measured-date filtered data solely because backend auto-filled `now`.

---

## RQ82 - Open actions vs measured outcomes denominator contract

Status: WAITING
Ready after: RQ81 or explicit unblocking
Priority: P1
Type: backend-contract/tests
Feature family: action-outcome-open-vs-closed-denominator
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ82-<agent>.lock.md`
Commit suggestion: `fix(actions): clarify open action outcome denominator`

### Why

Outcome summary currently counts any non-pending outcome as measured, even if the action is still open.

### Contract decision required

Choose one:

- closed-only outcome summary: only done/rejected actions count in outcome rates, or
- lifecycle-aware summary: expose openMeasuredCount separately and label it as early feedback.

### Scope only

- `AnalyticsActionItemService.cs`
- action outcome DTOs/types if adding fields
- `AnalyticsActionsPage.tsx` labels/tests

### Test matrix

- open action with success outcome.
- closed action with success outcome.
- closed pending action.
- rates and labels prove whether open outcomes are included or separated.

### Acceptance

- Outcome summary cannot mix open and closed action outcomes without visible denominator metadata.

---

## RQ83 - Outcome rate denominator alignment

Status: WAITING
Ready after: RQ82 or explicit unblocking
Priority: P1
Type: backend-contract/tests
Feature family: action-outcome-rate-denominators
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ83-<agent>.lock.md`
Commit suggestion: `fix(actions): align outcome rate denominators`

### Why

Coverage uses closed actions as denominator, but positive/negative rates use all non-pending actions. These are different universes.

### Contract

- Every rate must expose numerator and denominator meaning.
- If different denominators remain, DTO and UI must label them explicitly.
- Prefer fields such as `closedOutcomeCoverageRate`, `measuredOutcomeRate`, `closedPositiveOutcomeRate`.

### Acceptance

- Users can tell which action universe each rate uses.

---

## RQ84 - Measured impact sample vs measured outcome sample

Status: WAITING
Ready after: RQ83 or explicit unblocking
Priority: P1
Type: backend/frontend/tests
Feature family: action-outcome-impact-sample
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ84-<agent>.lock.md`
Commit suggestion: `fix(actions): expose measured impact sample size`

### Why

`measuredCount` can include non-pending outcomes without `MeasuredImpactRsd`, while `MeasuredImpactRsd` and realization ratio use only rows with measured impact. The samples differ.

### Contract

- Keep `measuredOutcomeCount` separate from `measuredImpactSampleCount`.
- UI must show when realization ratio is based on a subset.
- Warnings must be visible near money KPIs, not only in meta.

### Acceptance

- Realization ratio cannot be interpreted as covering all measured outcomes when it only covers rows with measured amount.

---

## RQ85 - Outcome summary default period semantics

Status: WAITING
Ready after: RQ82/RQ83 or explicit unblocking
Priority: P2
Type: product-contract/backend/frontend/tests
Feature family: action-outcome-default-window
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ85-<agent>.lock.md`
Commit suggestion: `docs(actions): clarify outcome summary default window`

### Why

Default outcome summary window is based on created date. Recent measured/resolved outcomes for older actions are excluded.

### Contract decision required

Choose one:

- default created-window summary remains, but UI calls it action creation cohort; or
- default resolved-window summary; or
- default measured-window summary; or
- multi-window tabs.

### Acceptance

- The default action outcome summary cannot be mistaken for “outcomes measured in the last 90 days” if it is actually “actions created in the last 90 days”.

---

## RQ86 - Evidence requirements for authoritative outcome statuses

Status: WAITING
Ready after: RQ81 or explicit unblocking
Priority: P0
Type: backend/frontend-contract/tests
Feature family: action-outcome-evidence-requirements
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ86-<agent>.lock.md`
Commit suggestion: `fix(actions): require evidence for authoritative outcomes`

### Why

The UI/backend can save `success`, `neutral`, or `negative` without measured impact, evidence source or measured date. The status can look authoritative while evidence is weak.

### Contract

Choose one:

- require measured impact/date/evidence for success/neutral/negative, or
- allow qualitative outcomes but label them separately as `qualitativeOutcomeStatus`, not measured outcome.

### Test matrix

- success with evidence is accepted.
- success without evidence is blocked or labelled qualitative.
- not_measured remains allowed without measured impact/date.
- summary separates qualitative from measured outcomes.

### Acceptance

- Outcome status cannot look measured/authoritative without either evidence or explicit qualitative labeling.

---

## RQ87 - Self-contained resolution ledger snapshot

Status: WAITING
Ready after: RQ86 or explicit unblocking
Priority: P1
Type: backend-contract/tests
Feature family: action-outcome-resolution-ledger
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ87-<agent>.lock.md`
Commit suggestion: `fix(actions): persist self-contained outcome ledger snapshot`

### Why

Resolution snapshot stores evidence metadata but not the actual outcome status, measured impact or measured date. Future consumers reading ledger metadata alone cannot reconstruct the outcome.

### Contract

Resolution snapshot should include enough immutable fields to reconstruct the outcome at save time:

- outcomeStatus
- measuredImpactRsd, nullable
- outcomeMeasuredAtUtc, nullable
- measuredWindowDays, nullable
- evidenceSource/reference
- resolutionNote

### Acceptance

- Ledger snapshot is self-contained for historical outcome report/export use.

---

## RQ88 - Closed KPI done/rejected split

Status: WAITING
Ready after: RQ82 or explicit unblocking
Priority: P2
Type: frontend-ux/tests
Feature family: action-count-closed-kpi-split
Parallel-safe: yes
Owner: unassigned
Local lock: `.ai/task-locks/RQ88-<agent>.lock.md`
Commit suggestion: `fix(actions): split closed action KPI`

### Why

KPI bar shows `Zatvoreno = done + rejected`. This is not wrong mathematically, but it can hide how many actions were completed vs rejected.

### Contract

- Either split `Završeno` and `Odbijeno`, or label combined card as `Zatvoreno (završeno + odbijeno)` with split details.

### Acceptance

- Rejected actions cannot be mistaken for completed actions in the top KPI bar.

---

## RQ90 - Analytics actions list contract

Status: WAITING
Ready after: STAB09 or explicit reprioritization
Priority: P1
Type: backend/tests
Feature family: analytics-actions-list-contract
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ90-<agent>.lock.md`
Commit suggestion: `fix(actions): preserve analytics actions list filters`

### Why

`AnalyticsActionsCriticalWorkflowTests.List_AppliesCanonicalFiltersSearchPagingAndPriorityOrdering` expected `totalCount=2` but the canonical list returned `0`. That is a list-contract failure, not just a display issue, because the seed data disappears before paging and ordering can be validated.

### Evidence already found

- `Api.Tests/AnalyticsActionsCriticalWorkflowTests.cs` seeds the in-memory analytics context and exercises the canonical list path.
- The failing test expected the filtered set to include two rows after canonical filters, search and priority ordering were applied.
- The route is implemented in `Api/Endpoints/AnalyticsActionsEndpoints.cs`.
- The list/service contract is implemented in `Infrastructure/Services/Analytics/AnalyticsActionItemService.cs`.

### Contract

- Canonical list filters must preserve seeded rows.
- `dataQualityStatus=warning` must still include legacy `fair` rows via normalization.
- `totalCount` must reflect the filtered set before paging.
- Priority ordering and pagination must remain deterministic.
- Invalid filters must still return `400`.

### Scope only

- `Api/Endpoints/AnalyticsActionsEndpoints.cs`
- `Infrastructure/Services/Analytics/AnalyticsActionItemService.cs`
- `Api.Tests/AnalyticsActionsCriticalWorkflowTests.cs`
- related endpoint tests only if needed

### Do not touch

- action-outcome evidence semantics
- resolution ledger shape
- unrelated inventory list routes

### Test matrix

- accepted product rows with `warning` and legacy `fair` data quality are both included
- search term matches seeded product rows
- `pageSize=1` returns one row but `totalCount` stays 2
- invalid filters still return `400`
- counts endpoint continues to reflect seed totals
- priority ordering remains deterministic

### Acceptance

- Analytics actions list regression no longer collapses the canonical filtered set to empty.
- Priority/search/filter behavior stays visible and testable.
