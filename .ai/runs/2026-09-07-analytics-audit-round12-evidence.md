# Analytics Audit Round 12 Evidence

- Date: 2026-09-07
- Queue: `direct-user-request`
- Branch: `main`
- Scope: production analytics only; standalone Trend, forecast, Shopify/vendor integrations and test-only functionality excluded.
- Objective: perform another deep analytics audit, identify new missed bugs, deduplicate against existing queue ownership, and write actionable prompts.

## Authority and code inspected

- `AGENTS.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- `Api/Endpoints/CachedAnalyticsEndpoints.cs`
- `Api/Endpoints/DecisionBoardEndpoints.cs`
- `Api/Endpoints/InventorySignalCalculator.cs`
- `Application/Analytics/ProductDecisionReasoningHelper.cs`
- `Application/Analytics/AnalyticsDecisionRecommendationEngine.cs`
- `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx`
- `Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx`
- `Klijent/clientapp/src/pages/SupplierDecisionHubPage.tsx`
- `Klijent/clientapp/src/utils/analyticsFormatters.ts`
- nearest PDC, supplier and response-meta tests

## Git history inspected

- `git log --oneline --all -- Api/Endpoints/CachedAnalyticsEndpoints.cs Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx Klijent/clientapp/src/pages/SupplierDecisionHubPage.tsx`
- `git blame` on PDC stock, margin coverage and refresh-meta lines.
- Relevant prior fixes: `a5c97d66` PDC baseline/coverage, `7a3cc040` pre/post unknown coverage, `69511be0` inventory null state, `322ec5fa` top-product margin coverage, `ec177a96` queue/evidence synchronization.

## Findings added

### `RQ254` - PDC refresh provenance

`BuildProductDecisionCenterAsync` uses its query-local `nowUtc` as `LastRefreshAtUtc`. The direct Decision Board composition calls the builder outside the cached route. This violates the distinction between response generation and proven successful source refresh. Existing `RQ187` covers cache-hit metadata assignment, not this direct/cache-miss path.

### `RQ255` - PDC nullable stock evidence

PDC maps nullable article quantity and minimum stock to zero before stock calculations and evidence rendering. This can present unknown stock as measured zero/OOS/minimum zero. `RQ158` covers general inventory paths but does not cover this PDC snapshot contract.

### `RQ256` - PDC no-sales margin denominator

When current-period revenue is zero, PDC serializes `MarginCoveragePct=0` and labels the row low quality. The denominator is absent, so the correct state is unavailable; measured zero is valid only with a positive, known revenue denominator.

### `RQ257` - Supplier non-finite classification

Supplier Sales Stats and Supplier Decision Hub use `Number.isNaN` instead of `Number.isFinite` in PoP/trend and ratio-to-percent helpers. `Infinity` can therefore be classified as available while shared formatting displays `N/A`, producing cross-surface state drift.

## Changed files

- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- `docs/ai/ANALYTICS_RELIABILITY_AUDIT_PROMPTS_2026-09-07-ROUND12.md`
- `.ai/runs/2026-09-07-analytics-audit-round12-evidence.md`

No production code was changed. These are queue prompts for subsequent execution.

## Validation

- `node scripts/check-prompt-queues.mjs` - PASS: `OK: prompt-queue governance checks passed (396 tasks).`
- `node scripts/check-prompt-queues.mjs --self-test` - PASS.
- `node scripts/check-agent-instructions.mjs` - PASS: `8 canonical files checked`.
- `node scripts/check-agent-instructions.mjs --self-test` - PASS.
- `node scripts/check-planning-architecture.mjs` - PASS: `78 new planning tasks checked`.
- `node scripts/check-planning-architecture.mjs --self-test` - PASS.
- `git diff --check` - PASS; Git emitted only the normal LF-to-CRLF working-copy warning for the queue file.
- New audit/evidence Markdown files have no trailing whitespace.

Product builds, backend/frontend tests and live browser/refresh/schema proof were not run because this pass only creates audit documentation and queue prompts; no implementation behavior is claimed.

## Delivery state and residual risk

- Current canonical `READY`: `RQ169`.
- New prompts `RQ254`-`RQ257`: `WAITING`, not claimed and not implemented.
- No commit or push was requested for this audit turn.
- Until the prompts execute, the four documented behaviors remain open in local production code.
- Existing broad owners `RQ143`, `RQ145`, `RQ146`, `RQ158`, `RQ187` and `RQ191` remain active where explicitly noted; the new prompts do not mark them complete.
