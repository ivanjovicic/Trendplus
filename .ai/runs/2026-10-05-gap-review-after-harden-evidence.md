Task ID: gap-review-after-harden-2026-10-05
Queue: direct-user-request (review product commits from 8a784df not adequately covered by review-harden)
Date: 2026-10-05
Agent/tool: Grok Bot (executor, box worktree `/workspace/rh`)
Delivery target: main
Base: origin/main `6c585f388d703d12c2c5d792108415a447afe24f`
Main commit SHA: pending

## Previously reviewed in harden pass (`2043f9dd`)
Deep-checked / fixed: RQ581, RQ590, RQ591 (reorder null-revenue), RQ576 dead helpers; hunches on RQ587, RQ479, RQ583, RQ584.
Claimed bulk coverage: RQ569–RQ584, RQ587, RQ479, RQ48, RQ88, RQ552/553, RQ589–RQ591.

## Newly reviewed in this pass (acceptance spot-check of under-reviewed product lanes)
RQ569/570 horizon + BeyondSourceHorizonPolicy, RQ571 Pre-Nivelacija anchor (RecommendationAllowed requires complete evidence incl. receipt), RQ572/577 basket grain gating, RQ573/574 Product Decision category/FIX_DATA ranking, RQ575 dimension coverage, RQ578/579/580 DQ/integrity/supplier labels, RQ582 quarantine, RQ48/RQ88 actions, RQ79/RQ80 executive+intake, RQ552/553 nivelacija, RQ589 V2 margin-alerts.

## Gaps found
1. RQ589/RQ591 residual: V2 margin-alerts FE MiniStat said "Niska marža (<10%)" while backend LOW_MARGIN cutoff is 15.
2. RQ589 residual: V2 margin-alerts still coerced missing nabavnaCena/prodajnaCena to 0.

## Fixes
- Shared FE `LOW_MARGIN_ALERT_THRESHOLD_PCT` + `lowMarginAlertStatLabel()`; BE `LowMarginAlertThresholdPct`.
- Nullable nabavnaCena/prodajnaCena on margin-alerts + FE types.
- Queue addenda on DONE RQ589/RQ591.

## Validation
- vitest insightStudioTrustPresentation.spec.ts (pending run)
- No full suite.

## Not pushed as invented work
Lanes above otherwise matched acceptance; residual UNPROVEN-RUNTIME only for live Render probes.
