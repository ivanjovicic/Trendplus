# Direct Pre-Nivelacija review evidence

- Date: 2026-09-18
- Queue: direct-user-request
- Interpreted outcome: review recent `main` commits and local code against the latest Pre-Nivelacija prompt requirements, repair confirmed defects, validate locally, and deliver directly on `main`.
- Owner: Analytics Frontend / Pre-Nivelacija
- Working hypothesis: RQ299/RQ300 are delivered, but the page may still have a table/detail/export parity gap around the persisted focus filter.

## Scope and files

- Read: `AGENTS.md`, `docs/ai/AGENT_START_HERE.md`, `docs/ai/PROMPT_QUEUE_PROTOCOL.md`, `docs/ai/VALIDATION_SELECTOR.md`, `docs/ai/AGENT_RUN_EVIDENCE_STANDARD.md`, `.ai/RUN_LOG_TEMPLATE.md`, recent RQ299/RQ300 commits, the RQ300 prompt, `App.tsx`, `PreNivelacijaPriorityPage.tsx`, and its focused spec.
- Changed: `Klijent/clientapp/src/pages/PreNivelacijaPriorityPage.tsx`; `Klijent/clientapp/src/pages/__tests__/PreNivelacijaPriorityPage.spec.tsx`.
- Unchanged and preserved: pre-existing untracked `.codex-remote-attachments/`.

## Finding and repair

RQ300 requires table/detail/export consistency. The table rendered `filteredRows` while `AnalyticsTableToolbar` received `sortedRows`, so a selected focus tab could export rows that were not visible in the table. The toolbar now receives `filteredRows`. A focused regression test verifies the default export set and the `Pregledaj` focus set.

No backend formula, ranking policy, tenant/security contract, or route contract was changed. The route review confirmed `/analitika/pre-nivelacija-prioriteti/:id` is intentionally handled by the existing `/analitika/:table/:id` route.

## Validation

- Passed: focused `PreNivelacijaPriorityPage.spec.tsx` — 31/31 tests.
- Passed: `npm run check:analytics-guardrails` — encoding, analytics guardrails, and typecheck.
- Passed: `npm run build` — production build completed.
- Passed before the final two-file patch: `npm run test:analytics` — 110 files / 733 tests.
- Passed: `git diff --check`.
- Investigated: `npm run lint` reports 75 errors and 208 warnings across the pre-existing repository baseline, including unrelated components, contexts, and utilities; no broad lint cleanup was included in this owner-scoped repair.
- Not run: CI and manual browser session; no named acceptance gate required either for this direct review.

## Delivery

- Delivery mode: direct-main.
- Planned implementation commit: `fix(analytics): export filtered Pre-Nivelacija rows`.
- Main verification to record after push: fresh `origin/main` contains the implementation SHA.

## Residual risk and next step

- Existing Vite chunk-size advisory remains non-blocking.
- Repository-wide lint remains red from unrelated baseline findings and is outside this focused repair.
- Next owner/step: none for this finding; future work may separately address the lint baseline or add manual browser proof.
