**Plan: Daily Sales by Shift & Top-Suppliers Table (Advanced Analysis)**

**Purpose:**
- **Goal:** Provide per-day rows showing shift totals, revenue, top 15 suppliers (items sold), "Others", and final total items sold; update UI/DTO/frontend contract and implementation steps accordingly.
- **Scope:** New Analytics menu item (position 3, after Prodaja po tipu obuće); fully-featured table with export (PDF/Excel/CSV), print, filters, sorts; reuses existing export infrastructure.

**Data Model & SQL Analysis**

**Source Tables & Joins:**
- `prodaja_zaglavlje` (ProdajaZaglavlja): `Id`, `DatumProdaje` (datetime), `IDObjekat` (store), `KorisnikIme` (cashier user name).
- `prodaja_stavke` (ProdajaStavka): `IdProdaja` → zaglavlje, `IdArtikal`, `Kolicina` (qty), `Cena` (unit price).
- `Artikli` (Artikli): `Id`, `IDDobavljac` (supplier FK).
- `Dobavljaci` (Dobavljac): `Id`, `Naziv` (supplier name).

**Aggregation Query (EF Core or raw SQL):**
```sql
-- Step 1: Compute top 15 suppliers globally for date range
WITH TopSuppliers AS (
  SELECT 
    a."IDDobavljac" AS SupplierId,
    d."Naziv" AS SupplierName,
    SUM(ps."Kolicina") AS TotalQty
  FROM "ProdajaStavke" ps
  INNER JOIN "ProdajaZaglavlja" pz ON ps."IdProdaja" = pz."Id"
  INNER JOIN "Artikli" a ON ps."IdArtikal" = a."Id"
  LEFT JOIN "Dobavljaci" d ON a."IDDobavljac" = d."Id"
  WHERE pz."DatumProdaje" >= @fromDate AND pz."DatumProdaje" < @toDate
  GROUP BY a."IDDobavljac", d."Naziv"
  ORDER BY TotalQty DESC
  LIMIT 15
),

-- Step 2: Daily aggregations aligned to top 15 suppliers
DailySuppliers AS (
  SELECT 
    DATE(pz."DatumProdaje") AS sale_date,
    a."IDDobavljac" AS supplier_id,
    d."Naziv" AS supplier_name,
    CASE 
      WHEN EXTRACT(HOUR FROM pz."DatumProdaje") >= 6 AND EXTRACT(HOUR FROM pz."DatumProdaje") < 14 THEN 1
      WHEN EXTRACT(HOUR FROM pz."DatumProdaje") >= 14 AND EXTRACT(HOUR FROM pz."DatumProdaje") < 22 THEN 2
      ELSE 0
    END AS shift,
    SUM(ps."Kolicina") AS qty,
    SUM(ps."Kolicina" * ps."Cena") AS revenue
  FROM "ProdajaStavke" ps
  INNER JOIN "ProdajaZaglavlja" pz ON ps."IdProdaja" = pz."Id"
  INNER JOIN "Artikli" a ON ps."IdArtikal" = a."Id"
  LEFT JOIN "Dobavljaci" d ON a."IDDobavljac" = d."Id"
  WHERE pz."DatumProdaje" >= @fromDate AND pz."DatumProdaje" < @toDate
  GROUP BY DATE(pz."DatumProdaje"), a."IDDobavljac", d."Naziv", shift
),

-- Step 3: For each day, compute shift totals and supplier breakdown
DailyData AS (
  SELECT 
    sale_date,
    COALESCE(SUM(CASE WHEN shift = 1 THEN qty ELSE 0 END), 0) AS first_shift_qty,
    COALESCE(SUM(CASE WHEN shift = 2 THEN qty ELSE 0 END), 0) AS second_shift_qty,
    COALESCE(SUM(CASE WHEN shift = 1 THEN revenue ELSE 0 END) + SUM(CASE WHEN shift = 2 THEN revenue ELSE 0 END), 0) AS day_revenue,
    COALESCE(SUM(qty), 0) AS total_qty
  FROM DailySuppliers
  GROUP BY sale_date
),

-- Step 4: Pivot suppliers as columns (use JSON for dynamic columns)
DailyPivot AS (
  SELECT 
    dd.sale_date,
    dd.first_shift_qty,
    dd.second_shift_qty,
    dd.day_revenue,
    JSON_AGG(
      JSON_BUILD_OBJECT(
        'supplierId', ts."Id",
        'supplierName', ts."Naziv",
        'qty', COALESCE(ds.qty, 0)
      )
    ) AS supplier_breakdown,
    COALESCE(SUM(CASE WHEN ds.supplier_id NOT IN (SELECT SupplierId FROM TopSuppliers) THEN ds.qty ELSE 0 END), 0) AS others_qty,
    dd.total_qty
  FROM DailyData dd
  CROSS JOIN (SELECT DISTINCT "Id", "Naziv" FROM "Dobavljaci" ds2 INNER JOIN TopSuppliers ts ON ds2."Id" = ts.SupplierId) ts
  LEFT JOIN DailySuppliers ds ON dd.sale_date = ds.sale_date AND ts."Id" = ds.supplier_id
  GROUP BY dd.sale_date, dd.first_shift_qty, dd.second_shift_qty, dd.day_revenue, dd.total_qty
)
SELECT * FROM DailyPivot ORDER BY sale_date DESC;
```

