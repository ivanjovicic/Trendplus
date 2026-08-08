# Trendplus Stabilization Status

> [!WARNING]
> **HISTORICAL SNAPSHOT — do not use for current sprint priority.**
> Snapshot date: **2026-07-01**. The RQ/SQL order below reflects the state at that date and is intentionally preserved as evidence. Use `MASTER_ROADMAP.md`, `docs/ai/AGENT_START_HERE.md`, and the current owner queues for present routing and READY status.

Date: 2026-07-01
Repo: `ivanjovicic/Trendplus`
Status: documentation snapshot; not a release certification
Adapted from: MathLearning stabilization-status pattern

## Purpose

This is the central snapshot for Trendplus stabilization: what appears stable, what is partial, what is not closed, and what should be prioritized next.

Use this before starting a stabilization sprint or claiming a feature family is production-safe.

## Executive summary

Trendplus is a retail decision-support product for inventory, sales, supplier and analytics operations. The main risk is not only crashes; it is analytics trust: silent fake zero, fake green, wrong expected impact, unit mismatch, dataScope drift, stale freshness, and export/detail/action payload disagreement.

## Stable / reasonably established

| Area | Status | Evidence docs | Notes |
|---|---|---|---|
| Prompt routing/index | Established | `docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md` | Global lane order exists for analytics reliability prompts. |
| Analytics safety gate | Established | `docs/ai/ANALYTICS_AGENT_SAFETY_GATE.md` | Agents must answer source-of-truth, unit, denominator, unknown/zero and surface parity before coding. |
| Waiting prompt prep | Established | `docs/ai/ANALYTICS_WAITING_PROMPTS_EXECUTION_PREP.md` | Contract-gated prompts have safe prepared defaults or stop rules. |
| Prompt protocol | Established | `docs/ai/PROMPT_QUEUE_PROTOCOL.md` | READY/WAITING/BLOCKED/PARTIAL/DONE model and local lock workflow exist. |
| Inventory forecast row matching | Fixed in prior pass | `Klijent/clientapp/src/components/inventory/DemandForecastPanel.tsx`; related test file | Store-specific forecast row matching was fixed; tests were added but not run in connector session. |
| Pilot intake optional counts | Fixed in prior pass | `Klijent/clientapp/src/components/analytics/PilotDataQualityIntakeReport.tsx` | Optional counters now render `-` instead of fake zero in CSV/export payload. |

## Partial / needs follow-through

| Area | Status | Gap | Next action |
|---|---|---|---|
| Decision Board expected impact | Partial | RQ01/RQ72 still need runtime fixes to prevent lost-sales fallback from becoming expected impact across board surfaces. | Execute RQ01 then RQ72. |
| Percent/share units | Partial | RQ39/RQ40/RQ79 still need implementation to prevent ratio vs percent mismatch across UI/export/report. | Execute RQ39/RQ40, then RQ79. |
| Inventory risk sorting/export | Partial | RQ57/RQ58 have a prepared page-local contract but runtime fix not applied yet. | Add page-local risk-sort label and make screen CSV use displayed rows. |
| Inventory null evidence | Partial | RQ64 still needs backend DTO/handler contract so null evidence does not become 0/info/false. | Execute RQ64, split if needed by handler. |
| Action outcome evidence | Partial | RQ81/RQ86 still need fake-measured and qualitative-outcome fixes. | Execute RQ81 first. |
| dataScope/store lineage | Partial | RQ05/Q81 requires matrix before runtime endpoint fixes. | Produce dataScope matrix. |
| SQL trust semantics | Partial | Q69 is still the SQL audit/test foundation. | Execute Q69 in SQL-only lane. |

## Not closed / do not claim production-safe yet

| Area | Reason |
|---|---|
| Full analytics reliability | Many RQ/Q prompts remain WAITING; wrong impact, unit, denominator and fake-confidence classes are not all fixed. |
| SQL semantic safety | SQL prompt queue still requires Q69 before runtime SQL fixes. |
| Production/live deploy health | Do not claim live smoke from local or connector-only checks. |
| End-to-end export/report parity | Several export/detail/report prompts remain open. |
| Action outcome measurement accuracy | Denominator and evidence contracts are still partial. |

## Next sprint order

Use the global order in `docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md`. Current highest-value sequence:

1. RQ01
2. RQ72
3. RQ39
4. RQ40
5. RQ51/RQ52
6. RQ57/RQ58
7. RQ64
8. RQ81
9. RQ86 staged qualitative-outcome labelling after RQ81
10. Q69 in SQL lane when a separate SQL-only run is available

## Stabilization evidence rules

Do not mark an area stable unless there is:

- a committed change or explicit docs-only status;
- exact validation command or skipped-validation reason;
- residual risk stated;
- follow-up prompt if the area is partial;
- no contradiction with active prompt queues.

## Review cadence

After 3-5 prompt-system commits, apply `docs/ai/PROMPT_BATCH_REVIEW_POLICY.md` before adding more queue/rule docs.

## Current caveat

This snapshot was created from documentation and targeted GitHub inspection. It is not based on a local build/test run. Treat any runtime safety claim as partial until targeted tests or CI evidence are available.
