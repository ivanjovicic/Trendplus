Task ID: P-UI-37
Queue: docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md
Date: 2026-10-02
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / direct delivery
Main commit SHA: 46f9587f6b2424e1238bac7b76be7a87c2ea5c6b
Main verification: passed - fresh origin/main equals implementation SHA 46f9587f6b2424e1238bac7b76be7a87c2ea5c6b
Evidence state: synchronized

## What was done
- Migrated Article List to the shared keyboard-focusable horizontal-scroll table, retaining all columns and pinning the article ID while scrolling.
- Raised phone pagination, page-size, filter and chip controls to 44px targets; phone inputs use 16px text. Added explicit labels/ARIA relationships and converted clickable sort headers into keyboard-accessible buttons without changing sort requests.
- Preserved server paging/filter/sort behavior and removed nullable-to-zero fallbacks for required API response fields.
- Added an Article List fixture route that exercises name filtering, page-size change and next-page behavior without changing the real API contract.
- Executed only the required Article List slice; no optional long-tail/admin/observability page was included.

## Files changed
- `Klijent/clientapp/scripts/responsive_baseline.mjs`
- `Klijent/clientapp/src/pages/ArtikliListPage.tsx`
- `Klijent/clientapp/src/pages/ArtikliListPage.spec.tsx`
- `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md`
- `docs/roadmaps/ANALYTICS_UI_PREMIUM_ROADMAP.md`
- `MASTER_ROADMAP.md`
- `.ai/runs/2026-10-02-P-UI-37-evidence.md`

## Validation run
- `npm run test:run -- src/pages/ArtikliListPage.spec.tsx` -> pass, 1 file / 2 tests.
- `npm run check:analytics-guardrails` -> pass; encoding, guardrail self-test, scanner and typecheck passed. The scanner baseline debt decreased by two nullable-zero findings across the two completed UI migrations.
- `npm run build` -> pass; existing Recharts chunk >500 kB warning remains.
- `npm run lint` -> fail on the pre-existing full-repository backlog (108 errors / 226 warnings); this exact current report is the baseline for the next bounded P-UI-23 cleanup slice. A structured ESLint report isolated the chosen shared-component family; Article List is not part of that slice.
- `npm run responsive:baseline -- --route-id articles --viewport-width <320|375|768> --theme <light|dark> --output-dir tmp/ui-visual/pui37-articles-<width>-<theme> --strict` -> pass for all six width/theme combinations; zero root-overflow observations and zero page errors. Fixture interaction opened filters, searched, changed page size, advanced to page 2 and confirmed all seven table columns plus keyboard-focusable scroll region. Measured phone controls were 44px high and inputs 16px.
- `git diff --check` -> pass.
- `git fetch origin`, exact `HEAD`/`origin/main` revision checks and `git merge-base --is-ancestor 46f9587f origin/main` -> pass; exact refs are `46f9587f6b2424e1238bac7b76be7a87c2ea5c6b`.
- `gh run list --commit 46f9587f --limit 20 --json databaseId,name,status,conclusion,headSha,url` -> GitHub API returned HTTP 503; no Actions status could be discovered at inspection.

## Validation not run
- Full frontend suite -> not run; the scope is one page and its focused test, guardrails, typecheck, build and responsive browser matrix passed.
- Physical mobile browser verification -> not run; no physical-device session was available.
- Queue/instruction/planning validators -> run with the documentation closure update in this delivery.

## Documentation impact
- Synchronized the P-UI queue, analytics UI roadmap and master routing summary with P-UI-37 completion and delivery evidence.

## What was missed
- No known acceptance item remains. Optional long-tail pages are intentionally unclaimed and remain outside this execution slice.

## Risks
- Browser console still reports unrelated seasonal-image fixture HTTP 503 responses; there were no page errors or strict-mode API request failures, and Article List workflow calls used deterministic local fixtures.
- Physical-device keyboard/zoom behavior remains unverified.
- GitHub Actions discovery returned HTTP 503, so no remote workflow state was available for the implementation SHA.
- Existing production-build warning for the Recharts chunk over 500 kB is outside this prompt's ownership.

## Next
- P-UI-23 is promoted and claimed for the bounded Pilot Data Quality Intake component lint slice; see `.ai/task-locks/P-UI-23-codex.lock.md`.