**EF Core Projection (Alternative to raw SQL):**
- Use`DbContext.Database.SqlQuery<DailySalesRowDto>()` for complex grouping or construct via LINQ GroupBy + Client evaluation (not recommended for large data).
- Recommendation: Use raw SQL with parameterization to ensure performance.

**Edge Cases & Data Quality:**
1. **Missing IDDobavljac** (NULL suppliers): Include as "Nepoznato" (Unknown) group.
2. **Shift boundary cases** (22:00-06:00): Define shift3 (night shift) or log as off-shift — clarify with user.
3. **Zero sales on a day**: Include row with zeros for all columns.
4. **Store filter (IDObjekat)**: Add `AND pz."IDObjekat" = @storeId` to WHERE clause if provided; optional.
5. **Outliers/bulk corrected sales**: No special handling; include as-is.
6. **Revenue vs Qty mismatch**: Ensure `FirstShiftTotalItems + SecondShiftTotalItems == TotalItemsSold` (qty-based); revenue is independent.

**Indexes & Performance:**
- **Existing (from 025_AddTrendplusPerformanceIndexes.sql):**
  - `idx_prodaja_datum` on `ProdajaZaglavlja(DatumProdaje DESC)` ✓
  - `idx_prodaja_stavke_prodaja` on `ProdajaStavke(IdProdaja)` ✓
  - `idx_prodaja_stavke_artikal` on `ProdajaStavke(IdArtikal)` ✓
  - `idx_artikli_dobavljac` on `Artikli(IDDobavljac)` ✓
- **Additional indexes (if not present):**
  - `idx_dobavljaci_id` on `Dobavljaci(Id)` (join speed)
  - Composite: `idx_prodaja_zaglavlje_date_store` on `(DatumProdaje, IDObjekat)` for store-filtered scans.

**Performance Estimates:**
- **Query time (100K-500K rows in 1-3 month window):** ~200-800ms (PostgreSQL with indexes).
- **SYNC_ROW_LIMIT check:** Depends on date range. If expected rows > 5000, design paging or recommend async export via existing `exportApi`.
- **Materialized View option:** Pre-compute daily aggregates nightly to a `mv_daily_sales_summary` for sub-50ms queries on common ranges (3-12 months).

**Min-Max Range Limits:**
- Recommend enforcing: `toDate - fromDate ≤ 365 days` (or make configurable in backend).
- Return error if user selects >1 year window (suggest rolling month-to-month analysis).
- Reason: Supplier list becomes very large (>15 unique suppliers possible per day on large windows).

**UI Notes**  
- **Header placeholders:** Prva smena: _______  
  Druga smena: _______  
