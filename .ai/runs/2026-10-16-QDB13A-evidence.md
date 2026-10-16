Task ID: QDB13A
Queue: `docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md`
Date: 2026-10-16
Agent/tool: Codex
Delivery target: main
Working branch / PR: `agent/qdb13a-sef-xml-draft-isolated` / no QDB13A PR
Main commit SHA: `b0687a3e3e99027b4ffb4c40b71bd630a377fc41` (implementation)
Main verification: passed - refreshed `origin/main` contains implementation SHA `b0687a3e3e99027b4ffb4c40b71bd630a377fc41`; closeout synchronization pending
Evidence state: pending post-close routing synchronization
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
- `git diff --cached --check` -> pass before implementation commit; closeout docs diff check will be rerun before delivery.
- GitHub Actions Analytics Quality Gates run `38879677397` -> SUCCESS on exact implementation SHA `b0687a3e3e99027b4ffb4c40b71bd630a377fc41`.

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
- Recovery base `origin/main` SHA and dependency scan: pending final refresh after closeout commit.
- Active owner queue/addendum files scanned: pending post-close verification; QDB owner queue is `docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md`.
- Changed/completed task IDs searched: QDB13A; newly satisfied dependency: the QDB13A prerequisite in QDB13.
- QDB13 promotion: not eligible without a formal PR #184 owner release/handoff and fresh collision check. No safe split remains inside the requested QDB13 integration scope because `/unos-robe` is the conflicted owned path; the disjoint unmounted component slice was completed as QDB13A.
- Global next-program routing: pending post-close check. Do not claim a separate program task without confirming the selector and user scope.

## Next
- QDB13 remains WAITING for the PR #184 owner to formally release/handoff the `/unos-robe` paths; QDB14 remains WAITING for QDB13 plus separate operator/owner authorization and isolated test-database approval.
