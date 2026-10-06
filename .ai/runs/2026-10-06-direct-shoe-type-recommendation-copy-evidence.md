Task ID: direct-shoe-type-recommendation-copy
Queue: direct-user-request
Date: 2026-10-06
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / none
Main commit SHA: pending
Main verification: pending
Evidence state: pending

## What was done
- Confirmed that `missing_comparable_signal` is an intentional backend safety gate: the pre/post nivelacija impact remains unavailable unless the same article cohort has sufficient sales evidence before and after the event.
- Removed the technical `Backend je blokirao izvrsenje preporuke` prefix for this specific shoe-type reason in the user-facing status projection.
- Passed the backend's concrete `prePostSignalNote` into the shoe-type projection and added a Serbian next-step message directing the user to verify nivelacija/sales dates or widen the period.
- Kept the recommendation blocked and preserved existing copy for unrelated recommendation blockers.

## Files changed
- `Klijent/clientapp/src/utils/shoeTypeStatusIdentity.ts`
- `Klijent/clientapp/src/utils/__tests__/shoeTypeStatusIdentity.spec.ts`
- `Klijent/clientapp/src/pages/ShoeTypeSalesStatsPage.tsx`
- `.ai/runs/2026-10-06-direct-shoe-type-recommendation-copy-evidence.md`

## Validation run
- `npm run test -- --run src/utils/__tests__/shoeTypeStatusIdentity.spec.ts src/pages/ShoeTypeSalesStatsPage.spec.tsx` -> pass, 20 tests.
- `npm run test -- --run src/utils/__tests__/shoeTypeStatusIdentity.spec.ts src/pages/__tests__/ShoeTypeSalesStatsPage.premium.spec.tsx` -> pass, 58 tests.
- `npm run check:analytics-guardrails` -> pass, including encoding check, guardrail self-test, guardrails and typecheck.
- `npm run build` -> pass; Vite production build completed with existing chunk-size warnings.
- `git diff --check` -> pass.

## Validation not run
- Live in-app Browser inspection -> not run; no browser session was available. The production URL was also checked with a direct HTTP request, which returned the SPA shell rather than the analytics API payload.
- Backend tests -> not run; no backend runtime/contract code changed.

## Documentation impact
- No product or architecture documentation changed; the durable run log records the analytics safety decision and evidence.

## What was missed
- The live response for the supplied production URL could not be inspected because the browser session was unavailable and the public Vercel URL served the frontend shell for the attempted API path.

## Risks
- The exact production row-specific note was not observable in this environment. The UI now uses the backend-provided note when available and falls back to the existing recommendation summary.
- Existing frontend test warnings from the test harness (`recharts` SVG tags and `window.scrollTo`) remain unrelated.

## Post-close routing recovery
- not applicable - direct user request

## Next
- Deploy the frontend through the repository's normal Vercel delivery path so the updated copy is visible in production.
