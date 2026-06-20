# Trendplus Common Failures and Fixes

## How to use this playbook

- Start with the symptom you see in UI, tests, deploy status, or docs.
- Confirm the detection step before editing code or docs.
- Apply the correct fix, not the fastest-looking workaround.
- Run the required checks listed for that failure type.
- If the same failure repeats again later, update the canonical doc linked from this playbook.

## 1. Mojibake / broken UTF-8

- Symptom: Serbian text shows corrupted characters such as `DobavljaÄ` or `marÅ¾a`.
- Detection: Search for `Ä|Å|â|�|DobavljaÄ|marÅ|osveÅ|uÄ|Å¡|Å¾`.
- Root cause: File encoding drift, copy/paste through the wrong encoding, or shell/editor mismatch.
- Correct fix: Re-save the file as UTF-8 and repair only the corrupted text.
- Wrong fix: Mixing encoding cleanup with unrelated business-logic refactors.
- Required checks: `cd Klijent/clientapp && npm run check:analytics-guardrails`, `cd Klijent/clientapp && npm run build`
- Usually touched files: docs, page copy, report copy, shared empty/error states

## 2. Local formatter drift

- Symptom: A page defines `formatCurrency`, `formatPercent`, `fmtRsd`, or `fmtPct` locally.
- Detection: Search the changed page/component for local formatter definitions.
- Root cause: Developer bypassed shared helpers for a one-off display need.
- Correct fix: Reuse `src/utils/analyticsFormatters.ts`.
- Wrong fix: Copy-pasting yet another formatter into a nearby file.
- Required checks: `cd Klijent/clientapp && npm run check:analytics-guardrails`, `cd Klijent/clientapp && npm run build`
- Usually touched files: `src/pages/*`, `src/components/*`, `src/utils/analyticsFormatters.ts`

## 3. Fake zero / fake green

- Symptom: Broken backend data still renders `0 RSD`, `0%`, green KPIs, or a healthy-looking status.
- Detection: Inspect API response and UI state path; look for empty success assumptions after an error.
- Root cause: Error path was mapped to default numeric values or green fallback styling.
- Correct fix: Backend returns `Problem` or `AnalyticsResponseMetaFactory.Error(...)`; frontend shows `AnalyticsErrorState` or warning state.
- Wrong fix: Hiding the broken panel while still rendering zero-valued KPI cards elsewhere.
- Required checks: backend tests where relevant, `npm run check:analytics-guardrails`, `npm run build`
- Usually touched files: endpoint handlers, page state mapping, analytics meta helpers

## 4. Empty vs error confusion

- Symptom: UI says “no data” when the backend actually failed, or shows an error for a genuinely empty dataset.
- Detection: Compare `meta.success`, `emptyReason`, `dataQualityStatus`, and the raw network response.
- Root cause: Empty and error states were collapsed into one branch.
- Correct fix: Use `AnalyticsEmptyState` for true empty or insufficient states and `AnalyticsErrorState` for failures.
- Wrong fix: Returning `[]` from a catch block and treating that as success.
- Required checks: targeted page tests, `npm run build`
- Usually touched files: frontend page loaders, `analyticsResponseMeta.ts`, analytics endpoints

## 5. Supplier scorecard silent fallback

- Symptom: A requested period silently widens to a different dataset without warning.
- Detection: Compare requested period, effective dataset, and `recommendationAllowed`.
- Root cause: Backend fallback happened without trust metadata or frontend visibility.
- Correct fix: Preserve requested/effective dataset, fallback reason, and block final recommendation when needed.
- Wrong fix: Showing the fallback result as if it were the requested range.
- Required checks: supplier page tests, supplier endpoint tests, guardrails/build
- Usually touched files: `SupplierDecisionHubEndpoints.cs`, supplier pages, supplier report docs/tests

## 6. Worker/refresh confusion

- Symptom: Users cannot tell whether analytics is fresh, stale, unknown, or currently refreshing.
- Detection: Compare worker status, refresh-status response, and page trust header/banner.
- Root cause: Refresh ownership or freshness mapping was hidden or inferred locally.
- Correct fix: Use `/api/analytics/refresh-status`, worker status surfaces, and honest freshness states.
- Wrong fix: Showing `Date.now()` or a client-side timestamp as proof of refresh.
- Required checks: worker API tests where touched, frontend build, refresh status tests where touched
- Usually touched files: `AnalyticsRefreshStatusService`, worker endpoints, `WorkersPanel`, trust header usage

