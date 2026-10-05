Task ID: P-UI-48
Queue: docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md
Date: 2026-10-06
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / none
Main commit SHA: pending regression-test follow-up delivery
Main verification: original implementation is on main at `575af2ab3f48eb9668477b9b95b90928b4a61aec`; verify the follow-up after push
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
- Regression follow-up `npm run test -- --run src/pages/__tests__/ConfigurationPage.spec.tsx` -> pass, 5/5.
- CI-failed case recheck `npm run test -- --run src/pages/__tests__/PreNivelacijaPriorityPage.spec.tsx -t "shows the backend evidence window in the trust header"` -> pass, 1 passed / 55 skipped.

## Validation not run
- Full local Vitest suite -> not run; focused shell/operational suites cover the touched controls and the responsive shell matrix covers the affected header widths.
- Live production admin-key acceptance -> not run; no production write was attempted. The UI sends the credential to the existing server-side `AdminAccessControl` boundary.
- Remote Planning Governance run `37386822805` -> green on `575af2ab3f48eb9668477b9b95b90928b4a61aec`.
- GitHub Analytics Quality Gates run `37386823059` -> completed red on implementation SHA `575af2ab3f48eb9668477b9b95b90928b4a61aec`; its ConfigurationPage assertion was corrected in this follow-up. The separate PreNivelacija failure passed locally in isolation; exact-main CI for the correction remains pending.

## Documentation impact
- Updated the UI queue claim, cross-program current pointer and stale RQ supplemental pointers.
- Synchronized P-UI-40's post-close recovery evidence at base `72f85d81fda2626f02d15a66837760a8b1997fb3`.

## What was missed
- None known.

## Risks
- The backend remains the authority for `X-Admin-Key`; production key availability is not established by local UI tests. Invalid or unavailable credentials remain rejected by the existing server boundary.
- The production build retains its existing chunk-size warning; analytics guardrails retain 39 baseline findings. Exact-main CI for the test-only correction remains pending.

## Post-close routing recovery
- Recovery base `origin/main` SHA: `9663b4b8be7624be47dd4585ffd40dbb34a40cd6` (fresh fetch after P-UI-48 closure metadata).
- Active owner queue/addendum set scanned: 12 `ANALYTICS_RELIABILITY_PROMPT_QUEUE*.md` files, `SQL_ANALYTICS_PROMPT_QUEUE.md`, `ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md`, `ANALYTICS_UI_PREMIUM_PROMPT_QUEUE_LEAST_IMPROVED_ADDENDUM.md`, and `MASTER_ROADMAP.md` (16 files).
- Completed/changed dependency IDs searched: P-UI-48 and RQ576; P-UI-48 is implemented on main at `575af2ab3f48eb9668477b9b95b90928b4a61aec`; RQ576 is DONE on main at `e5e2dc83bf73eee8b34f0b7e3565afa272625535`.
- Re-evaluated dependent/candidate UI prompts: P-UI-41 was dependency-complete and collision-safe once RQ576's distinct Inventory-page work landed; P-UI-45 became READY after P-UI-48; P-UI-42 remains WAITING for P-UI-47; P-UI-31/P-UI-35/P-UI-36 remain WAITING for P-UI-47; P-UI-43/P-UI-44 remain gated by RQ569; P-UI-53 remains gated by P-UI-47 plus P-UI-31/35/36; P-UI-38 remains the final responsive/theme/a11y gate. Independent READY lanes include P-UI-47/P-UI-49/P-UI-51/P-UI-52. No zero-READY claim applies.
- CI recovery after that closure found an in-scope P-UI-48 test assertion defect; P-UI-48 resumed and P-UI-41 returned to READY before implementation. This does not invalidate the dependency scan or require a zero-READY proof.

## Next
- Deliver the focused ConfigurationPage regression-test correction to `main`, inspect the resulting current-main CI once, then close and refresh routing.
