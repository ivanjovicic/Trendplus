# Pilot Release Evidence Refresh - 2026-08-22 (STAB15)

Repo: `ivanjovicic/Trendplus`
Smoke target: exact deployed production runtime and live frontend bundle
Owner prompt: `STAB15`
Related: `docs/qa/GENAI_EVALUATION_AND_RELEASE_GATE.md`, `docs/qa/PILOT_RELEASE_EVIDENCE_REFRESH_2026-08-22.md`, `docs/qa/ANALYTICS_PILOT_SMOKE_TEST.md`, `MASTER_ROADMAP.md`

## Verdict

| Gate | Verdict | Why |
|---|---|---|
| Live smoke pack | **PASS** | The exact deployed backend SHA and current frontend bundle were exercised against the canonical production route/filter matrix. Routes rendered real content or explicit honest degraded states, not shell-only false positives. |
| Core pilot | **NOT READY** | STAB15 proves the exact deployed runtime is truthful on the smoke surface, but the broader backend current-main gate remains the higher-priority blocker. |
| GenAI entry (`GAI01`) | **BLOCKED** | The core pilot is still not cleared because the backend current-main truth remains red. |

## Environment

- Frontend base: `https://trendplus.vercel.app`
- Backend base: `https://trendplus-api.onrender.com`
- Backend runtime version observed: `d9c4d0a8cd893c8e7cb330f47e41e92843fa9875`
- Frontend bundle observed: `/assets/index-HJjiguak.js`

## Evidence index

| Area | Current truth | Observed smoke result |
|---|---|---|
| Health | backend healthy | `GET /health` returned `200` with `status=healthy`, `provider=render`, `ready=true` |
| Readiness | backend ready | `GET /ready` returned `200` with `db.ok=true`, `db.latencyMs=2400`, and `reason=ready` |
| Runtime version | exact deployed backend SHA | `GET /api/runtime/version` returned `commitSha=d9c4d0a8cd893c8e7cb330f47e41e92843fa9875` |
| Refresh status | honest degraded freshness | `GET /api/analytics/refresh-status?dataScope=all` returned `dataFreshnessStatus=unknown`, workers disabled, and an in-memory cache warning |
| Action queue | real data-bearing response | `GET /api/analytics/actions?dataScope=all` returned 4 items |
| Product decision center | real data-bearing response | `GET /api/analytics/cached/products/decision-center?fromDate=2026-08-01&toDate=2026-08-22&top=10&dataScope=all` returned `totalRows=50`, `analyzedRows=12422`, and populated rows |
| Dashboard | honest rendered content | `/analytics` rendered the real dashboard content on `/assets/index-HJjiguak.js` |
| Pilot readiness page | honest rendered content | `/analytics/pilot-readiness` rendered the real checklist content on `/assets/index-HJjiguak.js` |
| Product decisions | honest rendered content | `/analytics/products` rendered the real product decision content on `/assets/index-HJjiguak.js` |
| Supplier view | honest rendered content | `/analytics/supplier` rendered the real supplier content on `/assets/index-HJjiguak.js` |
| Inventory view | honest rendered content | `/analytics/inventory` rendered the real inventory content on `/assets/index-HJjiguak.js` |
| Data quality | honest rendered content | `/analytics/data-quality` rendered the real data-quality content on `/assets/index-HJjiguak.js` |
| Actions view | honest rendered content | `/analytics/actions` rendered the real action queue content on `/assets/index-HJjiguak.js` |
| Decision board | honest rendered content | `/analytics/decision-board` rendered the real decision-board content on `/assets/index-HJjiguak.js` |
| Pilot intake report | honest rendered content | `/analytics/reports/pilot-intake?fromDate=2026-08-01&toDate=2026-08-22&dataScope=all` rendered the report page on `/assets/index-HJjiguak.js` |
| Supplier report | honest rendered content | `/analytics/supplier/report?fromDate=2026-08-01&toDate=2026-08-22&dataScope=all` rendered the report page on `/assets/index-HJjiguak.js` |

## Explicit non-claims

- This smoke is synchronized to the exact deployed runtime SHA above, but it does not clear the higher-priority backend current-main gate.
- This smoke does not prove `main` itself is green on the backend CI workflow.
- GenAI remains blocked until the canonical gate is explicitly reopened.

## Minimum clear path after this smoke

1. Keep BCI current-main truth separate from STAB release-truth evidence.
2. Recheck the backend current-main gate if you need full release truth for GenAI.
3. Keep this STAB15 pack as the exact-deploy smoke proof for the current production runtime.
