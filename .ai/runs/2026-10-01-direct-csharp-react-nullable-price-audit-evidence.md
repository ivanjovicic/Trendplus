Task ID: direct-csharp-react-nullable-price-audit
Queue: direct-user-request
Date: 2026-10-01
Agent/tool: Codex local
Delivery target: main
Working branch / PR: main / none
Main commit SHA: 719215fb6f4d2832990412a5c37aff3c1f8d3796
Main verification: passed - fresh `origin/main` is `719215fb6f4d2832990412a5c37aff3c1f8d3796` and contains the implementation commit
Evidence state: synchronized

## What was done
- Re-reviewed the remaining C# and React analytics commit coverage against the earlier direct-audit evidence. The newly confirmed defect was in Pre/Post price-change averages: `DefaultIfEmpty().Average()` converted “no observed price change” to a measured `0%`, and the dominant-price card could display the unavailable-price segment as its leading direction.
- Added a shared C# aggregate helper that averages observed price-change percentages only and returns null when none are known. Kept an observed average of exactly `0%` numeric.
- Made the aggregate fields nullable through the API DTO, TypeScript and Zod contracts. The React card now omits segments with unknown price changes and shows its unavailable state.
- Read the focused sources/contracts and tests: `Api/Endpoints/AllEndpoints.cs`, `Api/Models/VendorSalesNivelacijaModels.cs`, `Application/Analytics/VendorSalesNivelacijaPriceChangeEffectPolicy.cs`, related `Api.Tests` policy/nullability tests, the Pre/Post page/service/schema and their focused tests, `docs/ai/SQL_ANALYTICS_PROMPT_QUEUE.md`, `MASTER_ROADMAP.md`, and the prior direct C#/React audit logs.
- This was classified and delivered as a direct user request. No formal queue claim or queue status was edited. The current Q83 section says DONE while the roadmap’s latest routing entry says it was re-promoted to READY; that owner-state inconsistency remains for canonical queue recovery.

## Files changed
- `.ai/runs/2026-10-01-direct-csharp-react-nullable-price-audit-evidence.md`
- `Api.Tests/VendorSalesNivelacijaNullabilityContractTests.cs`
- `Api.Tests/VendorSalesNivelacijaPriceChangeEffectPolicyTests.cs`
- `Api/Endpoints/AllEndpoints.cs`
- `Api/Models/VendorSalesNivelacijaModels.cs`
- `Application/Analytics/VendorSalesNivelacijaPriceChangeEffectPolicy.cs`
- `Klijent/clientapp/src/pages/ProdajaPrePostNivelacijePage.spec.tsx`
- `Klijent/clientapp/src/pages/ProdajaPrePostNivelacijePage.tsx`
- `Klijent/clientapp/src/pages/__tests__/SupplierFootwearAnalyticsPage.scope.spec.tsx`
- `Klijent/clientapp/src/pages/__tests__/SupplierFootwearAnalyticsPage.spec.tsx`
- `Klijent/clientapp/src/services/__tests__/vendorSalesNivelacijaApi.scope.spec.ts`
- `Klijent/clientapp/src/services/vendorSalesNivelacijaApi.ts`
- `Klijent/clientapp/src/validation/__tests__/analyticsResponseSchemas.spec.ts`
- `Klijent/clientapp/src/validation/analyticsResponseSchemas.ts`

## Validation run
- `dotnet test Api.Tests/Api.Tests.csproj --no-restore --configuration Release --filter "FullyQualifiedName~VendorSalesNivelacijaPriceChangeEffectPolicyTests|FullyQualifiedName~VendorSalesNivelacijaNullabilityContractTests" --verbosity quiet` -> pass, 16/16; the build emitted existing repository analyzer/nullability warnings.
- `npm run test -- --run src/pages/ProdajaPrePostNivelacijePage.spec.tsx src/validation/__tests__/analyticsResponseSchemas.spec.ts src/services/__tests__/vendorSalesNivelacijaApi.scope.spec.ts src/pages/__tests__/SupplierFootwearAnalyticsPage.scope.spec.tsx src/pages/__tests__/SupplierFootwearAnalyticsPage.spec.tsx` -> pass, 93/93.
- `npm run check:analytics-guardrails` -> pass, including encoding check, guardrail self-test, analytics guardrails and TypeScript typecheck.
- `npm run build` -> pass; Vite reported the existing advisory for chunks over 500 kB.
- `git diff --check` and `git diff --cached --check` -> pass.
- `git fetch origin main` and `git merge-base --is-ancestor 719215fb6f4d2832990412a5c37aff3c1f8d3796 origin/main` -> pass.
- GitHub Actions for implementation SHA `719215fb6f4d2832990412a5c37aff3c1f8d3796`: Analytics Quality Gates run `36892311311` and Analytics Tests & Data Integrity run `36892311292` were `in_progress` when inspected; no final result is claimed.

## Validation not run
- Full backend and frontend suites -> not run; the changed helper, DTO, API contract and React presentation have focused proof.
- PostgreSQL/Testcontainers and live production database/view verification -> not run; this fix changes aggregate nullability and presentation, not SQL or a database contract.

## Documentation impact
- No product, queue or roadmap documents were changed. This direct-task evidence log records the implementation and identifies the existing Q83 queue/roadmap state mismatch without changing queue ownership.

## What was missed
- No additional confirmed defect was found in the remaining C# and React commit coverage reviewed alongside the previously recorded audits.
- Canonical reconciliation of the Q83 DONE/READY status mismatch remains outside this direct-task delivery.

## Risks
- The two Actions runs for the delivered SHA were still in progress at inspection time.
- No live deployment or production analytics contract is inferred from local proof.

## Next
- Q83 owner: reconcile the SQL queue and roadmap status before further Q83 execution; the direct code fix is already on verified `main`.