- **Table layout (per-day row):** Date | First shift total (sum) | Second shift total (sum) | Total revenue for day | Top 15 suppliers (each a separate column: number of sold items per supplier) | Others | Total items sold (final column)  
- **Column behavior:** Top 15 supplier columns show supplier name in header and integer count cell per day; if fewer than 15 suppliers on a day, empty or zero cells allowed. "Others" = sum of sold items of all suppliers not in top 15 for that day/date-range. "Total items sold" = sum of First shift total + Second shift total (cross-check) and should equal sum(top15 columns + Others).  
- **UI features:** date-range selector, export CSV/Excel, sort by any column, tooltips explaining "First shift total", "Second shift total", and "Others", responsive layout with horizontal scroll for wide supplier columns.

**DTO / API Contract (Detailed)**

**Backend Endpoint Signature:**
```csharp
app.MapGet("/api/analytics/daily-sales", async (
    TrendplusDbContext db,
    DateTime? fromDate = null,
    DateTime? toDate = null,
    int? storeId = null,
    int topN = 15,
    CancellationToken ct = default
) => { ... })
.WithName("GetDailySalesStats")
.WithOpenApi();
```

**Query String Parameters:**
- `fromDate` (optional, default: 30 days ago): ISO 8601 date (e.g., 2026-01-01).
- `toDate` (optional, default: today): ISO 8601 date.
- `storeId` (optional): Filter by store. If null, all stores.
- `topN` (optional, default: 15, max: 25): Number of top suppliers to return as columns.

**Response DTOs:**

```csharp
public class DailySalesTableResponse
{
    /// ISO 8601, midnight UTC
    public DateTime RequestedFrom { get; set; }
    public DateTime RequestedTo { get; set; }
    
    /// Ordered list of top supplier names (in order by qty descending)
    public List<string> TopSuppliersOrder { get; set; } = new();
    
    /// Rows per day, sorted by Date DESC
    public List<DailySalesRowDto> DateRows { get; set; } = new();
    
    /// Metadata for validation UI
    public DailySalesMetadata Metadata { get; set; } = new();
}

public class DailySalesRowDto
{
    /// ISO 8601 date (Date only, time = 00:00:00 UTC)
    public DateTime Date { get; set; }
    
    /// Sum of items sold during shift 1 (06:00-14:00)
    public int FirstShiftTotalItems { get; set; }
    
    /// Sum of items sold during shift 2 (14:00-22:00)
    public int SecondShiftTotalItems { get; set; }
    
    /// Total revenue (sum of qty * price for day, all shifts)
    public decimal TotalRevenue { get; set; }
    
    /// List of item counts per top supplier (aligned to TopSuppliersOrder)
    /// Length must equal TopSuppliersOrder.Count
    public List<int> TopSupplierCounts { get; set; } = new();
    
    /// Items from suppliers not in top N
    public int OthersCount { get; set; }
    
    /// Total items sold = FirstShiftTotalItems + SecondShiftTotalItems
    /// Must also equal sum(TopSupplierCounts) + OthersCount
    public int TotalItemsSold { get; set; }
}

public class DailySalesMetadata
{
    /// Total days in response
    public int TotalDays { get; set; }
    
    /// Total unique products sold (across all suppliers/days)
    public int UniqueSuppliersInRange { get; set; }
    
    /// Percent of sales with null IDDobavljac (Unknown supplier)
    public decimal UnknownSupplierPct { get; set; }
    
    /// Data quality warnings (if any)
    public List<string> Warnings { get; set; } = new();
}

```

**Validation & Constraints:**
1. **Range validation:** If `(toDate - fromDate) > 365` days, return error: "Max date range is 365 days." 
2. **Consistency check:** `FirstShiftTotalItems + SecondShiftTotalItems == TotalItemsSold`.
3. **Supplier column check:** `sum(TopSupplierCounts) + OthersCount == TotalItemsSold`.
4. **Top N bounds:** If `topN > 25`, cap to 25 (API safety limit); if `topN < 1`, use 1.
5. **Response size:** Warn if >10K rows; suggest date range reduction.
6. **Null handling:** If all rows have zero items, still return valid response (not error).

**UI/Frontend Integration**

