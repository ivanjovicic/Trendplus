# Owner-delegated product decisions — Decision Pulse and Operations filters

Date: 2026-10-08
Repository: `ivanjovicic/Trendplus`
Authority: the user requested “Odblokiraj, odluči šta treba” in the current conversation, delegating the **product-contract choice**. This document does not grant authority to mutate production, override tenant security, fabricate freshness/causal evidence, or take over another active agent's ownership.

## Decision A — RQ481: Decision Pulse follows shared applied filters (APPROVED)

**Choice:** Decision Pulse is a shared analytical decision surface. The selected/applied period and dataScope are first-class request and response context, including stable shareable URL and browser history. An unlabelled independent 30-calendar-day feed is **not** the desired product contract.

- Use the established global dataScope (`all`, `existing`, `imported`) and applied date window. When no period was explicitly selected, let the backend provide its documented default anchored in the available reliable observation/import horizon where supported (RQ570/RQ583), **not** client-guessed “today” or implied fresh data. Show requested/effective/observed dates distinctly.
- Keep store and supplier filters explicit. Each Product, Inventory or Supplier source must apply every dimension it supports; where the adapter cannot apply a requested dimension, expose that fact per source with honest partial/not-applied semantics, or withhold that source's actionable candidates. **Never** show all-store/all-supplier recommendations under a selected narrower context.
- Use a consistent documented inclusive-UI / half-open-query period convention, UTC handling, validation, and request cancellation/obsolete-response protection. Filter changes must not flash stale Pulse items or dispositions.
- Preserve backend-owned suppression, freshness, partial/error, recommendation eligibility, and data-quality policies. Do not add frontend scoring, fake-zero, synthetic present-day freshness or inferred tenant identities.
- Preserve source lineage through links and CSV export; never imply identical denominators across different products when source populations differ.
- Required proof: focused client/page/backend tests for URL/reload/history, global scope/period propagation, response requested/effective metadata, unsupported source-dimension behavior, partial/source-failure paths, stale-request races and date boundaries.

**Routing:** RQ480/RQ482 are DONE. RQ481 product-choice gate is satisfied; it can be promoted to **READY** once fresh ownership collision is confirmed. 2026-10-08 main open PRs #112 (SQL) and #116/#102/#103 (docs) do not themselves own Pulse's page/service. Runtime freshness remains a separate deployment gate, not an implementation start gate.

## Decision B — RQ319: explicit Apply across Operations (APPROVED POLICY, WAITING ON PATH)

- Date range/preset, store and multi-field filter edits update **draft**. Click **Primeni** once to apply and refetch. Reset/cancel restores the last applied set. Persist applied state in URL.
- Tabs, sort, pagination and search within the current loaded result may apply immediately; changing their behavior must not silently reapply a dirty date/store filter.
- Trust/header metadata must refer only to data already queried using **applied** values. Matching the app-wide design system §6.3 is more important than preserving one sibling screen's old auto-apply behavior.
- RQ140 is currently IN_PROGRESS and owns overlapping Shoe Type/Pre-Post surfaces; **do not claim RQ319 until explicit release/close and fresh path checks**, even though the business-choice blocker is now resolved.

## Decision C — RQ320: clear unapplied-draft trust state (APPROVED POLICY, WAITING)

- Render a visible **Nije primenjeno** marker whenever draft != applied; offer Apply/Reset. The evidence/trust strip must continue to show the applied period and scope, not whatever the user is typing.
- Execute RQ320 after RQ319 is DONE on the shared pages; don't parallel-write the same controls.

## Deliberately NOT approved or deblocked

- **STAB16/RQ592** — production deploy parity, durable refresh success, source freshness and measured markdown results still require actual provider/read-only/run evidence. Do not classify code tests or old imports as current live proof.
- **RQ472** — no blanket approval of journal completeness. Missing watermark/opening-stock proof remains fail-closed.
- **RQ531, MT02 and other source/policy/identity gates** — no new scoring thresholds, authoritative unavailable metrics, or tenant identity are invented by this decision.
- **PR #112** — the PostgreSQL dependency fix is separate; its schema/CI/merge requirements are not waived here.

## Immediate route

1. Claim/implement RQ481 (P1) after fresh recheck of main, locks, active branches and PRs; do not wait for STAB16 to write deterministic source-contract tests.
2. After RQ140 path ownership closes/releases, promote and implement RQ319 (P2).
3. After RQ319, promote and implement RQ320 (P2).
4. Recompute queue routing after each change, never inherit a prior Zero-READY proof as current.
