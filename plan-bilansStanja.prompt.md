## Plan: Bilans stanja — Stranica u Analytics meniju

TL;DR: Napraviti novu stranicu "Bilans stanja" u analytics meniju koja prikazuje bilans zaliha, detaljan popis robe (filtriran/paginisan), KPI zaliha i mogućnosti za štampu/izvoz (CSV/PDF) u skladu sa postojećim izveštajima po izgledu i funkcionalnostima.

**Steps**
1. Discovery: mapirati postojeće API-je koje možemo ponovo koristiti (npr. `GET /api/analytics/inventory/status`, `GET /api/analytics/cached/filters/stores`) i identifikovati nedostajuće (npr. `GET /api/analytics/inventory/list`). *depends on step 2*
2. Backend: dodati/izložiti potrebne endpoints:
   - `GET /api/analytics/inventory/balance` — sažeti KPI zaliha (total SKU, ukupno na stanju, low stock, out of stock, vrednost zaliha)
   - `GET /api/analytics/inventory/list` — paginisan, filtrabilan popis artikala sa poljima: `sku, artikalId, naziv, kategorija, prodavnica, onHand, reserved, avgSales30d, lastUpdated, supplier` *blocks step 3*
   - `POST /api/documents/generate` reuse — opcija za server-side PDF/XLSX izvoz
3. Frontend: dodati novu stranicu i rutu u analytics meniju
   - Create `Klijent/clientapp/src/pages/InventoryBalancePage.tsx` (ili `BilansStanjaPage.tsx`) koristeći UI pattern iz `AnalyticsDashboard.tsx` (metric cards, filter bar, table with export/print button)
   - Reuse `Klijent/clientapp/src/services/analyticsApi.ts` — add `getInventoryBalance`, `getInventoryList`, `requestInventoryExport`
4. UX & funkcionalnosti
   - Filter bar: period (zadnjih X dana za avgSales), prodavnica, dobavljač, kategorija, search (sku/naziv)
   - KPI cards row: `Ukupno SKU`, `Ukupno na stanju`, `Niska zaliha`, `Bez zaliha`, `Procena vrednosti zaliha`
   - Table: paginacija, sort (onHand, avgSales30d), row actions: `Detalji` (otvara modal ili route), `Rezervisati` (opcionalno)
   - Export/Print: `Export CSV` (client-side if page has loaded subset), `Export PDF` (server-side via `POST /api/documents/generate`) i `Print` (otvara `/api/documents/{id}/print` ili `window.print()` za mala prikazivanja)
5. Item detail view
   - Modal komponenta `Klijent/clientapp/src/components/InventoryItemDetail.tsx` koja pokazuje istoriju promena, lokacije zaliha i povezane dokumente. Dostupna i kao `GET /inventory/:id` ruta za deep-link.
6. Performance & thresholds
   - Za list >2000 redova preusmeriti na server-side izvoz (asinkroni job) umesto client-side CSV.
   - Backend treba podržati pagination params (`page`, `pageSize`) i server-side sort/filter.
7. Tests & Verification
   - Backend unit test za `GET /api/analytics/inventory/list` i `balance` (sample data)
   - Frontend integration: render page, apply filters, export CSV and assert file content shape
   - Manual: load with realistic dataset, export CSV, request PDF and open print preview
8. Accessibility & i18n
   - Reuse existing components for focus/keyboard; text should use existing SR translations

**Relevant files**
- `Klijent/clientapp/src/pages/InventoryBalancePage.tsx` (new)
- `Klijent/clientapp/src/components/InventoryItemDetail.tsx` (new)
- `Klijent/clientapp/src/services/analyticsApi.ts` — add `getInventoryBalance`, `getInventoryList`, `requestInventoryExport`
- `Api/Endpoints/AllEndpoints.cs` — add mappings for new analytics endpoints if needed
- `Application/Analytics/Queries/*` — add `GetInventoryBalanceQuery`, `GetInventoryListQuery` handlers
- `Infrastructure/Services/Documents/*` — reuse for PDF/XLSX generation

**Verification**
1. API: `curl "http://localhost:8080/api/analytics/inventory/balance"` returns KPI JSON; `GET /api/analytics/inventory/list?page=1&pageSize=50` returns paged data.
2. UI: Open Analytics -> Bilans stanja, apply filters, table displays rows, `Export CSV` downloads CSV with correct columns and encoding, `Export PDF` triggers server job and returns downloadable PDF.
3. Print: `Print` opens print preview with proper `@media print` CSS and page breaks.

**Decisions / Assumptions**
- Look & feel: match `AnalyticsDashboard` patterns (metric cards, filter bar, responsive charts/tables) for consistency.
- Data source: read-only analytics DB (`ProductsDim`, `InventoryMovementFacts`) for historical/aggregated fields; join with write DB if latest on-hand needed.
- Export strategy: client CSV for small sets, server PDF/XLSX for large or formatted exports.

**Further Considerations**
1. Columns for CSV and PDF: confirm required export columns (SKU, Naziv, Kategorija, Store, OnHand, Avg30d, Supplier, LastUpdated, Cost, Value)
2. Authorization: restrict export/print to roles with analytics/reporting permissions.
3. Scheduling: allow scheduled exports (email) later as enhancement.

