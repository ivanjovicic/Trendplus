# Analytics Visual Regression Protocol

Date: 2026-08-06  
Repo: `ivanjovicic/Trendplus`  
Task: `P-UI-05`  
Audience: agents and humans doing premium UI / layout work on analytics surfaces

## Purpose

Premium UI changes must be verified as **rendered pixels**, not only by route smoke or unit tests. This protocol is the repeatable gate before broad visual refactors (`P-UI-06` onward).

There is **no** Playwright / Storybook screenshot harness in `Klijent/clientapp` today (`vitest` only). Until an automated harness is added, validation is:

1. automated route smoke (non-visual)
2. **manual screenshot checklist** (this document)
3. optional evidence file filled per PR / session

## When this protocol is required

Run it when the change touches any of:

- sidebar / navigation chrome
- global header / top bar
- analytics TrustHeader / trust strip
- analytics dashboard overview
- export toolbar / export modal
- shared table density, sticky headers, numeric columns
- theme tokens / dark-light appearance
- inventory / product / supplier / data-quality table layout

Skip only for pure backend, docs-only, or non-UI copy that cannot affect layout.

## Automated baseline (not enough alone)

```powershell
cd Klijent/clientapp
npm run test -- --run src/__tests__/AppAnalyticsRoutes.spec.tsx
npm run check:analytics-guardrails
```

Route smoke proves routes render without crashing. It does **not** prove visual layout, theme contrast, or overflow.

Do **not** change `ThemeProvider defaultTheme` unless the task is explicitly theme/design-system work (`docs/Frontend/ROUTING_AND_SMOKE_TEST_STANDARDS.md`).

## Viewport matrix

Capture each required surface at these widths (height flexible; use full-page or main chrome + primary content):

| Name | Width | Notes |
|---|---:|---|
| mobile | `375` | sidebar collapsed / drawer; no horizontal scroll on primary content |
| tablet | `768` | header + filters usable; tables may scroll horizontally with sticky first column if designed that way |
| desktop | `1280` | sidebar expanded; tables readable without crushing trust header |

Record actual device pixel ratio if not 1x (e.g. Retina). Prefer Chromium (Chrome/Edge) as the reference browser.

## Theme matrix

For every surface in the checklist below, capture **both**:

| Theme | How to set | Expectations |
|---|---|---|
| light | app theme control / `ThemeProvider` light | text readable on backgrounds; borders use theme tokens; no hard-coded dark-only colors |
| dark | app theme control / dark | same structure; no washed-out badges; trust tones still distinguishable; no white flashes on chrome |

Theme switch must not change business semantics (recommendation labels, fake-zero rules).

## Surfaces and routes checklist

Mark each cell: `PASS` / `FAIL` / `N/A` (with reason). Failures need a short note + screenshot path.

### A. Application chrome

| ID | Surface | Route / action | What to verify |
|---|---|---|---|
| A1 | Sidebar expanded | any `/analytics/*` desktop | brand/nav readable; active item clear; no overlap with content |
| A2 | Sidebar collapsed | desktop toggle if available | content gains width; icons still actionable |
| A3 | Sidebar mobile | `375` width | drawer/overlay works; content not trapped under overlay after close |
| A4 | Global header desktop | `/analytics` | title/actions aligned; no overflow; theme toggle reachable |
| A5 | Global header tablet | `/analytics` @ `768` | controls wrap cleanly; no clipped CTAs |
| A6 | Global header mobile | `/analytics` @ `375` | hamburger/menu OK; no horizontal page scroll from header |

### B. Trust and overview

| ID | Surface | Route / action | What to verify |
|---|---|---|---|
| B1 | Trust header — recommendation mode | page with recommendations (e.g. `/analytics/products` or decision board) | status/confidence/DQ visible; not fake-green on error |
| B2 | Trust header — signal mode | inventory or signal-heavy page | signal tones readable in both themes |
| B3 | Trust header — report mode | `/analytics/reports/pilot-intake?...` or supplier report | period/freshness/methodology visible |
| B4 | Analytics dashboard overview | `/analytics` | above-the-fold hierarchy: KPIs / actions / trust not colliding; empty/error explicit |

### C. Export and tables

| ID | Surface | Route / action | What to verify |
|---|---|---|---|
| C1 | Export toolbar / menu | open export on products or supplier | menu/modal aligned; focus trap OK; no clipped actions |
| C2 | Export modal | open modal if present | title, period, confirm/cancel; theme tokens |
| C3 | Product decision table | `/analytics/products` | sticky header if expected; numeric columns right-aligned; trust columns not truncated into nonsense |
| C4 | Inventory table | `/analytics/inventory` | density readable; filters not covering table; scroll behavior OK |
| C5 | Supplier table | `/analytics/supplier` | ranking/detail columns consistent; no unit confusion in headers |
| C6 | Data quality table | `/analytics/data-quality` | issues/impact readable; readiness card not colliding |

### D. Optional high-traffic companions (if PR touches them)

| ID | Surface | Route |
|---|---|---|
| D1 | Executive decision board | `/analytics/decision-board` |
| D2 | Actions queue | `/analytics/actions` |
| D3 | Pilot readiness | `/analytics/pilot-readiness` |

## Pass / fail rules

**PASS** only if all of the following hold for the captured viewport+theme:

- primary content is visible without being covered by chrome
- no unintended horizontal scroll on the page root (table internal scroll OK)
- theme uses CSS variables / design tokens (no obvious hard-coded clash)
- error/empty/warning states remain visually cautionary (not success-green)
- text contrast is usable at a glance (no unreadable gray-on-gray)

**FAIL** if any required surface regresses layout, contrast, clipping, or trust presentation.

## Evidence fields (copy into PR or evidence file)

Use `docs/qa/ANALYTICS_UI_VISUAL_REVIEW_EVIDENCE_TEMPLATE.md`.

Minimum fields:

| Field | Example |
|---|---|
| Date (UTC) | `2026-08-06T07:00:00Z` |
| SHA / branch | `abc123` / `main` |
| Reviewer | name or agent id |
| Browser | Chrome 128 / Edge |
| Base URL | `http://localhost:5173` or staging |
| Automated commands run | route smoke + guardrails |
| Theme × viewport matrix completed | yes/no |
| Screenshot folder | `tmp/ui-visual/<sha>/` (local only; do not commit customer data) |
| Failures | IDs + one-line notes |
| Verdict | `PASS` / `FAIL` / `PARTIAL` |

## Screenshot naming convention

```text
tmp/ui-visual/<sha-or-date>/<surface-id>__<theme>__<viewport>.png
```

Examples:

```text
tmp/ui-visual/2026-08-06/A1__light__desktop.png
tmp/ui-visual/2026-08-06/C3__dark__mobile.png
```

Do **not** commit screenshots that contain real customer metrics unless explicitly sanitized. Prefer local `tmp/` (ignored) + written PASS/FAIL notes in the PR.

## Future automation (out of scope for P-UI-05 runtime)

When adding Playwright later:

- keep this checklist as the coverage contract
- map each `ID` to a screenshot assertion
- run light+dark via theme attribute/class, not by changing `defaultTheme` in production code
- gate premium UI PRs on the same surface list

## Related docs

- `docs/qa/ANALYTICS_UI_PREMIUM_AUDIT.md` (P-UI-05 recommendation)
- `docs/Frontend/ROUTING_AND_SMOKE_TEST_STANDARDS.md`
- `docs/qa/ANALYTICS_UI_VISUAL_REVIEW_EVIDENCE_TEMPLATE.md`
- `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md`
