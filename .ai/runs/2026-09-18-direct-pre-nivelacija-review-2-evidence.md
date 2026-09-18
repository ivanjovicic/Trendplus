# Direct Pre-Nivelacija review evidence — second pass

- Date: 2026-09-18
- Queue: direct-user-request
- Interpreted outcome: re-review all same-day commits and local Pre-Nivelacija code against RQ297-RQ300 requirements, repair confirmed issues, and deliver directly on `main`.
- Owner: Analytics Frontend / Pre-Nivelacija

## Review scope

- Refreshed `main` from `origin/main`; same-day chain reviewed from `RQ297` through the direct export-parity repair.
- Read the RQ297-RQ300 problem/scope/tests/acceptance sections, recent commit diffs, `PreNivelacijaPriorityPage.tsx`, focused tests, application routes, and queue/governance state.
- Governance checks passed: prompt queue, agent instructions, and planning architecture validators.
- Pre-existing untracked `.codex-remote-attachments/` was preserved and not staged.

## Confirmed finding and repair

The RQ298 export column configuration was exported from the React page module so tests could import it. That created a Fast Refresh lint violation (`react-refresh/only-export-components`), and the page metadata `useMemo` had an implicit `data` dependency warning after numeric normalization.

Repair:

- moved the normalized `DecisionCandidate` contract and `decisionColumns` into `Klijent/clientapp/src/pages/preNivelacijaDecision.ts`;
- imported the contract/configuration into the page and focused spec without changing runtime behavior;
- changed toolbar metadata dependency tracking to `[data]` so the memo reflects its actual source object;
- kept the previous focus-filtered export parity fix intact.

No backend formula, ranking policy, tenant/security contract, URL contract, or recommendation ownership was changed.

## Validation

- Passed targeted ESLint for page, helper module, and focused spec: 0 errors / 0 warnings.
- Passed focused Pre-Nivelacija spec: 31/31.
- Passed `npm run test:analytics`: 110 files / 734 tests.
- Passed `npm run check:analytics-guardrails`: encoding, analytics guardrails, and typecheck.
- Passed `npm run build`; existing non-blocking Vite chunk-size advisory remains.
- Passed governance validators and `git diff --check`.
- Not run: CI and manual browser session.

## Delivery

- Delivery mode: direct-main; current branch was already `main`, so no feature-branch merge was required.
- Implementation files: `Klijent/clientapp/src/pages/PreNivelacijaPriorityPage.tsx`, `Klijent/clientapp/src/pages/preNivelacijaDecision.ts`, `Klijent/clientapp/src/pages/__tests__/PreNivelacijaPriorityPage.spec.tsx`.
- Evidence file: this run log.
- Implementation commit: `4b8644a43db268efcd8313d29419dcb5f96565b1` (`refactor(analytics): isolate pre-nivelacija table contract`).
- Main verification: passed — fresh `origin/main` equals and contains implementation SHA `4b8644a43db268efcd8313d29419dcb5f96565b1`.

## Residual risk

- Existing Vite chunk-size advisory remains non-blocking.
- CI and deployed-browser proof remain uninspected.
