# Analytics Semantic Guardrails

Purpose
-------
This document describes the semantic guardrails for analytics code in the Trendplus frontend and backend. Its goal is to avoid "drift" between frontend UI code and backend business logic (recommendation engines, decision scores, confidence/reliability signals).

Source of truth
----------------
- Final recommendation status, confidence, reliability, decision score and related reason codes MUST be produced by backend engines:
  - Product recommendations -> backend product decision center (product-level model)
  - Supplier recommendations -> `AnalyticsDecisionRecommendationEngine` / canonical supplier backend
  - Inventory action suggestions -> backend inventory action workflow
  - Dashboard decision actions -> backend dashboard bootstrap
- Formatting, presentation and tooltips are frontend responsibilities but MUST use shared utilities: `analyticsFormatters`, `analyticsMetricDescriptions`.

Do / Don't (high level)
------------------------
- Do: format numbers using shared helpers (`fmtRsd`, `fmtPct`, etc), map backend status to UI classes/labels, sort and filter using backend-provided fields.
- Don't: reimplement weighted decision scores, thresholds (BOOST/KEEP), or compute final `recommendationStatus`/`confidencePct`/`reliabilityPct` in the browser.

Concrete rules enforced by guardrails
-----------------------------------
1. Backend is the single source of truth for:
   - `recommendationStatus` (final)
   - `confidencePct`
   - `reliabilityPct`
   - decision score (business-weighted)
   - `dataQualityStatus` used in recommendations
   - margin / cost coverage data when used to influence decisions

2. Frontend is allowed to:
   - format numbers and dates via `src/utils/analyticsFormatters.ts`
   - sort and filter results using backend fields
   - map statuses to CSS classes / labels (pure mapping) and show tooltips

3. Frontend is forbidden to:
   - calculate final business recommendations (no local weighted decisionScore formulas)
   - define BOOST/KEEP thresholds or similar stopgap business constants in page code
   - fabricate `confidencePct` / `reliabilityPct` when backend does not provide them
   - fall back to local recommendation calculations when backend response is null

If backend omits recommendation
-------------------------------
- UI must show either "Nedovoljno podataka" / "Preporuka nije dostupna" rather than inventing a recommendation.

How to add a new analytics screen without drift
----------------------------------------------
1. Design the DTO(s) that the backend will return: include `recommendation` object with `Status`, `Label`, `ConfidencePct`, `ReliabilityPct`, `DataQualityStatus`, `ReasonCodes`.
2. Implement backend logic that produces the DTO and include unit tests for the engine.
3. Frontend renders the DTO using shared formatters and mapping helpers; avoid computing decision logic in page code.
4. Add guardrail tests (if the new screen contains business logic, the `check-analytics-guardrails` script should fail).

How to add a new metric
------------------------
1. Add metric description to `src/utils/analyticsMetricDescriptions.ts` (tooltip / label / short description).
2. Add format rules, if needed, to `src/utils/analyticsFormatters.ts`.
3. Add backend support for the metric if it influences recommendations.

How to add a new recommendation
-------------------------------
1. Add new recommendation code and reasonCodes in backend engine (unit-tested).
2. Ensure endpoints return the new recommendation fields.
3. Update frontend mapping (label & UI styling) in `src/layout/navConfig.ts` or relevant component mapping.

What to do if backend does not provide required data
----------------------------------------------------
- Do not compute substitute recommendations in the browser.
- Show user-friendly messages: "Nedovoljno podataka" or "Preporuka nije dostupna".
- Add feature request / issue to backend product team to provide the missing signal.

Appendix: Examples
------------------
- Good (frontend): `recommendationToneClass(status)` maps backend status to CSS class.
- Bad (frontend): `const decisionScore = qualityIndex*0.4 + confidence*0.3 + shareNorm*0.3` in a page file.

Guardrail automation
--------------------
- Script: `scripts/check-analytics-guardrails.mjs` scans the frontend sources and flags suspicious patterns.
- Run locally via `npm run check:analytics-guardrails` (from `Klijent/clientapp`).

Contact
-------
For questions about rules or false positives, talk to the analytics backend owner before changing the script rules.
