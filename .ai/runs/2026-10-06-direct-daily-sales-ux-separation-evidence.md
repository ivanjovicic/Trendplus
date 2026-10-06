Task ID: direct-daily-sales-ux-separation
Queue: direct-user-request
Date: 2026-10-06
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / no open PR
Main commit SHA: c0ae4bbfd7345113c80365fb49969c7970daa5dc
Main verification: passed - fresh fetch confirms origin/main contains c0ae4bbfd7345113c80365fb49969c7970daa5dc
Evidence state: synchronized

## What was done

- Razdvojeni su korisnički tokovi `Prodaja po dobavljačima` (canonical Supplier) i `Prodaja po smenama` (Daily Sales), bez nove Supplier implementacije ili backend/DTO/formula izmene.
- Daily Sales je preusmeren na smenski fokus: četiri primarna KPI-ja, sekundarni kontekst, zatvorena tabela po danima i client-side display pagination (14/30/60).
- Sortiranje ostaje nad kompletnim sortiranim redovima pre pagination slice-a; export/print redovi i zbirne projekcije ostaju kompletni.
- Dodati su recipročne Daily ↔ Supplier overview veze sa očuvanjem kompatibilnog period/store/dataScope konteksta.
- Fresh audit je urađen nad `origin/main`; relevantan javni GitHub pregled nije našao otvorene PR-ove.

## Files changed

- `.ai/runs/2026-10-06-direct-daily-sales-ux-separation-evidence.md`
- `Klijent/clientapp/scripts/known-guardrail-baseline.json`
- `Klijent/clientapp/src/layout/__tests__/navConfig.spec.ts`
- `Klijent/clientapp/src/layout/components/__tests__/Sidebar.spec.tsx`
- `Klijent/clientapp/src/layout/navConfig.ts`
- `Klijent/clientapp/src/pages/DailySalesStatsPage.css`
- `Klijent/clientapp/src/pages/DailySalesStatsPage.tsx`
- `Klijent/clientapp/src/pages/SupplierConsolidatedPage.css`
- `Klijent/clientapp/src/pages/SupplierConsolidatedPage.tsx`
- `Klijent/clientapp/src/pages/__tests__/DailySalesStatsPage.premium.spec.tsx`
- `Klijent/clientapp/src/pages/__tests__/DailySalesStatsPage.spec.tsx`
- `Klijent/clientapp/src/pages/__tests__/SupplierConsolidatedPage.spec.tsx`
- `Klijent/clientapp/src/routes/analyticsRouteDefinitions.ts`

## Validation run

- Fresh repository audit: `git fetch --all --prune`, local `main` aligned with `origin/main` before edits -> pass.
- `git diff --check` -> pass.
- `npm run test:run -- src/layout/components/__tests__/Sidebar.spec.tsx src/layout/__tests__/navConfig.spec.ts src/pages/__tests__/DailySalesStatsPage.spec.tsx src/pages/__tests__/DailySalesStatsPage.premium.spec.tsx src/pages/__tests__/SupplierConsolidatedPage.spec.tsx` -> pass (5 files, 60 tests).
- `npm run check:analytics-guardrails` -> pass (encoding, guardrail self-test, baseline-only guardrail scan, typecheck).
- `npm run build` -> pass.
- Existing responsive audit, fixture mode, Daily + Supplier, light theme, widths 320/360/375/390/768/1024/1280/1800/2048/2400, strict -> pass (20 observations, 0 root overflow, 0 page errors; expected aborted in-flight API requests only).
- Visual spot check of generated Daily screenshots at 320px and 1280px -> pass; no root horizontal overflow or clipped primary shell observed.
- GitHub `Analytics Quality Gates` run `37444058297` for the implementation SHA -> red at the pre-existing `npm audit --audit-level=high` steps (`Audit clientapp dependencies` and `Audit POS UI dependencies`); the same two steps were red on prior run `37435564656`, while this change's local guardrails/build/tests pass.

## Validation not run

- Live Vercel production smoke check -> not run - this task requested repository UX correction and no deployment credential/action was supplied.
- Full frontend analytics suite -> not run - focused Daily/Supplier/Sidebar coverage, guardrails and production build provide the narrow proof for this change.
- GitHub failed-job logs -> not available anonymously (GitHub API returned 403); job/step names and conclusions were inspected through the public Actions API.

## Documentation impact

- No product/architecture owner document required an update; the requested UX behavior is protected by focused tests and this evidence log.
- The existing guardrail baseline line was updated only to track the same reviewed `refetch_setData_null` finding after line movement.

## What was missed

- No known in-scope UX requirement missed.
- Live deployed Vercel verification remains outside this run.

## Risks

- The existing Daily/Supplier APIs and live deployment may still expose unrelated backend/data-quality issues; this change preserves their established trust/error semantics and does not mask them.
- Responsive fixture checks use synthetic data; production data density should receive a normal post-deploy smoke check.
- Current-main CI has a pre-existing high-severity dependency-audit failure in both frontend workspaces; it is outside this UX patch and blocks a green quality-gates result until dependency remediation is handled.

## Post-close routing recovery

- not applicable - direct-user-request

## Next

- Live Vercel smoke is the optional operational follow-up; the delivered implementation SHA is already verified on fresh `origin/main`.
