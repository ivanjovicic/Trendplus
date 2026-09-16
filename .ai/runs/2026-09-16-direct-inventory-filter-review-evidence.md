Task ID: direct-inventory-filter-review
Queue: direct-user-request
Date: 2026-09-16
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / none
Main commit SHA: f9ca2cd6b884af6048f4ac0a936bcca001ece480
Main verification: passed - fresh fetch confirms `origin/main` contains `f9ca2cd6b884af6048f4ac0a936bcca001ece480`
Evidence state: synchronized

## What was done

- Re-audited the latest direct Operations/analytics deliveries, local refs/worktrees, and unreviewed continuation paths after the previous review.
- Confirmed there is one local worktree and one local branch (`main`), with no local branch to merge.
- Found and fixed Inventory supplier-filter drift that remained after the Product Decision/Supplier fail-closed work: a failed supplier-filter request left previous options enabled without a warning.
- On an actual supplier-filter request failure, Inventory now preserves prior options only as visibly stale, blocks new selection, clears the dependent supplier filter, and avoids presenting the old list as confirmed for the active period/scope.
- On a real global `dataScope` change, Inventory now clears the dependent supplier and resets the list to page one before issuing new-scope requests.
- Added regression coverage for both the scope-transition request and the direct supplier-filter failure.

Analytics safety gate:
- Surface: Inventory filter controls and scope-dependent list request.
- Source of truth: persisted global `dataScope` and the existing supplier-filter API contract.
- Contract changed: no; the existing parameter and fallback semantics are now applied consistently.
- Unit, numerator, denominator, true-zero, missing/unknown, and no-baseline behavior: not applicable; no business metric changed.
- Freshness/fallback case: prior supplier values remain visible only as stale/blocked after request failure.
- DataScope/store/search filters: a changed global scope clears the dependent supplier before the Inventory request is sent.
- User-visible surfaces affected: Inventory supplier filter and inventory list.
- Export/detail/action payload affected: no.
- Tests proving true-zero vs unknown and table/detail/export/action parity: not applicable; no numeric, export, detail, or action contract changed.
- Stop condition hit: no.

## Files changed

- Klijent/clientapp/src/pages/InventoryPage.tsx
- Klijent/clientapp/src/pages/__tests__/InventoryPage.queueStatus.spec.tsx

## Validation run

- `npm run test -- --run src/pages/__tests__/InventoryPage.queueStatus.spec.tsx src/pages/__tests__/InventoryPage.partialFailure.spec.tsx src/pages/__tests__/InventoryPage.freshnessLineage.spec.tsx src/pages/__tests__/InventoryPage.forecastRestock.spec.tsx src/pages/__tests__/InventoryPage.forecastGuardrails.spec.tsx src/pages/__tests__/InventoryPage.totalValue.test.tsx src/pages/__tests__/InventoryPage.scopeReload.test.tsx` -> pass, 7 files / 31 tests.
- `npm run check:analytics-guardrails` -> pass (encoding check, analytics guardrails, TypeScript build).
- `git diff --check` -> pass before the implementation commit.

## Validation not run

- Live browser/backend verification -> not run; the fix is covered by frontend request/UI regressions and no backend contract changed.
- Full frontend suite and production bundle build -> not run; the changed owner has focused behavioral coverage and the TypeScript/analytics guardrails passed. A previous production-build attempt in this command environment did not reach a final exit status.

## Documentation impact

- No product or architecture document changed; this run log records the direct review and delivery evidence.

## What was missed

- Remote-only historical refs remain untouched. They are not local branches and their stale merge bases make automatic integration unsafe.

## Risks

- Live service behavior remains unverified locally; the change preserves the existing API contract and is covered at the component-request boundary.

## Next

- none
