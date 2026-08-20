# Controlled Markdown / Replenishment Scenario Planning Contract

Date: 2026-08-20

This document freezes the docs-only precursor for `RQ107`.

## Purpose

Define a controlled scenario-planning contract for markdown and replenishment what-if analysis without inventing runtime outcomes or forecast certainty.

## Allowed scenario vocabulary

- no-change
- fixed markdown bands
- replenishment bands
- conservative / base / aggressive planning variants

## Comparison basis

- compare against measured historical behavior, not invented forecast certainty
- keep missing measured windows unavailable, never as `0`
- preserve no-fake-zero behavior for unavailable impact
- use the existing forecast provenance and backtest contracts as the runtime follow-up gate

## Disallowed in this docs-only precursor

- simulator UI
- optimizer behavior
- runtime forecast mutation
- automatic write-back
- LLM-generated scenario outcomes

## Evidence

- `docs/qa/RETAIL_ANALYTICS_COMPETITIVE_GAP_AUDIT_2026-08-12.md`
- `docs/qa/FORECAST_BASELINE_BACKTEST_CONTRACT_2026-08-20.md`
- `docs/qa/FORECAST_SNAPSHOT_PROVENANCE_CONTRACT_2026-08-20.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`

## Acceptance

- one citeable scenario-planning contract exists on main
- queue and roadmap status are synchronized
- runtime scenario work stays gated on trusted forecast materialization plus a measured backtest window
