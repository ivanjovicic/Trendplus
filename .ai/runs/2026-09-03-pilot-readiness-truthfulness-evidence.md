Task ID: pilot-readiness-truthfulness
Queue: direct-user-request
Date: 2026-09-03
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / direct-main
Main commit SHA: 9efdbe3e028510a1e6ac1ad1e6af1ee9116860d2
Main verification: passed - fresh fetch confirms origin/main at e3ea9f8db45206bd75b21d20b0740beda7e0d319 contains implementation 9efdbe3e028510a1e6ac1ad1e6af1ee9116860d2
Evidence state: synchronized

## What was done

- Removed the duplicate technical "Podaci učitani" checklist item; the sales card already confirms the dashboard bootstrap and exposes the decision-relevant sales signal.
- Kept eight decision signals and made the summary denominator dynamic instead of hard-coded to nine.
- Made pilot intake readiness the decision-gate score. A separate health score no longer silently replaces it.
- Forced the readiness card to remain blocked when the API reports blocked recommendations, even if a legacy/contradictory score says 100.
- Rewrote readiness, freshness, action coverage and report-quality messages in Serbian, including a safe explanation for unknown refresh history and report warning/critical states.
- Removed repeated raw route text from every checklist card.
- Suppressed duration `0 s` when the refresh API has no recorded attempt/success history.
- Changed the partial-data notice so known quality/freshness blockers are not incorrectly described as a partial response.

Read-only production API verification on 2026-09-03 confirmed the displayed issue is real and not an empty dataset: intake readiness was 42/critical with 1,087 blocked recommendations, 12,422 missing categories, 12,416 insufficient signals and 656 ignored rows; refresh had no successful refresh or attempt history and workers were disabled. The health endpoint returned a separate 90-day traffic-quality score of 70/warning, so it is not used as the intake decision score.

## Files changed

- Klijent/clientapp/src/pages/PilotReadinessPage.tsx
- Klijent/clientapp/src/pages/PilotReadinessPage.css
- Klijent/clientapp/src/pages/PilotReadinessPage.integration.spec.tsx
- Klijent/clientapp/src/pages/__tests__/PilotReadinessPage.spec.tsx
- Klijent/clientapp/src/pages/__tests__/PilotReadinessPage.edgeCases.spec.ts
- Klijent/clientapp/src/components/analytics/AnalyticsRefreshStatusBanner.tsx
- Klijent/clientapp/src/components/analytics/__tests__/AnalyticsRefreshStatusBanner.spec.tsx
- .ai/runs/2026-09-03-pilot-readiness-truthfulness-evidence.md

## Validation run

- Focused regression tests before implementation -> fail as expected on the old duplicate count, contradictory quality copy, raw report code and `0 s` display.
- `npm run test -- --run src/pages/__tests__/PilotReadinessPage.spec.tsx src/pages/__tests__/PilotReadinessPage.edgeCases.spec.ts src/pages/PilotReadinessPage.integration.spec.tsx src/components/analytics/__tests__/AnalyticsRefreshStatusBanner.spec.tsx` -> pass, 4 files / 23 tests.
- `npm run check:analytics-guardrails` -> pass; encoding check, analytics guardrails and TypeScript typecheck passed.
- `npm run build` -> pass; Vite production build completed. Vite emitted existing chunk-size advisories only.
- `git diff --check` -> pass.
- Read-only GET verification against the Render Analytics API endpoints -> pass; responses and values recorded above.

## Validation not run

- Full frontend test suite -> not run; focused changed-surface tests and production build provide the narrow proof for this UI-only change.
- Browser visual inspection across every theme -> not run; CSS uses existing theme tokens and no new hard-coded status colors were introduced.

## Documentation impact

- No owner documentation required a change; durable execution evidence is recorded in this run log.

## What was missed

- No backend or Render worker configuration was changed. The production worker remains disabled and the refresh history remains empty; this task makes that state honest in the UI but does not create production refresh infrastructure.

## Risks

- Dashboard bootstrap, intake and supplier report endpoints currently expose different periods/denominators. The page now avoids treating their scores as interchangeable, but the backend contracts should eventually expose one canonical effective period for all readiness signals.
- Delivered implementation is verified on `origin/main` at 9efdbe3e028510a1e6ac1ad1e6af1ee9116860d2.

## Next

- Deploy/refresh worker configuration remains the operational follow-up; the UI now reports its absence safely.
