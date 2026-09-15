Task ID: direct-product-decision-scope-review
Queue: direct-user-request
Date: 2026-09-15
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / none
Main commit SHA: 4a248f21e82ffac6be9a53d59399c23257c22d33
Main verification: passed - after `git push origin main` and fresh fetch, `origin/main` equals and contains `4a248f21e82ffac6be9a53d59399c23257c22d33`
Evidence state: synchronized

## What was done

- Audited the latest delivered Operations/analytics work on `main`, the current local refs/worktrees, and the code-bearing remote-only refs not previously reviewed in this consolidation.
- Confirmed that the local repository has one worktree and one local branch (`main`); there was no local branch left to merge.
- Did not merge old remote-only refs: their merge bases predate the current delivery and a full merge would remove newer contracts, tests, and evidence. The RQ96 observed-inventory implementation is already present on `main`.
- Fixed Product Decision Center so its initial and changed global `dataScope` are sent both to the decision-center request and the supplier-filter request.
- Cleared the dependent supplier selection when the global scope changes, and when a refreshed supplier list proves that the selection is no longer valid.
- Kept last-known supplier options on filter-request failure, but mark them stale, block selection, show a clear warning, and clear the dependent selection. Applied the same fail-closed behavior to the canonical Supplier page.
- Added focused regression coverage for scope propagation/reload, scope-dependent selection clearing, and direct supplier-filter request failures.

Analytics safety gate:
- Surface: Product Decision Center and canonical Supplier filter controls.
- Source of truth: persisted global `dataScope` and the existing backend supplier-filter / decision-center contracts.
- Contract changed: no; the frontend now forwards an existing parameter and presents request failure honestly.
- Unit, numerator, denominator, true-zero, missing/unknown, and no-baseline behavior: not applicable; no business metric changed.
- Freshness/fallback case: retained filter values are explicitly stale and blocked after fallback metadata or request failure.
- DataScope/store/search filters: `dataScope` now reaches both Product Decision Center requests and its supplier filter; a scope change clears its dependent supplier filter.
- User-visible surfaces affected: Product Decision Center and Supplier canonical filters.
- Export/detail/action payload affected: no.
- Tests proving true-zero vs unknown and table/detail/export/action parity: not applicable; no numeric, detail, export, or action contract changed.
- Stop condition hit: no.

## Files changed

- Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx
- Klijent/clientapp/src/pages/SupplierConsolidatedPage.tsx
- Klijent/clientapp/src/utils/supplierFilterFallbackState.ts
- Klijent/clientapp/src/pages/__tests__/ProductDecisionCenterPage.queueStatus.spec.tsx
- Klijent/clientapp/src/pages/__tests__/SupplierConsolidatedPage.spec.tsx

## Validation run

- `npm run test -- --run src/pages/__tests__/ProductDecisionCenterPage.queueStatus.spec.tsx src/pages/__tests__/ProductDecisionCenterPage.confidence.spec.tsx src/pages/__tests__/ProductDecisionCenterPage.actionStatusFallback.spec.tsx src/pages/__tests__/SupplierConsolidatedPage.spec.tsx` -> pass, 4 files / 43 tests.
- `npm run check:analytics-guardrails` -> pass (encoding check, analytics guardrails, TypeScript build).
- `git diff --check` -> pass before the implementation commit.
- `npm run build` -> inconclusive; two attempts reached Vite chunk rendering, but the command runner ended each at about 30 seconds before a final exit status. This is not counted as a pass.

## Validation not run

- Live browser/backend verification -> not run; this change is covered by frontend request/UI tests and the backend was not started for this review.
- Full frontend suite -> not run; the focused tests cover both changed page owners and their counterexamples, and the static analytics guardrails passed.

## Documentation impact

- No product/architecture document changed; behavior and delivery evidence are recorded in this run log.

## What was missed

- Remote-only legacy refs were intentionally left untouched. They are not local branches and require separate ownership/retention approval before remote pruning.

## Risks

- Production build completion could not be independently confirmed in the command runner despite successful TypeScript/guardrail checks; live API behavior remains unverified in this local review.

## Next

- none
