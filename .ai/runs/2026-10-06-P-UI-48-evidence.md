Task ID: P-UI-48
Queue: docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md
Date: 2026-10-06
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / none
Main commit SHA: pending
Main verification: pending post-delivery verification
Evidence state: pending

## What was done
- Removed worker/Redis write buttons and the browser-local API-ping control from the business header. The header retains read-only worker/Redis status and links to the admin/operations surface.
- Moved worker and Redis writes to `/admin/configuration`; each write now explains its server-side consequence, requires explicit confirmation and a password-field admin key, and sends that key through the existing `X-Admin-Key` contract. No frontend role simulation or backend authorization change was introduced.
- Kept the API-ping preference on the health/diagnostics panel and labeled it as a browser-local pause/resume that does not stop the API service.
- Added an accessible first-tab-stop “Preskoči na sadržaj” link to the `main` landmark, full accessible labels for truncated breadcrumb links, and a descriptive accessible name for the header overflow control.
- Added focused tests for status-only header behavior, worker/Redis confirmation and admin-key transport, browser-local API preference, skip-link focus and worker request headers.

## Files changed
- `Klijent/clientapp/src/components/AdminActionConfirmModal.tsx`
- `Klijent/clientapp/src/components/ApiPingFlag.tsx`
- `Klijent/clientapp/src/components/RedisToggleFlag.tsx`
- `Klijent/clientapp/src/components/WorkerControlFlag.tsx`
- `Klijent/clientapp/src/components/__tests__/OperationalFlags.spec.tsx`
- `Klijent/clientapp/src/layout/AppLayout.tsx`
- `Klijent/clientapp/src/layout/AppLayout.spec.tsx`
- `Klijent/clientapp/src/layout/components/HeaderStatus.tsx`
- `Klijent/clientapp/src/layout/components/__tests__/HeaderStatus.spec.tsx`
- `Klijent/clientapp/src/pages/ConfigurationPage.tsx`
- `Klijent/clientapp/src/services/workersApi.ts`
- `Klijent/clientapp/src/services/__tests__/workersApi.spec.ts`
- `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md` and six active RQ addenda with stale supplemental-claim pointers refreshed
- `MASTER_ROADMAP.md`
- `.ai/runs/2026-10-06-P-UI-40-evidence.md` (post-close recovery synchronized)
- `.ai/runs/2026-10-06-P-UI-48-evidence.md`

## Validation run
- `npm run test -- --run src/layout/AppLayout.spec.tsx src/layout/components/__tests__/HeaderStatus.spec.tsx src/components/__tests__/OperationalFlags.spec.tsx src/services/__tests__/workersApi.spec.ts` -> pass, 17/17.
- `npm run check:analytics-guardrails` -> pass; 39 known baseline findings, 0 removed; encoding check and typecheck passed.
- `npm run build` -> pass; existing >500 KB Recharts chunk warning remains.
- `npm run responsive:baseline -- --route-ids app_shell --mode fixture --output-dir %TEMP%/trendplus-pui48-shell --strict --viewport-only` -> pass, 20 cases over two themes and ten widths including 375/768/1280; 0 root overflow observations and 0 page errors.
- `node scripts/check-agent-instructions.mjs --self-test` and `node scripts/check-agent-instructions.mjs` -> pass.
- `node scripts/check-prompt-queues.mjs --self-test` and `node scripts/check-prompt-queues.mjs` -> pass, 709 tasks.
- `node scripts/check-planning-architecture.mjs --self-test` and `node scripts/check-planning-architecture.mjs` -> pass, 80 tasks.
- `git diff --check` -> pass.

## Validation not run
- Full local Vitest suite -> not run; focused shell/operational suites cover the touched controls and the responsive shell matrix covers the affected header widths.
- Live production admin-key acceptance -> not run; no production write was attempted. The UI sends the credential to the existing server-side `AdminAccessControl` boundary.
- Remote CI for this implementation -> not yet inspected; inspect the current-main run after delivery before final evidence.

## Documentation impact
- Updated the UI queue claim, cross-program current pointer and stale RQ supplemental pointers.
- Synchronized P-UI-40's post-close recovery evidence at base `72f85d81fda2626f02d15a66837760a8b1997fb3`.

## What was missed
- None known.

## Risks
- The backend remains the authority for `X-Admin-Key`; production key availability is not established by local UI tests. Invalid or unavailable credentials remain rejected by the existing server boundary.
- The production build retains its existing chunk-size warning; analytics guardrails retain 39 baseline findings.

## Post-close routing recovery
- Pending: after P-UI-48 reaches `main`, refresh `origin/main`, scan the full active RQ/SQL/UI queue/addendum set and promote/claim a collision-safe successor or record the required Zero-READY proof.

## Next
- Pending post-close recovery.
