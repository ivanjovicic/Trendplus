Task ID: P-UI-52
Queue: docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md
Date: 2026-10-07
Agent/tool: Codex
Delivery target: main
Working branch / PR: `codex/p-ui-52-analytics-navigation-copy` / none
Main commit SHA: `31b0a8d95906019163a135f3db66f89ffac8ea2d`
Main verification: passed - fresh `origin/main` contains implementation SHA `31b0a8d95906019163a135f3db66f89ffac8ea2d`
Evidence state: pending post-close routing synchronization

## What was done
- Reconciled analytics sidebar labels, route-definition labels and page titles; removed status-like analytics group/item badges and retained descriptive `Analiza` / `Izveštaj` badges.
- Replaced the supplier decision redirect alias with canonical `/analytics/supplier?tab=scorecard`; Sidebar active-route matching now honors required query parameters and chooses the most specific matching query route.
- Translated Decision Pulse to the approved glossary name “Puls odluka”; the supplier report page title now matches its canonical nav label.
- Swept the bounded set of disjoint user-facing frontend copy: corrected Serbian diacritics/grammar in change-log, return, inventory and analytics detail surfaces. No API identifiers, enums, route segments, fixture contract values or business logic changed.
- The owned “N/A” fallbacks in Analytics Actions and Supplier Footwear already displayed “Nije dostupno” before this claim; a targeted search found no remaining `N/A` in those production files, so they needed no edit.
- Delivered implementation directly to `main` in `31b0a8d95906019163a135f3db66f89ffac8ea2d`.

### Navigation label reconciliation

| Canonical path | Nav label | Route-definition label | Page title |
|---|---|---|---|
| `/analytics` | Pregled poslovanja | Pregled poslovanja | Pregled poslovanja |
| `/analytics/pilot-readiness` | Pilot spremnost | Pilot spremnost | Pilot spremnost |
| `/analytics/decision-board` | Izvršni board odluka | Izvršni board odluka | Izvršni board odluka |
| `/analytics/products` | Odluke o proizvodima | Odluke o proizvodima | Odluke o proizvodima |
| `/analytics/actions` | Akcije i preporuke | Akcije i preporuke | Akcije i preporuke |
| `/analytics/decision-pulse` | Puls odluka | Puls odluka | Puls odluka |
| `/analytics/supplier?tab=scorecard` | Prodaja po dobavljačima | Prodaja po dobavljačima | Prodaja po dobavljačima |
| `/analytics/supplier` | Prodaja po dobavljačima | Prodaja po dobavljačima | Prodaja po dobavljačima |
| `/analytics/inventory` | Analitika zaliha | Analitika zaliha | Analitika zaliha |
| `/analytics/shoe-type-sales-stats` | Prodaja po tipu obuće | Prodaja po tipu obuće | Prodaja po tipu obuće |
| `/analytics/daily-sales` | Prodaja po smenama | Prodaja po smenama | Prodaja po smenama |
| `/analytics/nivelacije-pre-post` | Pre/Posle nivelacije | Pre/Posle nivelacije | Pre/Posle nivelacije |
| `/analytics/color-sales-stats` | Prodaja po boji artikla | Prodaja po boji artikla | Prodaja po boji artikla |
| `/analytics/pre-nivelacija-prioriteti` | Prioriteti nivelacije | Prioriteti nivelacije | Prioriteti nivelacije |
| `/analytics/data-quality` | Pregled zdravlja podataka | Pregled zdravlja podataka | Pregled zdravlja podataka |
| `/analytics/supplier/report` | Trendplus izveštaj dobavljača | Trendplus izveštaj dobavljača | Trendplus izveštaj dobavljača |
| `/analytics/reports/pilot-intake` | Pilot izveštaj kvaliteta podataka | Pilot izveštaj kvaliteta podataka | Pilot izveštaj kvaliteta podataka |

