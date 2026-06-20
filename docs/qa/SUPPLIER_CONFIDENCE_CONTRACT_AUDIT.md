# Supplier Confidence Contract Audit

Date/time: 2026-06-19 15:43:59 +02:00
Verification HEAD: `58165dc325621a84c5327705f2fe3554bca083d6`

## Purpose

Audit Q43 supplier surfaces against the shared decision-confidence contract so we do not invent confidence values in the UI.

## Files reviewed

- `docs/Analytics/DECISION_CONFIDENCE_CONTRACT.md`
- `Klijent/clientapp/src/pages/SupplierConsolidatedPage.tsx`
- `Klijent/clientapp/src/pages/SupplierDecisionHubPage.tsx`
- `Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx`
- `Klijent/clientapp/src/services/supplierDecisionReport.ts`
- `Klijent/clientapp/src/components/analytics/SupplierDecisionReport.tsx`
- `Klijent/clientapp/src/pages/__tests__/SupplierDecisionHubPage.spec.tsx`
- `Klijent/clientapp/src/services/__tests__/supplierDecisionReport.spec.ts`

## Surface mapping

| Surface | Contract source | Missing confidence behavior | Classification | Notes |
| --- | --- | --- | --- | --- |
| Supplier summary / consolidated header | `SupplierTrustHeaderPayload.recommendationAllowed`, `usedFallback`, `dataQualityStatus` | No numeric confidence is shown in the summary shell, so nothing is invented | SAFE | Summary is trust-first, not confidence-first |
| Supplier list / scorecard detail | `normalizeRecommendationPct(item.confidenceScore)` -> `confidenceAvailable` | Detail panel renders `RECOMMENDATION_SIGNAL_UNAVAILABLE` when backend omits confidence | SAFE | Internal sort fallback to `0` is used only for ordering, not display |
| Supplier overview detail | `normalizeRecommendationPct(recommended?.confidencePct)` -> `confidenceAvailable` | Selected supplier renders `RECOMMENDATION_SIGNAL_UNAVAILABLE` when confidence is missing | SAFE | The visible UI does not convert missing confidence into `0%` |
| Supplier report payload | `SupplierDecisionReportRow.confidenceAvailable` | Report row uses `Sigurnost nije dostupno` when confidence is missing | SAFE | Report renderer prints the payload value verbatim |

## Risk audit

- `confidencePct ?? 0` appears in supplier sorting code, but only as an internal tie-break / ordering fallback.
- `confidenceAvailable` gates every visible supplier confidence field that was reviewed.
- `RECOMMENDATION_SIGNAL_UNAVAILABLE` is the visible fallback text for missing confidence/reliability.
- No supplier surface reviewed here invents a new confidence tier or shows a fake 0% confidence label.

## Tests added

- `Klijent/clientapp/src/pages/__tests__/SupplierDecisionHubPage.spec.tsx`
- `Klijent/clientapp/src/services/__tests__/supplierDecisionReport.spec.ts`

## Checks

- `git diff --check` - pass, with repository line-ending warnings only
- `cd Klijent/clientapp && npm run check:analytics-guardrails` - pass
- `cd Klijent/clientapp && npm run test -- --run src/pages/__tests__/SupplierDecisionHubPage.spec.tsx src/services/__tests__/supplierDecisionReport.spec.ts` - pass
- `cd Klijent/clientapp && npm run build` - pass

## Outcome

Q43 is complete on the repo side: supplier summary/list/report confidence semantics are mapped to the shared contract without inventing new values in the UI.

## Follow-up

- Keep an eye on the internal `?? 0` sort fallback in supplier ranking code, but do not treat it as a visible confidence regression unless the UI starts rendering it.
