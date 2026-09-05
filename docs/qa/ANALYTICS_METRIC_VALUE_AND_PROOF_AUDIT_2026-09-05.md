# Analytics Metric Value and Proof Audit - 2026-09-05

Status: planning audit; no runtime behavior claimed

## Scope

This audit rates the decision value and proof strength of Trendplus's important analytics metrics. It compares controls, not vendor marketing claims, with Tableau, Power BI, Shopify, Lightspeed Retail and Amazon Forecast. External products are reference points for expected transparency; they do not prove Trendplus data is correct.

Current runtime claims are intentionally limited. The latest retained live audit is dated 2026-08-27 and reported unknown freshness, no registered worker and no direct source reconciliation. It is historical evidence, not a claim about the live deployment on 2026-09-05.

## Rating model

| Level | Meaning | Allowed product use |
| --- | --- | --- |
| 0 | Unavailable or missing a required source/denominator | Explain the gap; no KPI value, confidence or action |
| 1 | Descriptive observed/derived metric with explicit scope and limitation | Context, trend investigation and drilldown only |
| 2 | Decision-support signal with proven source, freshness, coverage and fail-closed gate | Backend-gated recommendation with clear caveat |
| 3 | Measured predictive signal with out-of-time comparison, cohort performance and baseline | Forecast support; confidence only when calibrated |
| 4 | Causal incremental estimate with treatment/control or pre-specified counterfactual | Impact/uplift claim, never inferred from before/after alone |

Decision value equals business relevance multiplied by measurement proof. A financially material metric with level 0-1 evidence is not a safe basis for action.

## Current metric assessment

| Metric family | Business value | Current proof | Current safe use | Main uncertainty | Required follow-up |
| --- | --- | --- | --- | --- | --- |
| Revenue and units sold | High | Level 1-2 | Describe sales and rank only after period/scope/freshness are explicit | Gross/net, returns, cancellation, discount, tax and source coverage vary by route | RQ148 |
| Margin contribution and gross margin | High | Level 1 | Prioritize investigation where cost coverage is explicit | Partial/fallback cost can look like profit; not net profit | RQ148, RQ147 |
| Supplier sales/margin | High | Level 1-2 | Supplier context and backend-gated signals | Same finance-basis risk plus supplier mapping/scope | RQ148, RQ141 |
| Sell-through and stock cover | High | Level 2 for guarded rows | Backend-gated inventory signal | Opening stock/inbound and velocity are guarded, but history and availability basis are incomplete | RQ149 |
| Inventory turnover, slow-stock capital, stock-at-risk | High | Level 1-2 | Operational investigation with cost/snapshot caveat | Current/reconstructed state and cost coverage can differ from historical economic value | RQ149 |
| GMROI | High | Level 0 | Do not display as active KPI | No stable observed average-inventory-at-cost contract | RQ149 |
| OOS/lost-sales estimate | High | Level 1 | Label as modelled potential, never booked revenue | True zero is separated from unavailable, but OOS/demand assumptions and counterfactual are not causal proof | RQ149, RL12 |
| Forecast/trend evaluation | High | Level 0-1 | Evidence contract only; no numeric accuracy/confidence until measured pairs exist | Pairing, cohort sample, baseline and freshness remain incomplete | RQ142, RQ150 |
| Forecast confidence/reliability | High | Level 0-1 | Hide when calibration evidence is absent | A backend field is not empirical calibration | RQ147, RQ150 |
| Pre/post nivelacija and elasticity | High | Level 1 locally; Level 0 for current live causal claim | Limited comparable signal; no automatic action without evidence | Availability, composition, seasonality and control cohort can explain apparent effect | RQ140, RL12 |
| Data-quality/readiness score | Medium-high | Level 1-2 | Gate other claims; show exact issue and scope | A green local score cannot prove freshness or unrelated KPI completeness | RQ141, RQ144, RQ147 |
| Recommendation outcomes | High | Level 1 | Measurement-only review of observed outcome coverage | Acceptance/execution and before/after are not incremental impact | RL12 |

## What is already strong

