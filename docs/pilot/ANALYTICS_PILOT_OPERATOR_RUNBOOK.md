# Analytics Pilot Operator Runbook

Updated: 2026-06-21

## Purpose

This runbook makes the Trendplus analytics pilot repeatable for:

- internal operators
- customer pilot operators
- sales/demo operators who need evidence instead of ad hoc screenshots

The operating rule is simple:

signal -> recommendation -> confidence -> action -> measured outcome -> learning

Operators should not treat the current pilot as an autopilot system.
They should treat it as a decision-support workflow with explicit warnings, freshness checks, and evidence capture.

## Current Pilot Reality

The current analytics pilot is usable, but it is intentionally conservative.

Known truths from the evidence docs:

- live smoke has passed for the required backend and frontend pilot surfaces
- protected action writes are enforced and should stay enforced
- no-fake-zero and no-fake-green rules are active
- markdown and replenishment flows still contain MVP/signal-only areas
- cache/freshness warnings must stay visible

Operators must not:

- hide stale or partial warnings
- treat `insufficient_data` as urgent
- record missing measured impact as `0 RSD`
- treat experimental signals as guaranteed profit

## Scope of This Runbook

This runbook covers:

- daily opening checks
- action queue review
- pilot decision review cadence
- supplier negotiation pack usage
- OOS/replenishment usage
- markdown signal usage
- data quality review
- escalation rules
- evidence capture

## Roles

### Pilot operator

Responsible for:

- opening checks
- daily review of alerts, actions, and data quality
- recording evidence
- escalating unclear or unsafe conditions

### Business reviewer

Responsible for:

- weekly review of high-impact decisions
- confirming whether queued actions were actually executed
- validating whether measured outcomes match business reality

### Technical owner

Responsible for:

- deploy health
- refresh/worker visibility
- cache/freshness issues
- route/API regressions
- queue/task follow-up when the pilot hits a repeated product limitation

## Daily Opening Checklist

Run this at the start of each pilot day before making decisions from the system.

### 1. Confirm pilot surface availability

Open and confirm these surfaces load:

- `/analytics`
- `/analytics/pilot-readiness`
- `/analytics/products`
- `/analytics/actions`
- `/analytics/decision-board`

If any required route fails to render, escalate before using analytics for the day.

### 2. Confirm trust state, not just page load

Check for:

- freshness state
- data quality status
- warning banners
- partial/fallback notices

Do not proceed as if the surface is healthy when:

- freshness is stale or critical without explanation
- the page shows partial/fallback state for key metrics
- expected impact is broadly unavailable for the decisions you need

### 3. Confirm action workflow availability

Open `/analytics/actions` and verify:

- action list loads
- outcome summary loads or fails honestly
- pending outcomes do not show as success
- no rows show impossible `0 RSD` placeholders for missing measured impact

### 4. Confirm today's decision scope

Before reviewing recommendations, decide which of these you are using today:

- product decisions
- inventory / OOS / replenishment
- supplier discussion prep
- markdown / pre-nivelacija signal review

This avoids mixing exploratory browsing with operational use.

### 5. Record opening status

Capture one short daily operator note with:

- date/time
- operator name
- surfaces checked
- trust state summary
- any warnings already present

Recommended format:

```text
Date:
Operator:
Surfaces checked:
Warnings present:
Safe for normal pilot review: yes/no
```

## Daily Action Queue Review

Open `/analytics/actions` and review in this order.

### 1. P1 open actions

Review all:

- `P1`
- `new`
- `accepted`
- `deferred`

For each row, confirm:

- source is understood
- recommendation reason is readable
- trust/data quality is acceptable
- expected impact is either known or honestly unavailable

Do not prioritize an action only because the number is large.
Trust state and freshness still win over raw impact.

### 2. Outcome hygiene

Review rows with:

- `pending`
- `not_measured`
- missing measured impact

Operator rule:

- `pending` is not failure
- missing measured impact is not `0 RSD`
- `not_measured` means evidence is missing, not that the action failed

If an action was executed in reality but the outcome is still blank, record that as an evidence gap for weekly review.

### 3. Protected write behavior

If you do not have permission to edit actions:

- continue using the pages read-only
- do not invent side notes in screenshots only
- log the blocked write attempt as an access/workflow note if the pilot depends on it

### 4. Daily queue outcome

Classify the day's queue state as:

- clear
- needs business review
- blocked by data quality
- blocked by access/workflow

## Product Decision Review

Use `/analytics/products` for product-level decisions.

### Safe-use rules

- treat `insufficient_data` as a stop or verification signal
- read the reason and warning context before acting
- use expected impact only when it is actually present
- use freshness and reliability together with confidence

### Daily operator flow

1. Review highest-confidence, highest-impact product recommendations.
2. Skip rows whose warnings make the recommendation non-final.
3. Queue or record only actions that have:
   - understandable reason
   - acceptable freshness
   - acceptable data quality
4. Flag rows where the recommendation looks operationally important but trust is weak.

## Supplier Negotiation Pack Usage

Use supplier decision/report surfaces as meeting prep, not as blind execution.

