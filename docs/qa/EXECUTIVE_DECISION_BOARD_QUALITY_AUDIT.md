# Executive Decision Board Quality Audit

Date: 2026-06-19 14:14:45 +02:00
Local HEAD: `c1d4482f728698727e96613d9312e8b5eaa177ec`

## Scope

- [ExecutiveDecisionBoardPage.tsx](../../Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx)
- [ExecutiveDecisionBoardPage.spec.ts](../../Klijent/clientapp/src/pages/__tests__/ExecutiveDecisionBoardPage.spec.ts)

## What We Verified

### 1. `insufficient_data` does not rank high

- The board now caps insufficient-data priority through `capInsufficientDataPriority(...)`.
- The cap applies to product, inventory, supplier, action, outcome, and the `blocker-signal` path.
- This keeps insufficient-data items visible, but prevents them from outranking real, high-confidence decision cards.

### 2. Stale and partial modules stay warned

- The blocker builder surfaces stale refresh state as `warning` or `critical`.
- Partial board state stays visible through the board model when warnings or errors are present.
- The new stale-refresh test confirms the warning code stays attached instead of being hidden.

### 3. Duplicate recommendations are repeated with context, not silently deduped

- The board intentionally reuses the same source recommendation across different sections.
- That repetition is acceptable here because each section is a different decision lens:
  - urgent
  - impact
  - stock risk
  - supplier risk
  - blockers
  - actions
- The model keeps the section titles and descriptions distinct so the user can tell why the same source appears again.

### 4. Missing expected impact does not become `0 RSD`

- Section card rendering shows `Nije dostupno` when `expectedImpactRsd` is missing.
- That avoids fake-zero presentation for incomplete data.

## Evidence

- `Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx`
  - priority capping for insufficient-data cards
  - stale/partial blocker surfacing
  - board section composition
  - `Nije dostupno` rendering for missing impact
- `Klijent/clientapp/src/pages/__tests__/ExecutiveDecisionBoardPage.spec.ts`
  - insufficient-data product ranking regression
  - stale refresh warning regression
  - insufficient-data blocker regression
  - repeated section-context regression

## Verification

- `git diff --check` - pass
- `cd Klijent/clientapp && npm run check:analytics-guardrails` - pass
- `cd Klijent/clientapp && npm run build` - pass
- `cd Klijent/clientapp && npm run test -- --run ExecutiveDecisionBoard` - pass

## Risk

- The board still repeats the same source cards across multiple sections by design. That is acceptable for now because the section context explains why the card is repeated.
- If product wants global dedupe later, that would need a separate UX decision and follow-up task.
