# Product-value execution plan refinement — 2026-10-07

Base: `origin/main=e0502a9d4c6e102c4a13b014c782ee9fba2492a0`

Purpose: replace the incorrect global "fresh import first" serialization with two parallel lanes and register only non-duplicate product work.

## Verified before registration

- Historical imports remain valid for formula/oracle/historical analytics proof; freshness is required for current-state claims, not for mathematical correctness.
- PDC still aggregates nullable Lost Sales / Slow Stock money with `?? 0m`.
- Inventory row UI preserves missing cost, but internal value/ABC/aging/store aggregates still consume zero-valued placeholders for missing money.
- Inventory transfer/rebalance/action workflow already exists end-to-end; a new transfer feature would duplicate it.
- The existing Inventory workflow still anchors its 30-day sales window to wall-clock now and conflates receipt-age evidence with "no movement" wording/rules.
- RQ555's owner choice is resolved by existing RQ571/RQ548/RQ537/RQ539 evidence: Pre-Nivelacija is the canonical retail markdown action source.
- RQ556 can build evidence fields and shadow ranking without owner approval to activate new weights.
- RQ530 already owns the Supplier buying panel and is PARTIAL; the useful missing slice is a selected-supplier negotiation view from already-authoritative Supplier + Inventory facts.
- RQ585 can be implemented on the latest trustworthy horizon if stale/current wording is explicit.
- RQ592 remains correctly freshness-gated because it is prospective current-production outcome proof.

## Routing changes

- READY primary: RQ593 — financial unknown/coverage correctness.
- READY additional/disjoint: RQ595 — Supplier Value / Negotiation Pack v1.
- WAITING after RQ593: RQ555 canonical markdown source and RQ594 Inventory action decision truth.
- WAITING after RQ555: RQ556 shadow-v9 evidence.
- WAITING after RQ593 + RQ594 + RQ555: RQ585 latest-horizon owner digest.
- RQ592 remains WAITING on currentness/live Pre/Post.
- RQ558 remains sample-gated; RQ559 remains source-gated; QDB10 remains later external-onboarding work.

## Documentation policy

No new broad audit was created. Canonical Product Vision, Business Roadmap, Master Roadmap and existing dated audit receive only the minimum correction needed to keep product truth/routing aligned.