**Existing Reuse Components & Patterns:**
- `AnalyticsTableToolbar` (export/print button, filters metadata, orientation).
- `exportApi.generateExport()` (async export for large data).
- `analyticsTableState.resolveAnalyticsTablePayload()` (standard export format).
- Date-range picker (from ShoeTypeSalesStatsPage pattern: preset + custom).
- Store filter dropdown (from existing filters).

**Header UI:**
```
┌─ Header Row ──────────────────────────────────────────────┐
│ Prva smena: _______    Druga smena: _______               │
│ [Date-range selector] [Store filter] [Refresh] [Meni ▼]  │
└───────────────────────────────────────────────────────────┘
    ▼ (Export/Print toolbar from AnalyticsTableToolbar)
```

**Table Structure:**
```
Date | 1st Shift $ | 2nd Shift $ | Day Revenue | Top15 Suppliers (15 cols) | Others | Total Items
────────────────────────────────────────────────────────────────────────────────────────────────
2026-01-05 | 1250 | 980 | 45,230.50 | 125 | 95 | 87 | ... | 2100
2026-01-04 | 1100 | 1050 | 48,900.00 | 140 | 80 | 102 | ... | 2230
...
```

**Column Definitions (for export system):**
```csharp
var columns = new AnalyticsTableColumn<DailySalesRowDto>[] {
    new() { Key = "Date", Header = "Datum", DataType = "date" },
    new() { Key = "FirstShiftTotalItems", Header = "Prva smena (kom.)", DataType = "number" },
    new() { Key = "SecondShiftTotalItems", Header = "Druga smena (kom.)", DataType = "number" },
    new() { Key = "TotalRevenue", Header = "Ukupan prihod", DataType = "currency" },
    // Dynamic supplier columns (generated from TopSuppliersOrder)
    // new() { Key = "TopSupplierCounts[0]", Header = suppliers[0], DataType = "number" },
    // ...
    new() { Key = "OthersCount", Header = "Ostali (kom.)", DataType = "number" },
    new() { Key = "TotalItemsSold", Header = "Ukupno proizvoda", DataType = "number" },
};
```

**Frontend Features:**
- **Sorting:** By date (default DESC), revenue, item totals.
- **Tooltips:** "Prva smena: sum from 06:00-14:00", "Druga smena: sum from 14:00-22:00", "Ostali: suppliers outside top 15".
- **Horizontal scroll:** Table is responsive; supplier columns scroll on small screens.
- **Row highlighting:** Rows with `TotalItemsSold != (sum(TopSupplierCounts) + OthersCount)` shown with warning badge (unlikely, but validates API contract).
- **Export:** Includes all columns, header placeholders, and metadata (date range, filters) in exported document.

**Backend / DB Aggregation Requirements**  
- **Aggregations needed per day:**  
  - Sum of items sold per shift (shift determination rule must be specified), sum of revenue for the day.  
  - Identify top 15 suppliers by items sold for the requested date range (or per day — pick the requirement; recommended: compute top 15 globally for the requested range, then show daily counts for those top 15; if top 15 must be per-day, compute separately per day). Clarify requirement in implementation step.  
  - Compute "Others" as total items sold minus sum(items for top 15).  
- **Performance:** add appropriate indexes for `sale_date`, `supplier_id`, and `shift` columns; consider precomputed daily aggregates or materialized view for large datasets.

**Implementation Steps (Detailed)**

**Phase 1: Backend (Week 1)**

1.1 **Create DTOs** — File: `Api/Models/DailySalesStatsDto.cs`
   - Define `DailySalesTableResponse`, `DailySalesRowDto`, `DailySalesMetadata`.
   - Validate in constructor: `TopSupplierCounts.Count == TopSuppliersOrder.Count`, sums reconcile.

1.2 **Implement Endpoint** — File: `Api/Endpoints/AllEndpoints.cs`
   - Add `MapGet("/api/analytics/daily-sales", ...)` handler.
   - Validation layer: enforce `fromDate ≤ toDate`, range ≤ 365 days, topN ∈ [1, 25].
   - Call service method (see step 1.3).
   - Return `DailySalesTableResponse` with 200 OK or 400 Bad Request.

