Task ID: direct-product-decision-history-ux
Queue: direct-user-request
Date: 2026-09-02
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / direct push
Main commit SHA: 3b5269d4e69a8db42454d63b80300924012a49cf
Main verification: passed - origin/main contains implementation 3b5269d4e69a8db42454d63b80300924012a49cf
Evidence state: synchronized

## What was done
- Confirmed the reported row is not a calculation/database error: the live Product Decision endpoint returns 2 sold units for SKU 3857 in the 30-day window, while the backend safety gate requires at least 3 units.
- Added explicit backend reason codes for small sample, no sales in period and missing last sale.
- Replaced the generic insufficient-data explanation with a precise, operator-facing reason that distinguishes a deliberate safety block from a system error.
- Added a visible blocked-decision gate with the cause and next step, separated signal reliability from recommendation readiness, and renamed the table quality column to “Kvalitet ulaza”.
- Changed blocked-row queue action wording to “Dodaj u proveru”.
- Added warning text theme tokens and higher-contrast warning styles for light, dark, neon and high-contrast themes.

## Files changed
- Application/Analytics/ProductDecisionReasoningHelper.cs
- Api/Endpoints/CachedAnalyticsEndpoints.cs
- Api.Tests/ProductDecisionReasoningHelperTests.cs
- Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx
- Klijent/clientapp/src/pages/ProductDecisionCenterPage.css
- Klijent/clientapp/src/pages/__tests__/ProductDecisionCenterPage.confidence.spec.tsx
- Klijent/clientapp/src/context/ThemeContext.tsx
- Klijent/clientapp/src/styles/themes.css

## Validation run
- dotnet test Api.Tests/Api.Tests.csproj --no-restore --filter "FullyQualifiedName~ProductDecisionReasoningHelperTests|FullyQualifiedName~AnalyticsProductDecisionConfidenceTests|FullyQualifiedName~ProductDecisionCenterBuilderIntegrationTests" -> pass (14/14)
- npm run check:analytics-guardrails -> pass (encoding, analytics guardrails, TypeScript)
- npm run build -> pass
- npx eslint src/pages/ProductDecisionCenterPage.tsx src/pages/__tests__/ProductDecisionCenterPage.confidence.spec.tsx src/context/ThemeContext.tsx -> fail only on pre-existing Fast Refresh errors and existing hook warnings in the touched page/context; no new rule failure from the added code was reported
- git diff --check -> pass
- Live GET /api/analytics/cached/products/decision-center for the reported 30-day window -> pass (HTTP 200; exact row confirmed with 2 units, 36.65% margin, 100% cost coverage, INSUFFICIENT_DATA)

## Validation not run
- Focused Vitest ProductDecisionCenterPage.confidence.spec.tsx -> not completed - Vitest produced only its startup banner and no result after more than two minutes in two isolated attempts; processes were terminated as inconclusive.
- Full frontend test suite -> not run - focused Vitest execution did not provide a usable result.
- Production verification of the new message -> pending - public Render endpoint still returned the old generic reason after the main push, indicating deployment lag or an instance not yet refreshed.

## Documentation impact
- No owner documentation changes were needed; this was a scoped Product Decision backend/frontend contract and UX repair.

## What was missed
- Render deployment refresh and a fresh live response containing the new explicit `low_sample_size` reason remain pending.

## Risks
- The safety threshold remains 3 sold units by design; relaxing it would create potentially unsafe recommendations. Production UI will show the new explanation only after the backend deployment is refreshed.

## Next
- Refresh/verify the Render deployment for commit 3b5269d4e69a8db42454d63b80300924012a49cf, then recheck SKU 3857 and the Product Decision Center UI in all themes.
