# Supplier Sales overview audit — „Prodaja po dobavljačima“ (2026-09-25)

Agent/tool: grok
Date: 2026-09-25
Surface: `/analytics/supplier?tab=overview&legacySource=operations-supplier-sales` (Operacije → „Prodaja po dobavljačima“)
Code base audited: `main` @ `30aa90cd` (runtime files identical to `b6c44fe4`/`origin/main` at audit time)
Delivery: local commits on `main` only (no push, per owner instruction)
Evidence state: pending (local-only; `origin/main` does not contain the fixes yet)

## Scope and data path

- Route and shell: `Klijent/clientapp/src/pages/SupplierConsolidatedPage.tsx` (trust header, context cards, canonical filters) embeds `SupplierSalesStatsPage` (`embedded`, `sharedFilters`, `onTrustMetadataChange`) for `tab=overview`.
- Legacy entry: `SupplierRedirects.tsx` maps `/analytics/supplier-sales-stats?...` to `/analytics/supplier?<all original params>&tab=overview&legacySource=operations-supplier-sales`; the URL state owner is `useSupplierCanonicalState.ts`.
- API client: `src/services/supplierSalesStatsApi.ts` → `GET /api/analytics/supplier-sales-stats` (`Api/Endpoints/AllEndpoints.cs:1095-2089`).
- Backend sources: `ProdajaStavke` ⋈ `ProdajaZaglavlja` ⋈ `Artikli`; revenue = `Kolicina * Cena` (net of return lines), supplier = sale-time `ps.SupplierIdAtSale`, data scope on `Artikli.DataOrigin`, store on `pz.IDObjekat`; cost/margin via `Application/Analytics/AnalyticsMarginPolicy.cs` (`MarginAccumulator`: sale-line cost → snapshot → `NabavnaCenaDin` → `NabavnaCena`, cost must be > 0; margin contribution = covered revenue − covered cost; margin % = contribution / covered revenue).

## Live verification

Not possible read-only: the public Vercel app serves the SPA shell for `/api/*`; `https://trendplus-api.onrender.com` and `https://trendplus.fly.dev` returned HTTP 503 (matches `PS06`/L5 in `docs/ai/PRODUCTS_SUPPLIER_AUDIT_PROMPTS_2026-09-25.md`); no local API on `127.0.0.1:8080`; local PostgreSQL login fails (28P01). Every verdict below is **verified by code and tests only**; no numbers were invented.

## Indicator verdicts