1.3 **Create Service** — File: `Api/Services/DailySalesService.cs` (new)
   - Method signature:
     ```csharp
     Task<DailySalesTableResponse> GetDailySalesAsync(
         DateTime fromDate, DateTime toDate, int? storeId, int topN,
         CancellationToken ct)
     ```
   - Implement raw SQL query (or EF if performant) using logic from Data Model section.
   - Compute top N suppliers globally.
   - Iterate days; for each day, compute aggregates and align to top N.
   - Apply instrumentation (use existing `SqlCommandLoggingHelper` from phase-1 SQL logging if available).

1.4 **Add Indexes** (if not present) — File: `Database/Migrations/nnn_AddDailySalesIndexes.sql`
   - `CREATE INDEX idx_dobavljaci_id ON "Dobavljaci"("Id");`
   - `CREATE INDEX idx_prodaja_zaglavlje_date_store ON "ProdajaZaglavlja"("DatumProdaje", "IDObjekat");`
   - Apply migration via Entity Framework or direct SQL script.

1.5 **Unit Tests** — File: `Api.Tests/Analytics/DailySalesStatsTests.cs`
   - Test 1: Normal range (10 days, 3+ suppliers).
   - Test 2: Zero sales on a day.
   - Test 3: Unknown supplier (NULL IDDobavljac).
   - Test 4: Single store filter.
   - Test 5: Date range validation (>365 days should error).
   - Test 6: Top N boundary (request N=25, expect clamped).
   - Test 7: Sum reconciliation (FirstShift + SecondShift == Total).
   - Assert response shape matches DTO contract.

**Phase 2: Frontend (Week 1-2)**

2.1 **Create API Client** — File: `Klijent/clientapp/src/services/dailySalesStatsApi.ts`
   ```typescript
   export async function getDailySalesStats(query: {
     fromDate: string;
     toDate: string;
     storeId?: number;
     topN?: number;
   }): Promise<DailySalesTableResponse> {
     // Call GET /api/analytics/daily-sales with params
   }
   
   export interface DailySalesTableResponse {
     requestedFrom: string;
     requestedTo: string;
     topSuppliersOrder: string[];
     dateRows: DailySalesRowDto[];
     metadata: DailySalesMetadata;
   }
   ```

2.2 **Create Page Component** — File: `Klijent/clientapp/src/pages/DailySalesStatsPage.tsx`
   - Import `useCallback`, `useEffect`, `useMemo`, `useState`.
   - State: `fromDate`, `toDate`, `storeId`, `topN`, `isLoading`, `data`, `error`.
   - Fetch data on mount and when filters change (debounce 500ms).
   - Render:
     - Header with placeholder inputs: "Prva smena: _______" and "Druga smena: _______".
     - Date range + store filter controls.
     - Table with dynamic supplier columns (generated from `topSuppliersOrder`).
     - `AnalyticsTableToolbar` for export (reuse existing).
   - Handle errors gracefully (show error toast, allow retry).

2.3 **Create Page CSS** — File: `Klijent/clientapp/src/pages/DailySalesStatsPage.css`
   - Table responsive styles with horizontal scroll for wide supplier columns.
   - Use existing CSS variable system (`--surface-*`, `--text-*`, `--border-*`).

2.4 **Update Navigation** — File: `Klijent/clientapp/src/layout/navConfig.ts`
   - Insert new item after `shoe-type-sales-stats`:
     ```typescript
     {
       to: "/analytics/daily-sales",
       label: "Prodaja po smeni i dobavljačima",  
       icon: ShoppingBag,
     }
     ```

2.5 **Frontend Tests** — File: `Klijent/clientapp/src/pages/__tests__/DailySalesStatsPage.test.tsx`
   - Mock API response.
   - Test 1: Render with sample data (5 days, 15 suppliers).
   - Test 2: Placeholder inputs render correctly.
   - Test 3: Date filter updates trigger refetch.
   - Test 4: Export button triggers `AnalyticsTableToolbar`.
   - Test 5: Validation highlights mismatched sums (if occurs).

**Phase 3: Migration & QA (Week 2-3)**

