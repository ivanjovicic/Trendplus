# Analytics Pilot Smoke Test

Date: 2026-06-17
Repo: `ivanjovicic/Trendplus`
Audience: operator, QA, sales demo owner
Purpose: repeatable manual smoke before demo, merge or production sign-off

## Goal

This smoke test checks:
- critical backend route availability
- frontend route/render stability
- no fake green trust states
- no fake `0 RSD` on failures
- visible stale refresh warnings
- report route stability and report freshness visibility

This is a manual checklist. It does not replace:
- backend integration tests
- frontend route smoke test
- `npm run check:analytics-guardrails`

## Before You Start

Use these environment values:
- `Frontend base`: `https://trendplus.vercel.app`
- `Backend base`: `https://trendplus-api.onrender.com`

Recommended browser setup:
- open DevTools `Network`
- preserve log
- disable cache for the active tab during the run

Save evidence for every failure:
- full-page screenshot
- URL
- response status code
- response body snippet or browser console/network error
- `X-Correlation-ID` response header if present
- `meta.correlationId` from payload if present

Quick pass rule:
- critical API routes must not return `404`
- core analytics screens must not crash or show blank page
- unknown status must not appear green or healthy
- missing report data must not look ready
- stale refresh must stay visible to the user

## Route Checklist

Legend:
- Success = route is reachable and behavior matches expected healthy flow
- Warning/Empty/Error = route may still pass smoke if it fails honestly and visibly
- Never = immediate fail condition

### Backend routes

| Route | Expected success state | Expected warning / empty / error behavior | Must never happen | Next action if it fails | Save |
|---|---|---|---|---|---|
| `GET /health` | `200` response, service reports healthy process state | If unhealthy, response must still be explicit and readable; service must not hang silently | `404`, timeout without explanation, HTML error page pretending API success | Check Render service health, latest deploy, startup logs | Screenshot of response, status code, timestamp |
| `GET /ready` | `200` response, readiness payload confirms app is ready | If app is not ready, response must clearly show not-ready state; no fake healthy readiness | `404`, fake `ready=true`, hidden startup issue | Check database/recent deploy/startup probe status | Screenshot of response, status code, readiness payload |
| `GET /api/analytics/refresh-status?dataScope=all` | `200` with refresh payload for analytics freshness | Warning or error must explain stale/failed refresh; stale state must be visible, not hidden | `404`, fake fresh timestamp, stale refresh hidden | Check worker/manual refresh path and backend logs | Screenshot, payload snippet, `correlationId` |
| `GET /api/analytics/actions?dataScope=all` | `200` with action list payload or valid empty payload | Empty action queue is acceptable if clearly empty; backend error must not look like zero counts | `404`, fake zero counts on backend failure, silent HTML error | Check analytics actions endpoint, filters, backend logs | Screenshot, status, payload snippet, `correlationId` |
| `GET /api/analytics/cached/products/decision-center?fromDate=2026-05-19&toDate=2026-06-17&top=10&dataScope=all` | `200` with analytics payload or analytics error meta | Empty/insufficient data must be explicit; error meta is acceptable if route exists and explains problem | `404`, fake rows, fake `0 RSD`, missing meta on failure | Check cached analytics route mapping, data refresh, backend logs | Screenshot, response body, `meta.success`, `meta.correlationId` |

### Frontend routes

