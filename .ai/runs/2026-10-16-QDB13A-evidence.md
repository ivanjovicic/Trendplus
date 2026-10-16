Task ID: QDB13A
Queue: `docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md`
Date: 2026-10-16
Agent/tool: Codex
Delivery target: main
Working branch / PR: `agent/qdb13a-sef-xml-draft-isolated` / no QDB13A PR
Main commit SHA: `b0687a3e3e99027b4ffb4c40b71bd630a377fc41` (implementation)
Main verification: passed - refreshed `origin/main` is `c93c39776317e767a7042f1fb6e277c854684ca0` and contains implementation `b0687a3e3e99027b4ffb4c40b71bd630a377fc41` plus closeout `c93c39776317e767a7042f1fb6e277c854684ca0`
Evidence state: synchronized
Ownership transfer: none
Claim reservation: inspected `origin/main` `6b65c392f8473de57cd4813bd2da936c69319ee4`; pushed claim `bd4a95d2da67dca1e32a658b70eb24009b7bc03a`; path correction `dc49fe4ffc10f743b08e4eab83546a4d610e8455`; owner Codex; worktree `F:\Trendplus-dev\Trendplus2-qdb13a-live`; branch `agent/qdb13a-sef-xml-draft-isolated`; no remote QDB13A branch/PR; PR #184 remains OPEN/DRAFT at `879ccc22a02c3722fb172a9dffd6885f5f80693b` and owns only excluded shared UI paths.

## What was done
- Added an isolated, intentionally unmounted SEF XML receipt-draft wizard, request-scoped read-only preview/reconcile client, presentation helpers and deterministic CSV export.
- Exact code matches are labelled for operator verification; code equality does not assert a shared ID namespace, item identity, model/size identity or received stock.
- Physical quantity stays unknown until operator entry; source invoice quantities/prices are not silently transformed. CSV is formula-safe and every row is `NEKNJIŽENO — NACRT`.
- No `/unos-robe` route, router, shared API/auth, backend, Access import, inventory posting, journal, price, migration or PR #184 files were changed.
- Development-storage guidance now prefers disposable worktrees/cache/temp under `F:\Trendplus-dev` when available, while keeping the canonical checkout in place and excluding production data/secrets.

## Files changed
- `Klijent/clientapp/src/components/sef-xml-pilot/SefXmlReceiptDraftWizard.tsx`
- `Klijent/clientapp/src/components/sef-xml-pilot/SefXmlReceiptDraftWizard.css`
- `Klijent/clientapp/src/components/sef-xml-pilot/sefXmlDraftCsv.ts`
- `Klijent/clientapp/src/components/sef-xml-pilot/sefXmlDraftPresentation.ts`
- `Klijent/clientapp/src/components/sef-xml-pilot/SefXmlReceiptDraftWizard.spec.tsx`
- `Klijent/clientapp/src/services/sefXmlPilotApi.ts`
- `Klijent/clientapp/src/services/__tests__/sefXmlPilotApi.spec.ts`
- `AGENTS.md`
- `.github/copilot-instructions.md`
- `docs/ai/AGENT_START_HERE.md`
- `docs/ai/WINDOWS_DEVELOPMENT_STORAGE.md`
- `docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md`
- `MASTER_ROADMAP.md`
- `.ai/runs/2026-10-16-QDB13A-evidence.md`

## Validation run
- Initial red: the new focused Vitest files failed import resolution before implementation because their component/client modules did not yet exist (expected test-first failure).
- `npm ci --cache F:\Trendplus-dev\npm-cache --prefer-offline` -> pass; 464 packages installed, 0 audit vulnerabilities.
- `npm run test -- --run src/services/__tests__/sefXmlPilotApi.spec.ts src/components/sef-xml-pilot/SefXmlReceiptDraftWizard.spec.tsx` -> pass, 27/27.
- `npm run typecheck` -> pass.
- `npm run check:analytics-guardrails` -> pass; encoding, guardrail self-test/baseline (36 known, 0 removed) and typecheck passed.
- `npm run build` -> pass; Vite production build, 2,734 modules.
- `node scripts/check-agent-instructions.mjs --self-test` and `node scripts/check-agent-instructions.mjs` -> pass; 18 canonical files.
- `node scripts/check-prompt-queues.mjs --self-test` and `node scripts/check-prompt-queues.mjs` -> pass; 811 tasks.
- `node scripts/check-planning-architecture.mjs --self-test` and `node scripts/check-planning-architecture.mjs` -> pass; 80 new planning tasks.
- `git diff --check` and `git diff --cached --check` -> pass for implementation and closeout docs.
- GitHub Actions Analytics Quality Gates run `38879677397` -> SUCCESS on exact implementation SHA `b0687a3e3e99027b4ffb4c40b71bd630a377fc41`.
- GitHub Actions Planning Governance run `38880614047` -> SUCCESS on exact closeout SHA `c93c39776317e767a7042f1fb6e277c854684ca0`.

## Validation not run
- Browser or deployed demo: not run; component is deliberately unmounted and no demo URL exists.
- Real supplier XML and owner/operator session: not run; no real invoice or consent was provided.
- Production Access, stock or receipt writes: not run and not authorized.

