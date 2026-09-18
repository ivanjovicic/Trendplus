# Remaining analytics commits review evidence

- Date: 2026-09-18
- Queue: direct-user-request
- Interpreted outcome: review the same-day/adjacent commits not covered by the previous RQ297-RQ300 passes, compare them with their prompt acceptance, repair confirmed defects, and deliver on `main`.
- Owner: Analytics Frontend / Pre-Nivelacija and adjacent Pre/Post surfaces

## Review scope

- Refreshed `main` from `origin/main`.
- Reviewed the RQ291-RQ296 requirement/acceptance sections, completion evidence, recent implementation diffs, and the current Pre-Nivelacija scope/error/empty-state paths.
- RQ294 scope-event and RQ296 filtered-empty behavior were consistent with their acceptance and focused coverage. RQ291-RQ293 completion contracts were checked for the adjacent Pre/Post surfaces; no additional confirmed defect was widened into this owner-scoped pass.
- Pre-existing untracked `.codex-remote-attachments/` was preserved and not staged.

## Confirmed finding and repair

RQ295 required suppressing raw technical/non-JSON response bodies. `preNivelacijaApi.ts` still fell back to the raw body when the response was not JSON; a technical `text/plain` body without a recognized error pattern could therefore be shown to the operator.

Repair:

- non-JSON/empty response bodies now fail closed to the established non-empty Serbian fallback;
- structured JSON guidance continues through the shared safe-message mapper;
- correlation ID and error code metadata remain available without exposing raw backend text;
- a regression test covers a non-JSON technical response body.

No backend contract, recommendation formula, ranking policy, tenant/security behavior, or route contract was changed.

## Validation

- Passed targeted ESLint for `preNivelacijaApi.ts` and its focused spec.
- Passed focused API + Pre-Nivelacija specs: 37/37.
- Passed `npm run test:analytics`: 110 files / 735 tests.
- Passed `npm run check:analytics-guardrails`: encoding, guardrails, and typecheck.
- Passed `npm run build`; existing non-blocking Vite chunk-size advisory remains.
- Passed `git diff --check`.
- Not run: CI and manual browser session.

## Delivery

- Delivery mode: direct-main; branch was already `main`, so no feature-branch merge was required.
- Changed implementation files: `Klijent/clientapp/src/services/preNivelacijaApi.ts`, `Klijent/clientapp/src/services/__tests__/preNivelacijaApi.scope.spec.ts`.
- Final target-branch SHA and fresh `origin/main` verification to be recorded after push.

## Residual risk

- CI and deployed-browser proof remain uninspected.
- Existing Vite chunk-size advisory remains non-blocking.