| # | Indicator (UI) | Formula / source | Verdict | Evidence |
|---|---|---|---|---|
| 1 | KPI „Ukupan promet“ | Σ visible rows `ukupanPromet`; backend Σ `Kolicina*Cena` per supplier | Correct (label nit) | `SupplierSalesStatsPage.tsx:684`, `:1776`; `AllEndpoints.cs:1333-1375`. InfoTip says „svih dobavljača“ although a focused/known-only view shows the visible population (RQ373 contract). Same revenue definition as Daily Sales (`DailySalesStatsService.cs:116/191/238`); bucket attribution differs → `RQ441`. VAT/discount semantics of `Cena` unverifiable without the Access price contract. |
| 2 | KPI „Ukupno prodato“ | Σ visible rows `ukupnaKolicina` (net of return lines) | Correct | `:685`, `:1781` |
| 3 | KPI „Ukupna nabavna vrednost“ | Σ visible rows `totalCost` = Σ qty × reliable unit cost for covered lines only | Correct, labeled | `:686`, `:1786`; `AnalyticsMarginPolicy.cs:46-117` |
| 4 | KPI „Maržni doprinos“ + quality badge | Σ visible rows `marginContribution` (covered revenue − covered cost) | Correct value; badge describes the whole response (`data.totals.marginQuality*`) even in focus mode (low) | `:687`, `:1791-1806`; `AnalyticsMarginPolicy.cs:107-117` |
| 5 | KPI „Ponderisana marža“ | backend `totals.prosecnaMarza` = Σ known MC / Σ known covered revenue | Correct, labeled „(ceo odgovor)“ when filtered | `:1810`; `AllEndpoints.cs:1749-1757`, `AnalyticsMarginPolicy.cs:165-176` |
| 6 | KPI „Udeo top 5 dobavljača“ | top-5 revenue / visible **known** suppliers revenue | Correct per its tooltip; inconsistent with table/comparison denominators (#9, #10); focus mode → trivial 100% (`PS11`/C15); negative-revenue suppliers can push it above 100% | `:197-225`, `:1053-1056`, `:1815`; fail-closed guard `8f5097be` covers Daily only |
| 7 | KPI/toolbar „Ukupan PoP trend“ | was (Σ visible current − Σ visible previous) / Σ visible previous | **Bug (medium)** — suppliers with previous-period sales but no current sales have no row, so the previous base is too small and growth is overstated. Fixed by `RQ443` | `:688`, `:715`, `:1272`, `:1821`; `AllEndpoints.cs:1281-1317` vs rows from current lines `:1430+`; backend `totals.previousPeriodRevenue` includes them |
| 8 | Chart „Koncentracija prometa“ | known visible rows, share of known total, top N | Correct math; InfoTip says „ukupnog prometa“ but the denominator excludes unknown (low, `PS11` addendum) | `:1077-1080`, `:1845-1867` |
| 9 | Chart „Udeo prometa vs maržnog doprinosa“ | table `sharePct` (denominator: all visible incl. unknown), top 8 known | Correct math; same legend „Udeo u prometu %“ as #8 with a different denominator (low, `PS11` addendum) | `:1082-1098`, `:1898` |
| 10 | Table „Udeo u prometu“ / „Udeo u MD“ | row / Σ visible (incl. unknown) ×100; MD share null when Σ MD ≤ 0 | Correct; sums to 100% when no row is negative; focus mode → 100% (`PS11`/C15) | `:695-705` |
| 11 | Table „Promet“, „Količina“, „Nabavna vrednost“, „Maržni doprinos“, „Marža %“ | backend per-supplier values | Correct | `AllEndpoints.cs:1430-1639`; `AnalyticsMarginPolicy.cs` |
| 12 | Table „PoP trend“ (row) | backend per supplier; null when previous ≤ 0 → „Novo“/N/A | Correct (zero/negative base fails closed) | `AllEndpoints.cs` per-supplier PoP; `describePopMetric` tests |
| 13 | Table „Preporuka“, priority chips, toolbar counts | backend recommendation; chip/toolbar counts known suppliers only | Correct; toolbar says „U pregledu“ while the chip says „Oprez“ (copy, `RQ325` addendum) | `:1100-1112`, `:1283`, `:1914` |
| 14 | Trust header „Period“ (embedded) | `data.fromDate`/`data.toDate` through local `formatDate` | **Bug (medium)** — `toDate` `…T23:59:59Z` renders as the next day in Europe/Belgrade. Fixed by `RQ444` | `:1219-1221`; `utils/analyticsFormatters.ts:61-70` |
| 15 | Context card „Period i filteri“ | `trustPayload.effectivePeriodLabel` = whole-history sales data window | **Bug (medium)** — shows `dataWindowFrom–dataWindowTo` (`GetSalesDataWindowAsync`, `AllEndpoints.cs:7911`) instead of the analyzed period. Fixed by `RQ444` | `SupplierConsolidatedPage.tsx:119-130`, `:485`; `SupplierSalesStatsPage.tsx:1229` |
| 16 | Period filters with a legacy `sezonaId` | backend replaces from/to with the season range when `sezonaId` is sent | **Bug (medium)** — legacy links/Data Quality return links keep `sezonaId`; consolidated Period/Od/Do never clear it, so KPIs stay on the season while the filters show new dates. Fixed by `RQ444` | `useSupplierCanonicalState.ts:128-144`; `SupplierSalesStatsPage.tsx:783`, `:856`, `:1325-1328`; `AllEndpoints.cs:1161-1175` |
| 17 | Whole-day boundary | `T23:59:59Z` + `<=` | Known bug, owned by `RQ442` (not duplicated) | `:182-187`; `AllEndpoints.cs:1338` |
| 18 | Legacy redirect `operations-supplier-sales` | all params copied, `tab=overview` and `legacySource` set | Correct; only the legacy `sezonaId` side effect (#16) | `SupplierRedirects.tsx`; `SupplierRedirects.spec.tsx` |
| 19 | Loading / error / empty / retry | spinner, safe error with retry, empty reasons (`no_data`/`filtered_out`/`insufficient_data`) | Present; failure vs empty on a 503 is owned by `PS06` | `:1700-1740`, `:1205-1210` |
| 20 | Sorting | unknown suppliers pinned last; null-safe compare | Correct; StrictMode sort toggle and rank badges → `PS18` | `:940-985` |
| 21 | Copy | Serbian UI | Residual ASCII/English: `:597` „izvrsenje“, `:1163` „tumaciti … delimican“, `:1283` „U pregledu“, `:1659` „Canonical pregled“, `:1224`/`:1664` „Supplier sales stats (scope: …)“, `:410`/`:484` „Low signal“, `:1926` „decision preporuke“ → `RQ325` addendum (and `PS17`) | — |

## Bugs found (severity)

1. Medium — total PoP trend overstated (#7) → fixed, `RQ443`.
2. Medium — sticky legacy `sezonaId` pins the analyzed period (#16) → fixed, `RQ444`.
3. Medium — context card shows the data window as the period; header end date +1 day in UTC+ zones (#14, #15) → fixed, `RQ444`.
4. Low — concentration/Top-5 use a known-only denominator while the table/comparison chart use all visible rows, under the same legend; negative-revenue rows can push Top-5 above 100% (#6, #8, #9) → routed to `PS11` (addendum; owner decision on the denominator).
5. Low — known-only and focused PoP need a backend-scoped previous total that includes previous-only suppliers → `PS11` step 3 (`RQ443` fails closed to N/A for known-only meanwhile).
6. Low — margin quality badge and „Ukupan promet“ tooltip describe the whole response in focus mode (#1, #4) → `PS11`/`PS17` addendum.
7. Low — copy residuals (#21) → `RQ325` addendum.

## Existing owners referenced (not duplicated)

`RQ373` (display population, DONE), `RQ233` (top-5, DONE), `RQ441` (Daily attribution parity), `RQ442` (half-open ranges), `RQ382`, `RQ431`, `RQ325` (copy), `RQ439` (PR #63 triage), and the unregistered `PS06`, `PS11`, `PS12`, `PS16`, `PS17`, `PS18` in `docs/ai/PRODUCTS_SUPPLIER_AUDIT_PROMPTS_2026-09-25.md` (committed `30aa90cd` while this audit ran).

## Fixes

- `RQ443` — see `.ai/runs/2026-09-25-RQ443-evidence.md`.
- `RQ444` — see `.ai/runs/2026-09-25-RQ444-evidence.md`.

## Validation baseline

- Full frontend suite before the fixes (`883b10e7`, clean `git archive` export): 10 failed / 1314 passed (1324). Failures: the nine `RQ440` cases plus `DailySalesStatsPage.numericState.spec.ts` „preserves a signed negative remainder …“ (not in this scope).
- Focused Supplier specs before: 45/46 (only the pre-existing `RQ440` case in `SupplierConsolidatedPage.spec.tsx`).
