# Analytics generation provenance truth — 2026-08-24

Scope: RQ113
Family: supplier-decision / supplier-sales-stats

This note records the additive provenance contract used to make the first pilot supplier family explain why a result is trusted without relying on render time or an inferred cache age.

## Contract facts

- requested period/scope stays explicit in the supplier decision hub trust metadata and report payload
- effective period/scope stays explicit when fallback is used
- provenance basis is now explicit instead of inferred
- fallback / degraded / operational path state remains visible through the existing trust metadata fields

## Implemented provenance bases

- supplier decision hub: `mv_supplier_decision_score_cache`, `mv_supplier_decision_score_cache_90d`, or `mv_supplier_decision_score_cache_180d` depending on the requested window
- supplier sales stats: `live_query` or `live_query/snapshot_cost_batch_<id>` when the snapshot-cost path is active

## Proof points

- `Api.Tests/SupplierDecisionHubContractTests.cs`
- `Api.Tests/SupplierDecisionSchemaSqlTests.cs`
- `Klijent/clientapp/src/components/analytics/__tests__/AnalyticsTrustHeader.spec.tsx`
- `Klijent/clientapp/src/components/analytics/__tests__/SupplierExplainabilitySnapshot.spec.tsx`
- `Klijent/clientapp/src/components/analytics/__tests__/SupplierDecisionReport.spec.tsx`
- `Klijent/clientapp/src/pages/__tests__/SupplierDecisionHubPage.spec.tsx`
- `Klijent/clientapp/src/pages/__tests__/SupplierConsolidatedPage.spec.tsx`
- `Klijent/clientapp/src/pages/__tests__/SupplierSalesStatsPage.premium.spec.tsx`
- `Klijent/clientapp/src/pages/__tests__/analyticsTrustStateProof.spec.tsx`

## Notes

- The UI renders the provenance basis as additive trust metadata; it does not invent a scoring rule on the client.
- Unknown provenance should still remain unknown/unavailable rather than being rewritten as fresh or healthy.