- The codebase has explicit no-fake-zero, empty-is-not-error and backend-owned recommendation rules. `LostSalesValidationSourceStatus` distinguishes a trusted zero, fallback and unavailable source instead of collapsing them into `0 RSD`.
- Stock cover and sell-through have documented formulas and denominator blocking. The frontend uses backend status rather than calculating row-level inventory signals independently.
- Forecast evaluation is intentionally fail-closed: the established baseline/backtest contract returns unavailable rather than fabricated WAPE, MAE or bias without paired observed data.
- Current queue work already recognizes lineage, period, freshness, schema failure, parity and frontend decision invention as independent correctness concerns (`RQ141`-`RQ146`).

## Material gaps

1. KPI methodology is client-centered, not an authoritative evidence contract. Formula text alone does not declare observed/modelled/causal status, source generation, coverage or action eligibility.
2. Financial labels need a route-specific basis. A sales-line sum and available-cost margin are not automatically net sales or profit when return/cancellation/tax/discount/cost coverage is unknown.
3. Inventory economic value needs observed historical stock and availability treatment. Sales observed during stockouts are censored demand, so turnover, lost-sales and GMROI cannot be treated as equally proven.
4. Forecast accuracy needs cohort-specific backtesting and calibration. A single aggregate percentage cannot establish replenishment usefulness or validate a prediction interval.
5. Outcome tracking is valuable but not causal. No current evidence permits an "incremental impact" or "uplift" claim from a recommendation outcome without control/counterfactual evidence.

## Benchmark comparison

- Tableau attaches data-quality warnings to data sources, tables and columns, propagates them to downstream dashboards, and automatically adds/removes monitoring warnings after failed/successful refreshes. Trendplus has trust metadata but needs KPI-level evidence that survives every consumer; this is the role of RQ147. [Tableau Data Quality Warnings](https://help.tableau.com/current/online/en-us/dm_dqw.htm)
- Power BI separates data refresh and query-cache refresh, preserves refresh history including failures and warnings, and exposes execution details. Trendplus's existing requirement not to use query time as refresh time is correct; RQ141/RQ146 must prove it route by route. [Power BI refresh history](https://learn.microsoft.com/en-us/power-bi/connect-data/refresh-data)
- Shopify connects dashboard metric cards to deeper report/table exploration. Trendplus should meet the same internal consistency standard for card, table, chart, export and report, while retaining its stronger trust gating. [Shopify Analytics](https://help.shopify.com/en/manual/reports-and-analytics/shopify-reports)
- Lightspeed Retail makes reorder inputs inspectable through trailing sales period, forecast period and supplier lead time, and supports store/vendor reporting. Trendplus should expose equivalent measurement basis before presenting inventory economic conclusions. [Lightspeed Retail Analytics reports](https://retail-support.lightspeedhq.com/hc/en-us/articles/360019042893-Basic-reporting-with-Analytics-checklist)
- Amazon Forecast evaluates forecasts across backtest windows with multiple metrics and quantiles; it documents WAPE's undefined near-zero denominator and quantile loss for asymmetric under/over-forecast cost. Trendplus's RQ142 is the foundation; RQ150 supplies the missing decision-calibration layer. [Amazon Forecast accuracy metrics](https://docs.aws.amazon.com/forecast/latest/dg/metrics.html)

## Queue actions created

| Prompt | Purpose | Status |
| --- | --- | --- |
| RQ147 | Backend-owned metric evidence classes, decision tiers and parity payload | WAITING |
| RQ148 | Gross/net/return/cost basis for sales and margin metrics | WAITING |
| RQ149 | Inventory economic evidence, censored-demand handling and GMROI eligibility | WAITING |
| RQ150 | Forecast calibration by cohort, baseline and asymmetric decision cost | WAITING |
| RL12 | Causal outcome-comparison gate before impact/uplift claims | WAITING |

## Non-negotiable claim rules

- Use "observed" only for source facts whose period, scope, freshness and coverage are known.
- Use "modelled" for forecast, lost sales, risk and estimated impact; do not translate it into certainty.
- Use "causal" or "incremental impact" only with a documented treatment/control or pre-specified counterfactual, common period/scope and attrition accounting.
- Valid zero is a value only when its numerator and denominator/source are proven. Missing, unknown, stale, fallback, partial, non-finite and insufficient inputs stay distinct.
- Confidence and reliability are displayable only when their backend definition and empirical/coverage basis are available; a score label is not calibration proof.
