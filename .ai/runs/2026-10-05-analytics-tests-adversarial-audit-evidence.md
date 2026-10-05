# Evidence — Analytics tests adversarial audit 2026-10-05

- Start base: `origin/main` `8703a0b7`
- Branch: `analytics-tests-adversarial-2026-10-05`
- Box worktree: `/workspace/docs-audit`
- Trendplus2: not touched
- RQ588: not claimed / not edited
- RQ582: Insight quarantine respected (no product redesign)

## Commands

```text
dotnet test Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AnalyticsCertifiedRetailMetamorphicTests|FullyQualifiedName~AnalyticsSupplierSalesUnitTests+AggregationInvariantTests|FullyQualifiedName~AnalyticsSupplierSalesUnitTests+MarginCalculationTests|FullyQualifiedName~AnalyticsSupplierSalesUnitTests+PctHelperTests|FullyQualifiedName~AnalyticsCrossScreenRevenueInvariantIntegrationTests|FullyQualifiedName~SalesReceiptPopulationPolicyTests|FullyQualifiedName~SupplierSharePolicyTests"
→ Passed 40, Failed 0, Skipped 2

npm test -- --run src/utils/__tests__/certifiedRetailMetamorphic.spec.ts src/utils/__tests__/analyticsUnavailableLabel.spec.ts src/utils/__tests__/dailyShiftSummary.spec.ts
→ 12/12 passed

npm run check:analytics-guardrails  # includes encoding + tsc -b
→ OK

git diff --check
→ OK
```

## Key changes

1. In-memory certified retail oracle + metamorphic suite (BE + FE)
2. Cross-screen revenue invariant integration tests (skip without integration env)
3. Replaced wrong null-cost / tautology supplier unit tests
4. Oracle manifest half-open + header-origin truth
5. Guardrail baseline line drift IntelligenceSnapshotPanel 273→274

## Bundle

See `/workspace/out/analytics-tests-adversarial-2026-10-05.bundle` after commit.