| Route | Expected success state | Expected warning / empty / error behavior | Must never happen | Next action if it fails | Save |
|---|---|---|---|---|---|
| `/analytics` | Dashboard loads with Trust Header, period, freshness and data quality context visible | Empty/error state may appear, but it must be explicit; stale refresh warning must stay visible | blank page, fake `0 RSD`, unknown shown as green, stale refresh hidden | Check `/api/analytics/refresh-status` and dashboard bootstrap requests | Full-page screenshot, Network entry for failing request, `correlationId` |
| `/analytics/pilot-readiness` | Checklist loads with visible statuses for pilot readiness steps and links to next useful screens | Unknown/warning/blocked states must look cautionary, not successful; unavailable API must not fake readiness | all-green state when data is unknown, blank page, missing links | Check refresh status, data quality and linked routes | Full-page screenshot, failed request details, visible status labels |
| `/analytics/products` | Product recommendations render with reason, action and trust context | If optional action status is unavailable, page may show non-blocking warning; if main endpoint fails, real error state must show | blank page, fake action counts, fake `0 RSD`, unknown shown as healthy | Check product decision endpoint and actions endpoint separately | Screenshot, Network for decision-center and actions calls, `correlationId` |
| `/analytics/supplier` | Supplier screen loads with period/trust context and usable tabs or report entry | Empty/insufficient data must be explicit; supplier fallback must not look like final recommendation | blank page, fake healthy supplier signal, hidden fallback warning | Check supplier data source, refresh status and linked report route | Screenshot, active tab state, failing request details |
| `/analytics/inventory` | Inventory decision view loads with actionable sections and trust context | Partial or warning data must look cautionary; error must use explicit error state | blank page, fake healthy quality state, fake `0 RSD` | Check inventory analytics requests and Data Quality page | Screenshot, failing request, visible warning/error copy |
| `/analytics/data-quality` | Data Quality screen loads with intake/readiness, issues and next actions | Empty or missing sections must be explicit; unknown must not appear ready | green/healthy copy for unknown, silent empty panel, fake zero issue counts | Check intake/report requests and refresh state | Screenshot, failing request, visible quality labels |
| `/analytics/actions` | Action queue screen loads with list or honest empty state | Empty list is acceptable if clearly shown; errors must not be masked as zero counts | blank page, fake zero queue counts, `404` hidden behind empty success | Check `/api/analytics/actions` and any summary/count calls | Screenshot, failing request, visible queue state |

### Report routes

| Route | Expected success state | Expected warning / empty / error behavior | Must never happen | Next action if it fails | Save |
|---|---|---|---|---|---|
| `/analytics/reports/pilot-intake?fromDate=2026-06-01&toDate=2026-06-30&dataScope=all` | Report page opens directly, stays stable on browser refresh, shows generated/freshness context when available | Missing report data must show unavailable or empty state clearly; no fake report rows | blank page, missing report shown as ready, hidden missing freshness info | Check route registration, report query params and underlying intake/report payload | Screenshot, URL, visible generated/freshness block, failing request |
| `/analytics/supplier/report?fromDate=2026-06-01&toDate=2026-06-30&dataScope=all` | Supplier decision report opens directly, keeps route on refresh, shows document-style trust context | Empty/unavailable report must be explicit and printable/export behavior must fail gracefully | blank page, fake rows, missing report shown as ready, stale/generated info hidden | Check supplier report route, payload, and report dependencies | Screenshot, URL, visible report header, failing request |

## What To Look For On Every Screen

Always verify these trust rules:
1. No fake `0 RSD`, `0 kom` or `0%` when the real issue is API/backend failure.
2. `Unknown`, `Nepoznato`, blocked or unavailable states must not use healthy green styling or healthy copy.
3. Freshness or stale refresh warning must be visible if refresh is old or failed.
4. Partial data must look like warning, not like fully trusted success.
5. Reports must show generation or freshness info where available.
6. Direct browser refresh on report URLs must keep the same report route and not drop to a blank page.

## Suggested Execution Order

1. Open backend `GET /health`.
2. Open backend `GET /ready`.
3. Open backend refresh status route.
4. Open backend actions route.
5. Open backend cached product decision route.
6. Open `/analytics`.
7. Open `/analytics/pilot-readiness`.
8. Open `/analytics/products`.
9. Open `/analytics/supplier`.
10. Open `/analytics/inventory`.
11. Open `/analytics/data-quality`.
12. Open `/analytics/actions`.
13. Open pilot intake report route.
14. Open supplier decision report route.
15. Re-open `/analytics/pilot-readiness` and confirm no step turned falsely green after any route failure.

## Result Template

Use this log per run:

| Item | PASS / WARN / FAIL | Notes | Evidence saved |
|---|---|---|---|
| `/health` |  |  |  |
| `/ready` |  |  |  |
| `/api/analytics/refresh-status` |  |  |  |
| `/api/analytics/actions` |  |  |  |
| `/api/analytics/cached/products/decision-center` |  |  |  |
| `/analytics` |  |  |  |
| `/analytics/pilot-readiness` |  |  |  |
| `/analytics/products` |  |  |  |
| `/analytics/supplier` |  |  |  |
| `/analytics/inventory` |  |  |  |
| `/analytics/data-quality` |  |  |  |
| `/analytics/actions` |  |  |  |
| Pilot intake report route |  |  |  |
| Supplier decision report route |  |  |  |

## Fail Escalation Rule

Stop the demo or merge sign-off if any of these happen:
- critical backend route returns `404`
- core analytics screen crashes or stays blank
- user-facing screen shows fake healthy green state while source data is unknown
- backend failure appears as fake `0 RSD`
- stale refresh is hidden from the user
- report route opens but missing report data still looks ready
