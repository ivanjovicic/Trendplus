# Operacije calculation audit — 2026-09-25

Status: reviewed and routed
Scope: read-only audit of the eight Operacije menu entries and their backend/frontend calculation contracts.

## Executive result

The menu contains eight entries, including two compatibility aliases to the canonical Supplier page. The audit confirmed one existing frontend regression, one cross-screen attribution defect, one date-boundary defect condition and one scope-metadata issue already owned by an existing queue prompt. A bounded fail-closed repair was applied to the concentration panel; the broader denominator contract remains with `RQ431`.

## Findings and routing

| Finding | Classification | Action |
|---|---|---|
| Daily Sales groups supplier totals through current `Artikli.IDDobavljac`, while canonical Supplier Sales uses `ProdajaStavke.SupplierIdAtSale` | New cross-screen correctness defect | `RQ441`, WAITING behind Daily Sales receipt/scope owners |
| Supplier/Shoe Type/Color use inclusive `23:59:59Z` end boundaries; Daily uses half-open next-day semantics | Confirmed code-level boundary defect; previously only a potential in second-pass notes | `RQ442`, READY |
| Daily concentration accepts supplier totals greater than period totals and can produce >100% shares/negative `Ostali` | Existing failing regression | Bounded fail-closed guard applied; complete contract remains `RQ431` |
| Daily receipt/quality/availability diagnostics do not share the selected data scope | Existing routed finding | `RQ382`, no duplicate prompt |
| Color does not use the Supplier/Shoe Type snapshot-cost path when snapshot mode is enabled | Latent conditional inconsistency; inactive under current `UseSnapshotCost=false` defaults | Covered by existing Color provenance contract; no duplicate prompt |

## Direct repair

`Klijent/clientapp/src/pages/DailySalesStatsPage.tsx` now detects when positive authoritative quantity or revenue totals are exceeded by supplier totals. In that case it:

- emits an explicit warning;
- suppresses concentration percentages and Top 3/Top 5 shares;
- does not synthesize the `Ostali` bucket;
- leaves the state unavailable instead of displaying trusted-looking impossible values.

This is intentionally a bounded repair. It does not decide the wider signed-return and denominator contract owned by `RQ431`.

## Verified contracts

- Inventory list calculations use half-open signal windows and expose the documented distinction between period-filtered lists and current snapshots.
- Margin contribution is centralized in `AnalyticsMarginPolicy`; coverage is measured against total revenue and margin percentage against revenue with reliable cost.
- Pre/Post uses calendar-date event semantics and was not changed by this audit.
- Supplier Sales and Supplier Footwear aliases resolve to the canonical Supplier tabs; they do not create independent calculation owners.

## Validation

- `npm run test:run -- src/pages/__tests__/DailySalesStatsPage.premium.spec.tsx` — 21/21 passed after the repair.
- `npm run check:analytics-guardrails` — passed, including encoding, guardrail self-test and TypeScript.
- Filtered backend analytics tests — 69/69 passed.
- Broader focused Operations run before the repair was 195/196; the single failure was the concentration regression described above.
- Live browser/API proof was not available because `http://127.0.0.1:8080` was unavailable. Production/STAB16 proof was not inferred.

## Delivery and follow-up

- Direct repair: delivered on `main` with the accompanying audit/evidence commit.
- `RQ441`: implement frozen supplier attribution parity after `RQ438`/`RQ382` and test ownership are clear.
- `RQ442`: normalize all whole-day Operations ranges to `[from, nextDay)` and prove fractional-second and adjacent-day behavior.
- Do not reopen or duplicate `RQ382`, `RQ431`, `RQ397` or `RQ438`; they remain the owners of their existing contracts.
