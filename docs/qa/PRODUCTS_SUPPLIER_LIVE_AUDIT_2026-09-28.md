# Live audit: Product Decision and Supplier Analytics

Date: 2026-09-28  
Queue: `direct-user-request`  
Target deployment: `https://trendplus.vercel.app`  
API host used for evidence: `https://trendplus-api.onrender.com` (the deployed Vercel bundle keeps Fly as primary and Render as fallback)  
Deployment SHA: `c5a1937f92cc363e03c4f067b4955875a531d515`

## Scope and method

Reviewed the live routes:

- [Product Decision](https://trendplus.vercel.app/analytics/products)
- [Supplier Analytics](https://trendplus.vercel.app/analytics/supplier)

The review combined the deployed Vite bundle, current `main` source, focused API requests and the nearest existing queue contracts. The native browser helper could not initialize in this environment, so browser-only pixel/layout conclusions are not classified as confirmed. API responses and source-level behavior were still independently checked against the deployed commit.

The live Render readiness and analytics-health endpoints responded successfully, so the following findings are not inferred from a dead host. They are either reproducible HTTP/contract failures or direct source-to-contract mismatches.

## Live evidence

| Surface | Request/result | Interpretation |
|---|---|---|
| Product Decision | `GET /api/analytics/cached/products/decision-center?fromDate=2026-09-01T00:00:00Z&toDate=2026-09-27T00:00:00Z&top=1200&dataScope=all` → 200; `rowCount=1200`, `analyzedRows=12422`, `ignoredRowsCount=11222`, `ignoredRowsMeaning=hidden_by_top_limit` | The response makes two populations explicit, but the page does not present both clearly. |
| Product Decision search | Same endpoint with `search=ZZZ_NO_SUCH_PRODUCT_998877` → 0 rows; current client wrapper does not append `search` | Backend search exists; the deployed frontend drops the option passed by the page. |
| Product action status | The page can send 1,200 lookup items; `POST /api/analytics/actions/status` rejects more than 1,000 | The queue-status request is outside its backend contract. |
| Product recommendation state | Product rows are `INSUFFICIENT_DATA`/blocked and summary action counts are zero; source sets `HasCompleteJournal = false` | No actionable recommendation can be emitted until journal completeness has a proven source. |
| Supplier overview | `GET /api/analytics/supplier-sales-stats?...&dataScope=all` → 503 with a safe generic problem response | Runtime failure is available to the UI, but the page must keep it distinct from a valid empty dataset and preserve retry/correlation semantics. |
| Supplier scorecard | `GET /api/analytics/suppliers/decision-hub/summary?...` → 200 transport, `meta.success=false`, `errorCode=MISSING_SCHEMA`, effective fallback 90d, `rowCount=0`, `recommendationAllowed=false` | The screen fails closed, but the live precomputed dataset/readiness gate is not customer-actionable enough. |
| Supplier assortment | `GET /api/analytics/vendor-sales-nivelacija?...` → `meta.success=false`, `vendor_sales_nivelacija_contract_missing`, `scopeApplied=false`, empty arrays/totals | Missing semantic contract is reported, but the tab needs a durable readiness/error state and an operational owner. |

## Confirmed findings and queue mapping

| ID | Confirmed finding | Impact | Queue prompt |
|---|---|---|---|
| F1 | Product action-status effect posts 1,200 items to a 1,000-item endpoint cap | Queue badges degrade or remain unavailable; unrelated freshness messaging can be triggered | RQ461 |
| F2 | `ProductDecisionCenterPage` passes `search` but `getProductDecisionCenter` has no `search` option/URL append | Products outside the top cap cannot be found; this is a regression of RQ200 | RQ462 |
| F3 | Product status/risk KPI counts use returned rows while money summaries use all analyzed rows; the distinction is not visible enough | Counts and money can appear to describe one population when they do not | RQ463 |
| F4 | `HasCompleteJournal` is hard-coded false | All product recommendations remain blocked and “good” evidence quality is unreachable | RQ464 |
| F5 | Product margin uses a different cost fallback/coverage rule and total-revenue denominator than Supplier overview | Equivalent margin labels can disagree across decision surfaces | RQ465 |
| F6 | Supplier overview can return 503; the page needs a distinct error, empty and retry contract | Operators can misread unavailable supplier analytics as “no suppliers” | RQ466 |
| F7 | Scorecard and assortment are transport-successful but semantic-data unavailable (`MISSING_SCHEMA` / contract missing) | Customer sees a technical readiness problem without a stable owner-facing recovery path | RQ467 |
| F8 | Supplier raw API share fields use signed total revenue while current overview/UI concentration uses positive-revenue visible-population denominator; Hub also filters non-positive rows | Raw API, recommendations, export and cards can disagree on share semantics, especially with returns/negative suppliers or unknown suppliers | RQ468 |

## Cross-screen comparison

| Question | Product Decision | Supplier overview | Supplier Decision Hub | Supplier assortment |
|---|---|---|---|---|
| Primary population | Up to `top` returned rows, with a larger analyzed population in metadata | Visible supplier rows after scope/filter | Backend-owned decision summary/scorecard population | Pre/post supplier/type population |
| Current denominator | Backend explicitly separates returned-row counts from analyzed-row money | Positive revenue of the visible population; unknown can be included when shown | Positive revenue rows for the decision summary | No valid denominator when the semantic contract is missing |
| Failure/blocked state | Row recommendation is blocked by insufficient evidence | 503 is a real failure and must not become empty | `meta.success=false`, no recommendation allowed | Contract missing, fail closed |
| Main parity risk | Local KPI counts and action-status lookup do not share one visible population | Raw API share fields differ from display projection | Summary is authoritative but has a different subset than overview | Availability is not comparable until schema/readiness is proven |

The most important alignment rule is to label population and denominator on every KPI, rather than force the screens to have identical numbers when they intentionally answer different questions. The product page should not count an action as executable when its row is blocked. Supplier overview and Hub may keep different cohorts, but their share fields must carry one explicit positive/negative/unknown policy.

## Existing work not duplicated

- `RQ200` is DONE, but F2 is a demonstrated client regression and therefore has a bounded follow-up (`RQ462`), not a reopening of the original delivery.
- `RQ233`, `RQ373` and `RQ443` remain the owners of the already-delivered Supplier visible-population, concentration and total-PoP contracts. `RQ468` covers the remaining raw-API/recommendation/export contract mismatch.
- `RQ444` owns Supplier period truth and `RQ459` owns Decision Hub KPI/chart/report parity; neither proves live scorecard schema readiness, so `RQ467` is an operational follow-up.
- Historical hypotheses about duplicate layout blocks, exact pixel overflow, local date widgets and minor copy are not promoted here because the browser renderer was unavailable and source evidence alone does not prove a live user-visible defect.

## Queue delivery

Eight bounded prompts were registered in `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md` as `RQ469`–`RQ476` (the intervening IDs already belong to existing Supplier report/Pilot intake work). They remain `WAITING`; this direct audit requested analysis and queue registration, not an implementation claim. The existing `Current READY prompt` is `RQ461` (with the existing parallel READY entries `RQ462` and `RQ466`); this audit did not promote a new prompt.