## 7. Missing MV/table

- Symptom: Analytics screen crashes or falls back to zero because a materialized view or table is missing.
- Detection: Backend exception contains missing relation/object details; UI often shows fake zero if mishandled.
- Root cause: Deploy or migration drift left a required object unavailable.
- Correct fix: Return a controlled unavailable/error state and document the migration/runtime dependency.
- Wrong fix: Catching the exception and returning DTOs full of zeros.
- Required checks: backend tests, live smoke or targeted endpoint call if applicable
- Usually touched files: endpoint handlers, DB initializer or migration docs, smoke docs

## 8. Hardcoded theme colors

- Symptom: New UI ignores the existing theme or uses one-off hex colors.
- Detection: Search for raw color values or inline color styles in the changed file.
- Root cause: Quick visual fix skipped theme tokens.
- Correct fix: Use CSS variables and existing semantic surfaces/status tokens.
- Wrong fix: Adding “just one more” inline success/critical color.
- Required checks: `npm run build`
- Usually touched files: page CSS, component CSS, TSX with inline styles

## 9. Recharts chart drift

- Symptom: Legend order changes, unsupported props are added, chart behavior differs from business expectations.
- Detection: Compare with existing working chart patterns and current Recharts version behavior.
- Root cause: Local chart customization diverged from established component usage.
- Correct fix: Keep legend order explicit and use supported props only.
- Wrong fix: Forcing behavior with arbitrary prop combinations or version-guessing.
- Required checks: `npm run build`, affected chart/page tests if present
- Usually touched files: chart pages, shared chart components

## 10. Vitest watch mode trap

- Symptom: Terminal hangs on `Waiting for file changes` and the task stalls.
- Detection: Test command was launched without `--run`.
- Root cause: Watch mode used in an agent workflow that expects one-shot execution.
- Correct fix: Run `npm run test -- --run <path-to-spec>`.
- Wrong fix: Re-running the same hanging watch command repeatedly.
- Required checks: target spec with `--run`
- Usually touched files: test commands, task notes, queue docs

## 11. Report preview expired

- Symptom: Report preview/export looks broken even though the UI route still renders.
- Detection: Inspect preview or durable report response for expiry/error metadata.
- Root cause: Preview state expired, export failed, or report payload no longer matches the durable route.
- Correct fix: Show an explicit expired/unavailable report state and keep methodology/warnings visible.
- Wrong fix: Pretending an expired preview is an empty but valid report.
- Required checks: report page tests, build
- Usually touched files: report pages, report actions, report endpoint docs/tests

## 12. API URL hits SPA instead of backend

- Symptom: Network request or curl call returns the SPA shell or HTML instead of JSON.
- Detection: Response body contains app shell HTML or bundle tags instead of API payload.
- Root cause: Wrong base URL, deploy routing drift, or frontend host was queried instead of backend host.
- Correct fix: Verify backend base URL and explicit API route host; update smoke docs and config only if proven.
- Wrong fix: Calling it a backend success because the route returned `200`.
- Required checks: targeted curl/fetch and live smoke docs when relevant
- Usually touched files: smoke docs, config docs, service base URL logic

## 13. Action notes stored in Description

- Symptom: Status history, outcome notes, or audit context gets packed into generic description text.
- Detection: Update flow mutates `Description` with audit-ish data instead of structured note/outcome fields.
- Root cause: Missing or bypassed structured action metadata handling.
- Correct fix: Keep history/outcome data in dedicated fields or planned ledger structures.
- Wrong fix: Appending more audit text into description blobs.
- Required checks: analytics action tests, build
- Usually touched files: analytics action endpoints, analytics actions UI, future ledger docs

## 14. Oversized commit

- Symptom: One commit mixes docs, backend behavior, frontend UX, tests, and deploy changes.
- Detection: Commit diff spans unrelated modules or three-plus independent concerns.
- Root cause: Scope control was lost during “while I’m here” edits.
- Correct fix: Split into docs-only, backend-only, frontend-only, test-only, or migration-only commits when possible.
- Wrong fix: Shipping a mega-commit because everything “kind of relates to analytics”.
- Required checks: self-review of changed files and commit scope before commit
- Usually touched files: all layers

