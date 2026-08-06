# Analytics UI Visual Review Evidence Template

Copy this file or paste the tables into a PR description when a UI premium change requires `P-UI-05` verification.

Do not commit real customer screenshots into `docs/` unless sanitized.

## Session metadata

| Field | Value |
|---|---|
| Date (UTC) | |
| Git SHA | |
| Branch | |
| Reviewer | |
| Base URL | |
| Browser / OS | |
| Protocol version | `docs/Frontend/ANALYTICS_VISUAL_REGRESSION_PROTOCOL.md` (2026-08-06) |

## Automated checks

| Command | Result |
|---|---|
| `npm run test -- --run src/__tests__/AppAnalyticsRoutes.spec.tsx` | pass / fail / not run |
| `npm run check:analytics-guardrails` | pass / fail / not run |

## Viewport × theme coverage

| Viewport | Light | Dark |
|---|---|---|
| mobile `375` | | |
| tablet `768` | | |
| desktop `1280` | | |

## Surface results

Use IDs from the protocol. Mark `PASS` / `FAIL` / `N/A`.

### A. Chrome

| ID | Light | Dark | Notes / screenshot path |
|---|---|---|---|
| A1 Sidebar expanded | | | |
| A2 Sidebar collapsed | | | |
| A3 Sidebar mobile | | | |
| A4 Header desktop | | | |
| A5 Header tablet | | | |
| A6 Header mobile | | | |

### B. Trust / overview

| ID | Light | Dark | Notes / screenshot path |
|---|---|---|---|
| B1 Trust recommendation | | | |
| B2 Trust signal | | | |
| B3 Trust report | | | |
| B4 Dashboard overview | | | |

### C. Export / tables

| ID | Light | Dark | Notes / screenshot path |
|---|---|---|---|
| C1 Export toolbar | | | |
| C2 Export modal | | | |
| C3 Product table | | | |
| C4 Inventory table | | | |
| C5 Supplier table | | | |
| C6 Data quality table | | | |

### D. Optional

| ID | Light | Dark | Notes / screenshot path |
|---|---|---|---|
| D1 Decision board | | | |
| D2 Actions | | | |
| D3 Pilot readiness | | | |

## Failures

| ID | Theme | Viewport | Issue | Follow-up |
|---|---|---|---|---|
| | | | | |

## Verdict

- [ ] PASS — all required surfaces OK for this change scope
- [ ] PARTIAL — required subset OK; list deferred IDs
- [ ] FAIL — blocking visual regression

**Verdict:**  

**Deferred / out of scope for this PR:**  
