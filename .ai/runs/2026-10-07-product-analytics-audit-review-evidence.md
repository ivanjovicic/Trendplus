# Product / analytics audit review evidence — 2026-10-07

Task: verify and correct the 2026-10-07 Trendplus product/analytics/business-value audit and align product documentation/queues.

## Reviewed repository evidence

- `docs/product/TRENDPLUS_PRODUCT_ANALYTICS_VALUE_AUDIT_2026-10-07.md`
- `docs/product/PRODUCT_VISION.md`
- `docs/roadmaps/BUSINESS_ROADMAP.md`
- `README.md`
- `MASTER_ROADMAP.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- `docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md`
- `docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_NIVELACIJA_AUDIT_ADDENDUM.md`
- `docs/qa/ANALYTICS_REAUDIT_2026-10-04.md`
- `docs/qa/ANALYTICS_RELIABILITY_VALUE_NEXT_WAVE_AUDIT_2026-10-04.md`
- `docs/qa/ANALYTICS_ACCURACY_AUDIT_2026-10-05.md`
- QDB09 evidence and current connector runtime files.

## External reality check

Public product pages reviewed for: Logosoft SmartPOS/POSitiv, SVEra, TiramisuPOS, Konty, Lightspeed shoe retail and Retalon markdown optimization.

Key correction: local products clearly cover many standard retail/inventory/reporting capabilities; global products already market markdown optimization. No “unique feature” claim is accepted from absence in a small public-page sample.

## Findings corrected

1. Web `workersEnabled=false` != proof no dedicated worker service.
2. QDB is not empty; SQL Server reaches checkpointed staging.
3. The residual commercial connector gap is staging -> canonical retail facts -> analytics/onboarding.
4. RQ574 removes the universal missing-category blocker when TipObuce is authoritative.
5. Commercial thresholds/pricing/ROI are hypotheses.
6. Audit commit-category percentages are treated as snapshot estimates, not canonical metrics.
7. Live results are dated; no 2026-10-04/09-28 observation is presented as current-main runtime truth.

## Owner decisions applied

- RQ585 priority P3 -> P1; freshness start gate retained.
- RQ585 optional signal families fail closed rather than blocking/faking a full digest.
- RQ592 P1 retained; pre-registration begins before execution as soon as freshness + live Pre/Post are restored.
- QDB10 registered WAITING for canonical non-Access ingestion after QDB07/QDB08 and external-pilot source choice.
- Product strategy ordered: freshness -> inventory capital -> markdown outcome -> supplier value -> external pilot.
- No bulk status changes to MT/GAI/P-UI/legacy prompts; defer guidance does not bypass canonical dependencies.

## Validation boundary

This delivery is documentation/queue only; no runtime formulas or product code are changed. Connector-side structural checks verify required sections/IDs and exact replacements. Repository CI/governance runs after the commit remain the authoritative executable validation.