3.1 **Database Migration Rollout:**
   - Run migration on dev/staging: `dotnet ef database update`.
   - Verify indexes exist: `SELECT * FROM pg_indexes WHERE tablename = 'ProdajaZaglavlja';`.

3.2 **Performance Testing:**
   - Run EXPLAIN ANALYZE on raw SQL query with typical date range (30d).
   - Capture plan; if time >2s, review/add indexes or consider materialized view.

3.3 **QA Checklist:**
   - ✓ Endpoint returns valid JSON matching DTO.
   - ✓ Date range validation works (test >365d error).
   - ✓ Store filter reduces row count correctly.
   - ✓ Top N suppliers are ranked by qty (largest first).
   - ✓ Sum reconciliation: FirstShift + SecondShift == Total.
   - ✓ "Others" is computed correctly (Total - sum(TopN)).
   - ✓ Frontend renders table, sorts by date DESC, exports PDF/Excel/CSV.
   - ✓ Export headers include placeholder row ("Prva smena: ", "Druga smena: ").
   - ✓ Responsive layout: table scrolls horizontally on mobile.

**Phase 4: Documentation & Monitoring (Week 3)**

4.1 **API Documentation** — File: `Api/docs/daily-sales-stats-runbook.md`
   - Endpoint summary, parameters, response contract.
   - Usage examples (curl, .http file).
   - Known limits: max 365-day window, topN ≤ 25.
   - Shift definitions: 1st = 06:00–14:00, 2nd = 14:00–22:00, else = off-shift.

4.2 **Monitoring & Alerts:**
   - Log query time (via `SqlCommandLoggingHelper`).
   - Alert if query time >5s (slow query).
   - Monitor null `IDDobavljac` ratio (log as metric).
   - Set threshold for "UnknownSupplierPct" >20% → log warning.

4.3 **Example Response** — File: `docs/DAILY_SALES_EXAMPLE.json`
   - Include sample response (2-3 days, 15 suppliers) for reference.

**Testing Strategy (Comprehensive)**

**Unit Test Scenarios:**

Backend Tests (xUnit):
```csharp
[Fact]
public async Task GetDailySalesAsync_NormalRange_ReturnsValidResponse()
{
    // Setup: Insert 10 days of sales with 5 suppliers
    // Assert: Response.DateRows.Count == 10
    // Assert: Sum(FirstShift + SecondShift) == TotalItemsSold for each row
}

[Fact]
public async Task GetDailySalesAsync_ZeroSalesDay_IncludesDay()
{
    // Setup: Insert sales only on days 1-5, no day 6
    // Assert: Response.DateRows includes day 6 with zeros
}

[Fact]
public async Task GetDailySalesAsync_UnknownSupplier_IncludedAsNeznato()
{
    // Setup: Articles with NULL IDDobavljac
    // Assert: Response.Metadata.UnknownSupplierPct > 0
}

[Fact]
public async Task GetDailySalesAsync_DateRangeExceedsMax_ReturnsBadRequest()
{
    // Setup: fromDate = 2025-01-01, toDate = 2027-01-01 (>365 days)
    // Assert: HTTP 400, message contains "Max date range"
}

[Fact]
public async Task GetDailySalesAsync_TopNLargerThanMax_Clamps()
{
    // Setup: topN = 50
    // Assert: Response.TopSuppliersOrder.Count <= 25
}

[Fact]
public async Task GetDailySalesAsync_StoreFilter_ReducesRows()
{
    // Setup: Sales for store 1 and 2
    // Act: Query with storeId = 1
    // Assert: Only store 1 data in response
}
```

Frontend Tests (Vitest/React Testing Library):
```typescript
it("renders daily sales table with mock data", () => {
    const mockData: DailySalesTableResponse = {
        dateRows: [{
            date: "2026-01-05",
            firstShiftTotalItems: 150,
            secondShiftTotalItems: 120,
            totalRevenue: 45_000,
            topSupplierCounts: [125, 95, 87, ...],
            othersCount: 50,
            totalItemsSold: 270,
        }],
        topSuppliersOrder: ["Dobavljač A", "Dobavljač B", ...],
    };
    
    const { getByText } = render(<DailySalesStatsPage />);
    // Assert table header contains "Datum", "Prva smena", etc.
    // Assert row values match mock data
});

it("export button triggers AnalyticsTableToolbar", () => {
    // Mock getDailySalesStats API
    // Assert export button exists and is clickable
    // Assert generateExport is called with correct columns
});
```

