# RQ281/RQ282 hardening evidence

Task ID: RQ281-RQ282-hardening
Queue: direct-user-request
Date: 2026-09-15
Agent/tool: cursor-cloud-agent
Delivery target: none
Working branch / PR: cursor/rq282-supplier-null-id-identity-c753
Main commit SHA: pending
Main verification: pending
Evidence state: synchronized

## What was done
Hardened the last two executed prompts without changing their owners.

RQ281: `lastRefreshAt` is now only projected from a parseable successful `lastRefreshAtUtc`. Failed and malformed timestamps stay unknown/omitted, empty success still keeps a valid timestamp with unknown freshness, parent overview mock asserts the canonical header can render child freshness, and embedded error/empty callbacks are covered.

RQ282: vendor keys are built from the whole row set so duplicate backend IDs do not collapse; article attribution refuses to guess on colliding IDs or blank duplicate names; detail snapshots keep row keys when the ID is not unique; the selected-row detail states that identity is unconfirmed.

## Files changed
- `Klijent/clientapp/src/utils/supplierSalesStatsTrust.ts`
- `Klijent/clientapp/src/utils/__tests__/supplierSalesStatsTrust.spec.ts`
- `Klijent/clientapp/src/pages/__tests__/SupplierSalesStatsPage.premium.spec.tsx`
- `Klijent/clientapp/src/pages/__tests__/SupplierConsolidatedPage.spec.tsx`
- `Klijent/clientapp/src/utils/supplierVendorIdentity.ts`
- `Klijent/clientapp/src/utils/__tests__/supplierVendorIdentity.spec.ts`
- `Klijent/clientapp/src/pages/SupplierFootwearAnalyticsPage.tsx`
- `Klijent/clientapp/src/pages/__tests__/SupplierFootwearAnalyticsPage.spec.tsx`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- `.ai/runs/2026-09-15-RQ281-evidence.md`
- `.ai/runs/2026-09-15-RQ282-evidence.md`

## Validation run
- `npm run test -- --run src/utils/__tests__/supplierSalesStatsTrust.spec.ts src/utils/__tests__/supplierVendorIdentity.spec.ts src/pages/__tests__/SupplierSalesStatsPage.premium.spec.tsx src/pages/__tests__/SupplierFootwearAnalyticsPage.spec.tsx src/pages/__tests__/SupplierConsolidatedPage.spec.tsx` — pass (51/51)

## Validation not run
- full frontend suite
- live browser proof

## Documentation impact
Completion notes for RQ281 and RQ282 record the same-owner hardening follow-up.

## What was missed
- Footwear/scorecard embedded freshness still uses their own resolvers; out of RQ281 owner scope

## Risks
- Duplicate vendor IDs are treated as distinct rows without a backend identity contract change

## Next
- `RQ283` remains next WAITING
