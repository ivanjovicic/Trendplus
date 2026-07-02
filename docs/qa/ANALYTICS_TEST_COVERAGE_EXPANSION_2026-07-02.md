# Analytics Test Coverage Expansion — 2026-07-02

Repo: `ivanjovicic/Trendplus`
Scope: frontend analytics tests, shared analytics controls, accessibility and readiness safety states
Status: implemented; runtime verification still required

## What was added

### Pilot Readiness page-level coverage

File:

- `Klijent/clientapp/src/pages/PilotReadinessPage.integration.spec.tsx`

Commit:

- `859a7447feea9c07d68aa84ca7ec12a721a8ffed`

Covered workflows:

- all nine readiness cards are rendered from confirmed sources;
- fully ready state maps to `good` and does not show false empty/error states;
- blocking data-quality issues map to a blocked pilot state;
- one failed optional source keeps confirmed cards while exposing partial status;
- all-null responses remain unknown and never become green;
- total source failure shows a global error and retry reloads every source.

### Pilot Readiness edge-state coverage

File:

- `Klijent/clientapp/src/pages/__tests__/PilotReadinessPage.edgeCases.spec.ts`

Commits:

- `9444139d5a25f0fb4637d96b3be2cf5f2d76bfa1`
- `9de4b2c8852a55d12dbcda6259edf848c6251141`

Covered branches:

- blocking data-quality issues;
- critically old analytics refresh;
- disabled workers while freshness claims `fresh`;
- empty/insufficient Product Decision response;
- unavailable action data versus a confirmed empty action queue;
- fallback report warning versus critical report blocking.

### Shared Modal accessibility hardening

File:

- `Klijent/clientapp/src/components/Modal.tsx`

Commit:

- `8ebdeebbdf330c1515dda275cbd4afbe0050f18f`

Changes:

- unique title IDs through `useId`;
- reliable `aria-labelledby` dialog naming;
- disabled controls are excluded from the focus-trap sequence;
- close button explicitly uses `type="button"`;
- backdrop is hidden from assistive technology;
- Escape close, body scroll lock and trigger focus restoration remain protected.

### Analytics export menu accessibility hardening

File:

- `Klijent/clientapp/src/components/analytics/AnalyticsTableToolbar.tsx`

Commit:

- `b5681782b851a2d426214fe97954b15672c8688f`

Changes:

- export trigger exposes `aria-haspopup`, `aria-expanded` and `aria-controls`;
- export format list uses `menu` / `menuitem` semantics;
- first item receives focus when the menu opens;
- Arrow Up/Down, Home, End and Escape are supported;
- Escape returns focus to the export trigger;
- clicking outside closes the menu;
- export completion/error copy is exposed through a polite live `status` region.

### Analytics toolbar tests aligned with accessible behavior

File:

- `Klijent/clientapp/src/components/analytics/__tests__/AnalyticsTableToolbar.spec.tsx`

Commit:

- `c0406721953464d57be352ef79b60c1906b57f34`

The existing export/print contract tests now use the actual accessible menu roles and validate the live status result.

### Dedicated analytics accessibility tests

File:

- `Klijent/clientapp/src/components/analytics/__tests__/AnalyticsAccessibility.spec.tsx`

Commit:

- `2c70a0a0d182cdaa09af2c15c1bc6450c9e37169`

Covered interactions:

- dialog accessible name and `aria-modal`;
- initial close-button focus;
- forward and reverse Tab wrapping;
- Escape close and trigger-focus restoration;
- backdrop close and `aria-hidden`;
- export menu semantics;
- Arrow/Home/End/Escape navigation;
- accessible export form labels;
- outside-click menu close.

### InfoTip keyboard and ARIA tests

File:

- `Klijent/clientapp/src/components/ui/InfoTip.spec.tsx`

Commit:

- `8dc8321705855de962aaf4f5fe58f02d09673a08`

Covered interactions:

- focus opens the tooltip after the configured delay;
- trigger receives `aria-describedby` while tooltip is visible;
- Escape closes the tooltip and restores focus;
- Enter and Space toggle the tooltip;
- blur removes tooltip content after the hide transition.

## Test command

From `Klijent/clientapp`:

```bash
npm run test:analytics
npm run check:analytics-guardrails
npm run build
```

The existing `test:analytics` command includes `src/components/analytics`, `src/services/__tests__` and `src/pages`, so all new analytics test files are in the targeted run.

## Verification status

The tests were not executed successfully in this connector session.

A local verification attempt could not clone/install because the execution environment could not resolve `github.com` through DNS. No pass rate or coverage percentage is claimed.

At the last status check, the newest commit had a pending Vercel status. An earlier intermediate commit showed a Vercel failure, but deployment logs were not available through the connector, so its cause was not inferred.

## Required next quality gate

After a machine with repository/network access runs the suite:

1. Fix every failing test, typecheck or build result before merging.
2. Run a real V8 coverage report for the targeted analytics suite.
3. Record the baseline separately for statements, branches, functions and lines.
4. Add a non-decreasing coverage threshold only after the baseline is known.
5. Raise thresholds incrementally as page-level, API-contract, accessibility and browser smoke coverage expands.

Do not select an arbitrary percentage before the first real report. A trustworthy measured baseline is better than a nominal high threshold that the suite cannot currently prove.