**Integration Tests:**
- Seed real data (100-500 rows across 30 days, 10 suppliers).
- Run full query (no mocks).
- Validate response structure and calculations.
- Measure query time; threshold: <1s for 30-day window.

**Edge Cases & Handling:**

| Edge Case | Handling |
|-----------|----------|
| NULL IDDobavljac | Include as "Nepoznato" supplier; track in Metadata.UnknownSupplierPct |
| Same supplier multiple rows? | Group by supplier ID; sum item counts |
| Shift boundary (22:00, 06:00) | 22:00-06:00 is off-shift; only count 06:00-14:00 (shift1) and 14:00-22:00 (shift2) |
| No sales on date | Include row with zeros |
| >5000 rows (SYNC_ROW_LIMIT) | Frontend uses existing async export logic; backend returns all rows (no pagination per spec) |
| Daylight saving time | Use UTC consistently (DatumProdaje assumed UTC); no DST adjustment |
| Revenue vs Qty mismatch | Revenue independent of shift breakdown (revenue = qty * price, any shift); document in API docs |

**Known Limitations & Risks:**

1. **Large date range performance:**
   - Risk: Querying 12 months of data with >1M sales rows may timeout.
   - Mitigation: Enforce max 365-day window; warn user if expected data >10K rows.
   - Future: Materialized view for pre-computed daily aggregates.

2. **Supplier list explosion:**
   - Risk: Very large datasets may have >15 unique suppliers per day; topN becomes subset (expected behavior).
   - Mitigation: Document that "Others" can be large on diverse supplier days.

3. **Shift definition hardcoded:**
   - Risk: User requests different shift times (e.g., 08:00-16:00, 16:00-24:00).
   - Mitigation: Currently hardcoded; future enhancement to make configurable via settings.

4. **Data gaps (missing KorisnikIme):**
   - Risk: No link between KorisnikIme and actual worker names (no Workers table schema found).
   - Mitigation: Use KorisnikIme as-is; placeholders stay empty for now (user fills manually in header).

5. **Shift3 (night shift 22:00-06:00):**
   - Risk: Sales outside 06:00-22:00 range are discarded.
   - Mitigation: Document as "off-shift" sales; if night shifts are common, revisit shift definition.

**Rollback Plan:**

If critical bugs discovered in production:
1. Delete endpoint from `AllEndpoints.cs` (comment out MapGet).
2. Remove nav item from `navConfig.ts`.
3. Push hotfix.
4. Investigate; re-deploy when ready.
5. Keep DTO and service code in place (no cleanup needed immediately).

**Future Enhancements:**

1. **Configurable Shift Times:**
   - Add settings table or config file for shift start/end times.
   - UI: Admin page to edit shifts.

2. **Worker-Level Analytics:**
   - Link KorisnikIme to Workers table (if created).
   - Drill-down by worker (how much each worker sold per day, per shift).

3. **Materialized View for Performance:**
   - Create `mv_daily_sales_summary` refreshed nightly.
   - Query MV for common ranges; fallback to live query for edge ranges.

4. **Supplier Profiling:**
   - Add "top supplier trend" mini-chart (sparkline).
   - Compare supplier performance month-to-month.

5. **Multi-Store Comparison:**
   - Pivot stores as columns (current design pivots suppliers).
   - Side-by-side store performance.

6. **Pre/Post Analysis:**
   - Link to nivelacija changes (similar to shoe-type-sales-stats).
   - Show impact of supplier price/availability changes on sales.

---

**Acceptance Criteria & Sign-Off**