## Documentation impact
- Added `docs/ai/WINDOWS_DEVELOPMENT_STORAGE.md` and linked it from the repository/agent entry instructions to make F: development storage discoverable and safe.
- Updated the QDB owner queue and `MASTER_ROADMAP.md` to distinguish the isolated QDB13A deliverable from the still-gated `/unos-robe` integration.

## What was missed
- Integration into `/unos-robe`, browser proof, deployed demonstration, real XML compatibility and operator value validation remain uncompleted by design.
- Two disposable development remnants remain on F: because recursive deletion was rejected by the safety layer: `F:\Trendplus-dev\qdb13a-incomplete-node_modules` (106,164,168 bytes) and `F:\Trendplus-dev\Trendplus2-qdb13a` (failed unregistered partial checkout containing only a QDB13A lock). They are not required by the active F: worktree; no alternate deletion method was attempted.

## Risks
- PR #184 remains OPEN/DRAFT and owns the shared `/unos-robe` UI paths; no formal release/handoff was recorded, so QDB13 remains WAITING.
- The UI component has no mounted route or authenticated browser/deploy proof. QDB13A is not a user-facing demo and must not be presented as one.
- The matching-code warning is a presentation safeguard; no supplier/Trendplus namespace or shoe size/model identity was proven against a real operator source.

## Post-close routing recovery
- Recovery base `origin/main` SHA: `c93c39776317e767a7042f1fb6e277c854684ca0`; verified both implementation `b0687a3e3e99027b4ffb4c40b71bd630a377fc41` and closeout SHA are contained in current `main`.
- Active owner queue/addendum files scanned: `docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md` (QDB07/QDB08/QDB10 and QDB13/QDB14 candidates), `MASTER_ROADMAP.md`, `docs/ai/PROMPT_QUEUE_PROTOCOL.md`, the analytics reliability main queue and `ANALYTICS_RELIABILITY_PROMPT_QUEUE_POST_IMPORT_VALUE_ADDENDUM.md`, plus the current backend-CI and stabilization queue headers.
- Changed/completed task IDs searched: QDB13A. Newly satisfied dependency: QDB13A prerequisite for QDB13. QDB11 and QDB12 remain DONE.
- Post-close checks: PR #184 remains OPEN/DRAFT, head `879ccc22a02c3722fb172a9dffd6885f5f80693b`; fresh remote lookup found no QDB13A branch or PR. No owner handoff/release was recorded.
- QDB owner-queue Zero-READY action ladder: repaired the stale QDB13A pointer; reverified QDB07 waits on release/authorization gates despite QDB09 DONE; QDB08 waits on QDB07 and an end-to-end flow; QDB10 waits on QDB07/QDB08 plus an owner-selected external source/mapping. Repository-local QDB13A proof and delivery are complete. Rechecked the exact PR #184 collision; QDB13 is already the bounded integration prompt, with no route-independent slice remaining after QDB13A. The prompts already exist, so no new queue item was created. Next-program scan found analytics RQ primary READY `RQ682` and independent READY `RQ683`/`RQ685`. No global Zero-READY claim is made; no unrelated RQ task was claimed because changing from the specifically assigned SEF pilot would change the requested outcome.
- Candidate blocker matrix: `QDB07 | WAITING | authorization/release gate | QDB09 is DONE; QDB07 still explicitly requires authorization/release permitting connector admin UI work, and the master router keeps it WAITING | checked current queue/master; no owner authorization was supplied | no meaningful same-scope slice: this is the separate SQL Server connector admin experience, not the assigned SEF receipt pilot | owner explicitly permits the connector admin UI pilot scope and a fresh path/owner scan is clear.` `QDB08 | WAITING | dependency on QDB07 and end-to-end sync truth | queue requires QDB07 DONE and proven discovery/mapping/preview/sync | checked queue/current connector evidence | no safe standalone diagnostics UI slice before its backend flow | QDB07 DONE plus end-to-end connector evidence.` `QDB10 | WAITING | QDB07/QDB08 and source/mapping authority | queue dependencies remain incomplete; no external source mapping contract was approved for canonical application | checked owner queue and roadmap | no safe write/apply slice without canonical mapping approval and lineage/idempotency proof | QDB07/QDB08 DONE and owner selects source and approves mapping contract.` `QDB13 | WAITING | external owner/path handoff | PR #184 OPEN/DRAFT at 879ccc22... owns /unos-robe paths; gh pr view and remote branch scan repeated | no owner contact sent because the user did not authorize messaging; local read-only checks done | no safe same-scope split remains: route integration edits the owned path and the disjoint QDB13A slice is DONE | PR #184 owner records a dated formal release/handoff naming prior owner, new owner/branch and whether PR #184 remains untouched, then a fresh PR/branch/lock scan is clear.` `QDB14 | WAITING | business/security/test-environment approval and QDB13 dependency | queue dependency and owner-gated write scope | no production or test DB write attempted | no safe implementation slice without receipt semantics, authorization and isolated DB | owner/operator explicitly approves Trendplus test posting semantics, roles/idempotency and isolated test database after QDB13.`
- Promoted successor: none in the QDB owner queue. The separate analytics RQ pointer was inspected but not claimed; no global no-work result is asserted.

## Next
- QDB13 remains WAITING for the PR #184 owner to formally release/handoff the `/unos-robe` paths; QDB14 remains WAITING for QDB13 plus separate operator/owner authorization and isolated test-database approval. A cross-program RQ task was not started because it would switch away from the requested SEF pilot.