## Files changed
- `Klijent/clientapp/src/components/DnevnikPromenaDetail.tsx`
- `Klijent/clientapp/src/components/analytics/AnalyticsDetailView.tsx`
- `Klijent/clientapp/src/components/inventory/ExportSchedulerPanel.tsx`
- `Klijent/clientapp/src/components/inventory/InventoryInsightPanels.tsx`
- `Klijent/clientapp/src/components/inventory/InventoryPriorityPanels.tsx`
- `Klijent/clientapp/src/layout/__tests__/navConfig.spec.ts`
- `Klijent/clientapp/src/layout/components/Sidebar.tsx`
- `Klijent/clientapp/src/layout/components/__tests__/Sidebar.spec.tsx`
- `Klijent/clientapp/src/layout/components/__tests__/headerNavigation.spec.ts`
- `Klijent/clientapp/src/layout/navConfig.ts`
- `Klijent/clientapp/src/pages/DecisionPulsePage.tsx`
- `Klijent/clientapp/src/pages/DnevnikPromenaPage.tsx`
- `Klijent/clientapp/src/pages/PovracajPage.tsx`
- `Klijent/clientapp/src/pages/SupplierConsolidatedPage.tsx`
- `Klijent/clientapp/src/pages/SupplierFootwearAnalyticsPage.tsx`
- `Klijent/clientapp/src/pages/__tests__/DecisionPulsePage.spec.tsx`
- `Klijent/clientapp/src/pages/__tests__/SupplierConsolidatedPage.spec.tsx`
- `Klijent/clientapp/src/routes/analyticsRouteDefinitions.ts`
- `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md`
- `docs/roadmaps/ANALYTICS_UI_PREMIUM_ROADMAP.md`
- `MASTER_ROADMAP.md`
- `.ai/runs/2026-10-07-P-UI-52-evidence.md`

## Validation run
- `npm run test:run -- --reporter=dot src/pages/__tests__/DecisionPulsePage.spec.tsx src/layout/components/__tests__/Sidebar.spec.tsx src/layout/__tests__/navConfig.spec.ts src/layout/components/__tests__/headerNavigation.spec.ts` -> pass, 4 files / 21 tests.
- `npm run test:run -- --reporter=dot src/pages/__tests__/SupplierFootwearAnalyticsPage.spec.tsx src/pages/__tests__/DnevnikPromenaPage.spec.tsx src/pages/__tests__/PovracajPage.spec.tsx` -> pass, 3 files / 23 tests.
- Scoped nine-file Vitest run covering nav, sidebar, route definitions, supplier redirects, supplier report and copy pages -> 8 files passed; `SupplierConsolidatedPage.spec.tsx` had one known failure (60/61 tests overall). The title-specific case `renders consolidated trust header` passes 1/1.
- `npm run check:encoding` -> pass; no mojibake detected.
- `npm run check:analytics-guardrails` -> pass; baseline remains at 39 known findings with no new findings; includes encoding and TypeScript build-mode check.
- `npm run typecheck` -> pass.
- `node scripts/check-agent-instructions.mjs --self-test` and `node scripts/check-agent-instructions.mjs` -> pass (14 canonical files).
- `node scripts/check-prompt-queues.mjs --self-test` and `node scripts/check-prompt-queues.mjs` -> pass (709 tasks).
- `node scripts/check-planning-architecture.mjs --self-test` and `node scripts/check-planning-architecture.mjs` -> pass (80 tasks).
- `git diff --check` -> pass before implementation commit.
- Targeted `rg -n 'N/A'` in `AnalyticsActionsPage.tsx` and `SupplierFootwearAnalyticsPage.tsx` returned no matches.
- GitHub Analytics Quality Gates run `37605941949` is queued on exact implementation SHA `31b0a8d95906019163a135f3db66f89ffac8ea2d`.

## Validation not run
- Full frontend suite -> not run; mapped focused specs and guardrails were used.
- Production build -> not run; the prompt's typecheck and analytics guardrail proof passed.
- Browser/device smoke -> not required for this copy/navigation prompt; canonical-link, active-sidebar and legacy redirect behavior have focused route/component tests.

## Documentation impact
- Updated the P-UI queue completion note, `MASTER_ROADMAP.md` routing row and `ANALYTICS_UI_PREMIUM_ROADMAP.md` current direction.
- Added this durable evidence log. The closure commit will synchronize the mandatory post-close recovery section.

## What was missed
- None known in repository-local acceptance. No in-scope production `N/A` fallback remained to change.

## Risks
- Full scoped tests have one known duplicate-text assertion failure at `SupplierConsolidatedPage.spec.tsx:472`: it expects one “Učitavanje pouzdanosti” text node while the existing trust header renders the label in two regions. This same class of trust-header assertion was red on earlier Analytics Quality Gates run `37592911311`, and run `37602941157` was red on unchanged analytics specs; the changed supplier title test passes in isolation. The assertion is outside P-UI-52's page-title behavior.
- Analytics Quality Gates run `37605941949` is queued; it has not proved anything yet.
- A pre-existing untracked responsive artifact directory `Klijent/clientapp/tmp/` remains in this isolated worktree and is excluded from commits; it was not cleaned. The primary checkout and its user changes remain untouched.

## Post-close routing recovery
- Pending the terminal closure commit landing on `main`; recovery must refresh `origin/main`, scan the complete 16-file active owner queue/addendum set and record the candidate/blocker matrix here before final evidence synchronization.

## Next
- Pending the required post-close recovery; do not infer a successor from the pre-close queue pointer.