**Backend Acceptance:**
- ✅ Endpoint `GET /api/analytics/daily-sales` exists and returns 200 OK.
- ✅ DTO contract validation: `TopSupplierCounts.Count == TopSuppliersOrder.Count`.
- ✅ Sum reconciliation: `FirstShiftTotalItems + SecondShiftTotalItems == TotalItemsSold` for all rows.
- ✅ Date range validation: Request with >365 day range returns HTTP 400.
- ✅ Top N clamping: topN > 25 → capped to 25 in response.
- ✅ Store filter: storeId parameter correctly reduces data.
- ✅ Unit tests: ≥8 test cases, all passing (see Testing Strategy).
- ✅ Query performance: EXPLAIN ANALYZE shows <1s execution on 30-day window.

**Frontend Acceptance:**
- ✅ Page renders at `/analytics/daily-sales`.
- ✅ Nav item appears in Analitika menu at position 3 (after shoe-type-sales-stats).
- ✅ Header displays: "Prva smena: _______" and "Druga smena: _______" placeholders.
- ✅ Table displays columns: Date, First Shift, Second Shift, Day Revenue, [Top 15 suppliers], Others, Total Items.
- ✅ Supplier column headers match `TopSuppliersOrder` names and order.
- ✅ Default date range: last 30 days (preset).
- ✅ Store filter dropdown works; refetches data on selection.
- ✅ Sort by Date (DESC default), Revenue, Total Items — all work.
- ✅ Export button visible and triggers `AnalyticsTableToolbar`.
- ✅ Export to PDF/Excel/CSV includes all visible columns and metadata (date range, filters, shift placeholders).
- ✅ Responsive layout: table scrolls horizontally on <1024px screens.
- ✅ Error handling: displays user-friendly error messages (not tech stack traces).
- ✅ Loading state: spinner visible while fetching; debounced (500ms) to avoid excessive requests.
- ✅ Frontend tests: ≥5 test cases, all passing (see Testing Strategy).

**Data Quality Acceptance:**
- ✅ Metadata.UnknownSupplierPct calculated and returned.
- ✅ Data warnings logged if >20% suppliers are unknown.
- ✅ Zero-sales days included in response.
- ✅ Sum totals validated on backend and frontend (highlight mismatches if they occur).

**Documentation Acceptance:**
- ✅ API runbook (daily-sales-stats-runbook.md) published.
- ✅ Example response (DAILY_SALES_EXAMPLE.json) provided.
- ✅ Shift definitions documented (06:00-14:00, 14:00-22:00).
- ✅ OpenAPI/Swagger generated for endpoint.

**Optional (Not Required for MVP):**
- ⚪ Configurable shift times (future enhancement).
- ⚪ Worker-level analytics drill-down (future enhancement).
- ⚪ Materialized view for large datasets (future optimization).

**Timeline:**
- Phase 1 (Backend): 3-5 days.
- Phase 2 (Frontend): 3-5 days.
- Phase 3 (QA & Migration): 2-3 days.
- Phase 4 (Docs & Monitoring): 1-2 days.
- **Total: 9-15 days.**

**Deliverables Checklist:**
- [ ] Backend endpoint deployed and tested.
- [ ] Frontend page deployed and tested.
- [ ] Nav item visible in Analytics section.
- [ ] All unit/integration tests passing (backend + frontend).
- [ ] Database indexes deployed.
- [ ] Export (PDF/Excel/CSV) working.
- [ ] API runbook published.
- [ ] Example response documented.
- [ ] Production monitoring enabled (slow query alerts).

**Sign-Off:**
- Feature Owner: ___________________
- QA Lead: ___________________
- DevOps/DBA: ___________________
- Date: ___________________

---

**Next Steps & Quick Start:**
1. ✅ **Plan Complete** — This document fully specifies the new Daily Sales analytics feature.
2. **Assignment:** Assign backend developer to Phase 1 (DTO + endpoint + service).
3. **Assignment:** Assign frontend developer to Phase 2 (page component + nav + tests).
4. **GitHub Issues:** Create issues for each phase (Backend: DTO/Endpoint/Indexes/Tests; Frontend: Page/Nav/Tests).
5. **Kickoff:** First team sync to clarify any blockers and assign work (target: next business day).
6. **Development:** Parallel backend + frontend work; integrate via shared API contract (DTO).
7. **QA:** Week 2-3 quality assurance and performance tuning.
8. **Deploy:** Staging → Production (with monitoring enabled).