## 15. Token/agent drift

- Symptom: The agent starts reading too much, patching too broadly, or forgetting the original task goal.
- Detection: Changed-file count grows, repeated re-reading happens, or unrelated fixes accumulate.
- Root cause: Missing scope stop, weak source-of-truth identification, or task creep.
- Correct fix: Stop, summarize confirmed vs unclear, and return to the smallest next step.
- Wrong fix: Continuing blind until the diff becomes unreviewable.
- Required checks: none beyond scope discipline; use targeted reads before code edits
- Usually touched files: task notes, queue docs, any broad diff in progress

## 16. Search/build loop

- Symptom: The same failed build or search is repeated without narrowing the issue.
- Detection: Command history shows the same failing command repeated.
- Root cause: No first-error triage or no stop rule after a repeated failure.
- Correct fix: Read the first meaningful error, narrow the scope, and retry only after a targeted fix.
- Wrong fix: Re-running the whole build/test command over and over.
- Required checks: targeted build/test after the first fix
- Usually touched files: whichever file caused the first real error

## 17. Vercel/GitHub commit email failure

- Symptom: Vercel or GitHub status fails because the commit email settings are not accepted.
- Detection: Deployment/status points to GitHub commit email settings or unverified email guidance.
- Root cause: Local `git user.email` is not verified or uses an incompatible address.
- Correct fix: Configure a verified GitHub email or GitHub no-reply email and trigger a fresh small commit.
- Wrong fix: Assuming the app is deployed because the local build passed.
- Required checks: `git config user.email`, deployment/status evidence
- Usually touched files: deploy proof docs, queue docs, git config guidance

## 18. Stale Vercel bundle / generic SPA shell

- Symptom: Live analytics route renders a generic shell or old bundle instead of the current screen.
- Detection: Compare live route content, bundle hash, and expected route-specific UI.
- Root cause: Vercel deploy drift, stale deployment, wrong branch/root/output config, or a blocked redeploy.
- Correct fix: Prove the live route content after redeploy and document the observed bundle/version.
- Wrong fix: Marking deployment fixed based only on build success or dashboard status.
- Required checks: live route verification, redeploy proof docs
- Usually touched files: Vercel proof docs, queue docs, route smoke docs

## 19. Route lazy import test breakage

- Symptom: Route smoke tests fail and the temptation is to replace lazy imports with direct imports.
- Detection: App route spec fails around Suspense/lazy loading or page mocks.
- Root cause: Test setup or mocks do not match the route-loading model.
- Correct fix: Fix tests and mocks while preserving lazy/Suspense runtime routing.
- Wrong fix: Flattening runtime routing to satisfy tests.
- Required checks: route smoke tests, frontend build
- Usually touched files: `App.tsx`, route tests, page mocks

## 20. Protected action writes 401/403 showing fake success

- Symptom: Create/update/resolve action call returns `401/403` but the UI still shows success or optimistic state.
- Detection: Simulate forbidden write and verify the card/row state after failure.
- Root cause: Forbidden errors are not mapped through shared write-error handling.
- Correct fix: Use `isAnalyticsActionWriteForbidden` and `getAnalyticsActionWriteErrorMessage`; reset busy state and keep read-only recommendations visible.
- Wrong fix: Swallowing the error and leaving the optimistic success state in place.
- Required checks: focused write-failure UI tests, `npm run check:analytics-guardrails`, `npm run build`
- Usually touched files: `AnalyticsActionsPage.tsx`, `ProductDecisionCenterPage.tsx`, `ExecutiveDecisionBoardPage.tsx`, `analyticsActionWriteErrors.ts`

## 21. Frontend computing recommendation/confidence locally

- Symptom: Page code derives recommendation, confidence, reliability, or decision score with local thresholds.
- Detection: Search for local scoring constants, assignments to decision/confidence fields, or weighted formulas in pages.
- Root cause: Backend contract was bypassed for convenience.
- Correct fix: Move or keep business scoring in backend contracts and let frontend display the returned semantics.
- Wrong fix: Adding more local thresholds to make the UI “feel smarter”.
- Required checks: `npm run check:analytics-guardrails`, affected tests, backend tests if contract changes
- Usually touched files: `src/pages/*`, `src/components/*`, analytics endpoints, decision board logic