### Safe-use rules

- fallback warning must stay visible
- blocked final advice must stay blocked
- missing cost or missing support must remain visible
- copied meeting text is a briefing artifact, not final proof

### Suggested workflow

1. Open the supplier pack before the meeting.
2. Confirm whether the recommendation is allowed or only signal-level.
3. Capture:
   - supplier name
   - reason summary
   - visible warnings
   - whether final advice is blocked
4. After the meeting, log one short outcome note:
   - agreement reached
   - no agreement
   - more data needed

## OOS / Replenishment Usage

Use inventory decisions conservatively.

### Safe-use rules

- estimated lost sales must stay labeled as estimate
- missing baseline must block fake replenish certainty
- stale inventory trust must remain visible
- `SIGNAL_REVIEW` rows are not final replenish instructions

### Daily operator flow

1. Review high-priority replenish/OOS signals.
2. Check whether the baseline row actually exists and is trustworthy.
3. If baseline/trust is weak:
   - do not treat the action as urgent final instruction
   - record it as verification-required
4. Escalate if repeated high-priority items are blocked by missing stock baseline.

## Markdown Signal Usage

Treat current markdown-related outputs as decision support, not as guaranteed optimizer output.

### Safe-use rules

- use words like signal, scenario, estimate
- do not promise guaranteed margin improvement
- if cost or support is missing, expected impact stays unavailable
- `insufficient_data` must not be promoted into urgent markdown action

### Weekly usage pattern

Use markdown review in a scheduled decision meeting, not ad hoc during every daily opening.

Recommended weekly questions:

- which markdown candidates have adequate support?
- which candidates are still only scenario-level?
- which missing inputs repeatedly block trust?

## Data Quality Review

Review `/analytics/pilot-readiness` and any visible quality/trust headers at least once daily.

### Check for

- stale or unknown freshness
- partial/fallback responses
- correlation between warnings and affected surfaces
- repeated missing cost / sparse sales / missing supplier issues

### Operator action

- if warning is isolated and understood, proceed with caution
- if warning is broad and cross-surface, downgrade pilot confidence for the day
- if warning blocks a core operating surface, escalate before making business decisions

## Weekly Decision Review

Run one weekly review meeting with operator + business reviewer.

### Agenda

1. Review actions created that week.
2. Review actions executed in real life.
3. Review which actions still have pending or missing outcomes.
4. Compare expected vs measured outcome where evidence exists.
5. Review repeated warning patterns:
   - stale freshness
   - missing cost
   - sparse sales
   - blocked writes
6. Review whether the system produced helpful recommendations or mostly verification noise.

### Weekly outputs

Record:

- top 5 accepted decisions
- top 5 blocked decisions
- biggest evidence gap
- biggest trust/regression concern
- follow-up queue/doc candidate if the same limitation repeated

## Escalation Rules

Escalate immediately when any of these happens.

### P0 technical escalation

- required analytics route does not load
- core backend/API route fails
- generic shell or blank page returns instead of real content
- action queue cannot load at all

### P1 trust escalation

- stale/partial/error state appears healthy
- missing impact looks like `0 RSD`
- insufficient-data recommendation appears high confidence or urgent
- outcome UI implies success without evidence

### P1 workflow escalation

- repeated access blocks stop pilot execution
- operators cannot record action outcomes for real executed actions
- supplier/OOS/markdown decisions repeatedly require out-of-band notes because the system cannot represent the evidence cleanly

### P2 business escalation

- recommendation reason is understandable but consistently not actionable
- multiple high-confidence suggestions are rejected by business reality
- measured outcomes repeatedly diverge from expected impact

## Evidence Capture Rules

Every pilot day should leave behind small, structured evidence.

### Minimum daily evidence

- opening status note
- list of reviewed high-priority actions
- list of actions executed or deferred
- any visible trust/data-quality blocker

### Minimum weekly evidence

- expected vs measured examples
- blocked/uncertain examples
- repeated warning pattern summary
- recommendation for next product/process improvement

### Safe evidence rules

- do not record secrets or admin credentials
- do not treat screenshots alone as the source of truth when a route/API note can be recorded
- do not hide warnings in customer summaries

## Recommended Daily Template

```text
Date:
Operator:

Opening check:
- routes OK: yes/no
- trust warnings present:
- ready for normal pilot use: yes/no

Actions reviewed:
- P1 items:
- accepted:
- deferred:
- blocked:

Trust/data issues:
- stale/fallback/partial:
- missing impact:
- access/workflow blockers:

Evidence captured:
- screenshots/links/notes:
```

## Recommended Weekly Template

```text
Week:
Operator:
Business reviewer:

Summary:
- actions created:
- actions executed:
- outcomes measured:
- unresolved pending outcomes:

Top wins:
- ...

Top blockers:
- ...

Trust concerns:
- ...

Requested follow-up:
- docs/process
- product/queue item
- data refresh/investigation
```

## Final Operator Rule

If the system is warning you, believe the warning.

Trendplus analytics is pilot-ready because it stays honest about uncertainty.
The operator workflow must preserve that honesty instead of smoothing it over.
