# Post-RQ567 analytics next-wave audit — 2026-10-04

Task: direct owner request after RQ567
Repo: ivanjovicic/Trendplus
Starting main: cf0ebd658562ce09e0b57c83e44accf6fc872915
Delivery target: main
Evidence state: synchronized

## Executive finding

RQ567 is correctly complete in repository-local scope, but its final statement that no RQ prompt is READY was stale by the time of closure.

The dependency graph advanced materially on 2026-10-04:
- RQ541 DONE
- RQ542 DONE
- RQ543 DONE
- RQ544 DONE
- RQ547 DONE
- RQ548 DONE
- RQ549 DONE
- RQ550 DONE
- RQ551 DONE
- RQ568 DONE
- RQ545 remains PARTIAL only for deployed/provider acceptance; its code-side diagnostics are delivered.

Therefore RQ564's own dependency rule is satisfied for repository-local work. Production/provider access is explicitly not a start gate for RQ564.

Fresh checks also found no open PR, RQ564/RQ553/RQ552 task-lock file or matching active branch owner.

## Routing changes

### RQ564 — promoted to READY (primary P0)

Add the dedicated Nivelacija runtime-integrity family now that event direction, maturity, overlap, equal-window split, DiD/control semantics, normalization, independent oracles and canonical Testcontainers Pre/Post view are all delivered.

Important extra acceptance added by this audit:
- a successful price-event write, live repair, import generation or relevant cache generation change must make previous Nivelacija integrity evidence non-current before any new green state;
- a bounded reprobe may run asynchronously, but stale evidence must remain non-current until it succeeds.

### RQ553 — promoted to READY (parallel-safe P3)

RQ529 is DONE and no collision exists. This is the remaining Nivelacija Serbian copy/i18n/accessibility cleanup and can run beside backend/oracle RQ564.

### RQ552 — scope repaired, sequenced after RQ564

The 2026-10-01 RQ552 text was partly obsolete:
- RQ540 already fixes transient Pre-Nivelacija failure caching and cancellation behavior.
- RQ542 owns event-bounded DiD/control semantics.
- RQ474 owns safe error classification.

Current residual:
- measure current-main Pre/Post query/request cost after the major semantic rewrites;
- reduce the duplicate current/previous request only if measurement proves it material;
- invalidate Pre/Post and Pre-Nivelacija caches immediately after POST /api/nivelacija and live repair;
- invalidate/re-establish the RQ564 Nivelacija integrity generation consistently;
- prove mutation -> next read freshness without waiting for TTL.

RQ552 stays WAITING only to avoid split ownership of mutation/integrity invalidation while RQ564 is being added.

## New P0 gap: RQ569 — source horizon and durable freshness

Live verification in .ai/runs/2026-10-04-daily-sales-live-contract-evidence.md requested 2026-05-05 through 2026-12-12 and got the correct requested range, 222 rows and 1,262 sold items, but the latest non-zero sales date was 2026-08-05.

This is not proof of stale data by itself: a store may legitimately have no sales after that date. The reliability defect is that requested/effective period, observed business-data horizon and durable import/checkpoint freshness are not one canonical Operations trust contract.

RQ569 was registered to:
- populate authoritative observedPeriodFromUtc/observedPeriodToUtc where facts prove them;
- derive freshness from durable import/sync/refresh evidence, never from cache-generation time or last non-zero sale alone;
- report missing source watermark as unknown, never fresh;
- render requested/effective period separately from observed data horizon using the existing RQ567 trust surface;
- bind observed/freshness evidence to store/dataScope context and source generation;
- reuse existing readiness/freshness policy without inventing a threshold.

RQ569 waits for RQ564 so Nivelacija and other Operations families share one context/generation lifecycle.

## Certification CI repair: RQ453

The existing .github/workflows/analytics-tests.yml already has a dedicated pgvector PostgreSQL certification job, but it remains named and framed as RQ447 Supplier/Shoe Type while it now executes OperationsAnalyticsAllRoutesIntegrationTests. RQ568 has expanded that route proof to:
- 6/6 current Operations routes
- 31/31 cases
- zero skipped
- Pre/Post VERIFIED

RQ453 was scope-repaired, not duplicated:
- generalize the existing job instead of creating another PostgreSQL harness;
- consume RQ561/RQ568 route proof;
- include RQ567 frontend trust-state proof;
- after RQ564/RQ569 include Nivelacija/freshness family contract proof;
- emit one machine-readable expected/executed/passed/skipped manifest;
- fail on skipped required tests, missing manifest, Pre/Post not VERIFIED, missing required family proof or trust regression.

Deployed browser/export remains RQ565 and is not a repository-CI start gate.

## RQ451 de-duplication

RQ567 now owns the generic visible readiness/integrity status, context matching, checked time and evidence id on all six Operations screens.

RQ451 must not reimplement that UI. Its residual is only Supplier/Shoe-specific drill-down proof that depends on RQ448 browser reconciliation: line counts, revenue/quantity deltas, attribution/unknown coverage, cost coverage and legacy Supplier browser proof.

## What is still genuinely externally gated

- RQ565: exact deployed API + authenticated browser + export reconciliation for all six current Operations screens.
- RQ566: exact deployed SHA, negative-ID inventory/repair authorization, worker/checkpoint and Europe/Belgrade timezone verification.
- RQ454/STAB16: approved read-only production/provider proof.
- RQ545 residual: deployed Pre/Post acceptance.

The currently deployed API returning null RQ567 trust metadata is therefore a deployment fact, not a repository-local implementation blocker.

## Further product/value lanes

Existing prompts remain useful after core reliability:
- RQ555: choose one canonical source for "treba sniziti sada" across Products, Actions and Prioriteti. Recommended owner choice: Pre-Nivelacija after its reliability family is live, because it is store-grain and markdown-specific; do not silently make this policy decision in code.
- RQ556: v9 priority model (weeks of cover, age, season end) remains owner-weight gated.
- RQ557: markdown outcome ledger is dependency-complete on the semantic/oracle side and is the strongest next business-value enhancement after core reliability/performance. Keep it descriptive ("ishod"), not causal.
- RQ558/RQ559 remain later because controlled-effect/size-run evidence has stronger source/policy requirements.

## Validation boundary

This audit changed documentation/queue routing only. It did not modify product runtime code or deploy services.

The GitHub connector cannot execute local Node/.NET validators. The next implementation agent must run:
- node scripts/check-prompt-queues.mjs
- node scripts/check-planning-architecture.mjs
- node scripts/check-agent-instructions.mjs
- git diff --check

before claiming queue/governance closure.
