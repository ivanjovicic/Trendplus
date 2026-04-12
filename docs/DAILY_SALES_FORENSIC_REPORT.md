# Forensic Investigation: `/analytics/daily-sales` Data Integrity

> Superseded on 2026-04-13 by a follow-up verification against the current `Trend plus.mdb` snapshot and Neon PostgreSQL.
>
> Corrected conclusion for the disputed dates `2026-03-15`, `2026-03-20`, `2026-03-21`, `2026-03-23`, `2026-03-26`, and `2026-03-27`:
>
> - Access MDB truth matches PostgreSQL imported truth for all disputed dates.
> - The frontend table renders backend values directly; no frontend date-shift or aggregation bug was confirmed.
> - The earlier claim that `(IdProdaja, IdArtikal, Cena)` repeats prove import corruption was too strong. The same repeated line patterns exist in the Access source and can represent legitimate sold quantity.
> - The dates that looked "inflated" are explained by source documents already present in MDB, including non-standard receipts such as `korekcija` on `2026-03-23` and `DUG` on `2026-03-26`.
> - Reimport is not required for the disputed dates.
>
> The code was updated to remove the false-positive duplicate-line warning from `DailySalesStatsService`, because that warning could misclassify legitimate repeated source rows as data corruption.

**Date:** 2026-04-13  
**Status:** ROOT CAUSES IDENTIFIED, FIXES APPLIED  
**Database State:** EMPTY (all data deleted by `cleanup-non-access` between Apr 4–12)

---

## 1. Executive Summary

The daily sales analytics page showed inflated revenue/quantity values for some dates due to **duplicate prodaja_stavke rows** in the PostgreSQL database. The duplicates were created by repeated Access imports before composite-key dedup protection was added to the import pipeline. A secondary issue—multiple zaglavlje (receipt headers) for the same logical sale—contributed to additional inflation on specific dates.

---

## 2. Data Path Trace

```
Frontend                  Backend                        Database
─────────                 ───────                        ────────
DailySalesStatsPage.tsx   GET /api/analytics/daily-sales
  ↓ API call              DailySalesStatsEndpoints.cs
dailySalesStatsApi.ts       ↓ IMemoryCache (2min TTL)
  params: fromDate,         DailySalesStatsService.cs
          toDate,             ↓ EF Core LINQ query
          storeId,          4-way JOIN:
          topN,               prodaja_stavke ps
          dataScope             INNER JOIN prodaja_zaglavlje pz
                                INNER JOIN "Artikli" a
                                LEFT JOIN "Dobavljaci" d
                              GROUP BY (Date, Hour, SupplierId)
                              SUM(Kolicina), SUM(Kolicina * Cena)
```

---

## 3. Ground Truth (Captured Before DB Wipe)

### Raw Daily Totals (prodaja_stavke JOIN prodaja_zaglavlje)

| PG Date    | Receipts | Lines | Qty | Revenue    |
|------------|----------|-------|-----|------------|
| 2026-03-15 | 1        | 14    | 14  | 59,560     |
| 2026-03-17 | 1        | 12    | 12  | 74,360     |
| 2026-03-18 | 1        | 8     | 8   | 58,350     |
| 2026-03-19 | 1        | 6     | 6   | 25,660     |
| 2026-03-20 | 1        | 24    | 24  | 122,520    |
| 2026-03-21 | 1        | 7     | 7   | 34,050     |
| 2026-03-23 | 2        | 15    | 15  | 82,450     |
| 2026-03-24 | 1        | 16    | 16  | 64,470     |
| 2026-03-26 | 3        | 40    | 40  | 232,840    |
| 2026-03-27 | 1        | 11    | 11  | 56,710     |

### Artikli JOIN: IDENTICAL (zero orphan stavke — INNER JOIN does not exclude any rows)

### Global Stats: 65,689 stavke, 5,451 zaglavlja, 12,341 artikli

---

## 4. Duplicate Stavke Evidence

**7,690 excess duplicate rows globally** across 6,045 composite-key groups.

### Duplicates in Disputed Date Range

| PG Date    | IdProdaja  | IdArtikal | Cena   | Count | Excess Revenue |
|------------|------------|-----------|--------|-------|----------------|
| 2026-03-15 | 1773581689 | 21466     | 3,290  | 2     | 3,290          |
| 2026-03-18 | 1773861156 | 19258     | 3,200  | 2     | 3,200          |
| 2026-03-19 | 1773947687 | 21452     | 1,990  | 2     | 1,990          |
| 2026-03-20 | 309539323  | 21300     | 4,900  | **3** | 9,800          |
| 2026-03-20 | 309539323  | 21468     | 3,900  | 2     | 3,900          |
| 2026-03-20 | 309539323  | 21431     | 9,490  | 2     | 9,490          |
| 2026-03-20 | 309539323  | 21332     | 320    | 2     | 320            |
| 2026-03-21 | 1774101929 | 21466     | 3,290  | 2     | 3,290          |
| 2026-03-23 | 1774360785 | 21362     | 6,500  | **3** | 13,000         |
| 2026-03-24 | 1774382840 | 21450     | 1,990  | **5** | 7,960          |
| 2026-03-26 | 1774549739 | 21465     | 9,200  | 2     | 9,200          |
| 2026-03-27 | 1774638869 | 21411     | 3,200  | 2     | 3,200          |

---

## 5. Raw vs Deduped Comparison

| PG Date    | Raw Lines | Dedup Lines | Excess | Raw Revenue | Dedup Revenue | Excess Revenue |
|------------|-----------|-------------|--------|-------------|---------------|----------------|
| 2026-03-15 | 14        | 13          | 1      | 59,560      | 56,270        | 3,290          |
| 2026-03-17 | 12        | 12          | 0      | 74,360      | 74,360        | 0              |
| 2026-03-18 | 8         | 7           | 1      | 58,350      | 55,150        | 3,200          |
| 2026-03-19 | 6         | 5           | 1      | 25,660      | 23,670        | 1,990          |
| 2026-03-20 | 24        | 19          | 5      | 122,520     | 99,010        | 23,510         |
| 2026-03-21 | 7         | 6           | 1      | 34,050      | 30,760        | 3,290          |
| 2026-03-23 | 15        | 13          | 2      | 82,450      | 69,450        | 13,000         |
| 2026-03-24 | 16        | 12          | 4      | 64,470      | 56,510        | 7,960          |
| 2026-03-26 | 40        | 39          | 1      | 232,840     | 223,640       | 9,200          |
| 2026-03-27 | 11        | 10          | 1      | 56,710      | 53,510        | 3,200          |

---

## 6. Disputed Dates Analysis

| Date   | UI Shows      | User Expected | Raw DB  | Dedup DB | Analysis |
|--------|---------------|---------------|---------|----------|----------|
| 03-15  | 14/0/59,560   | No sales      | 59,560  | 56,270   | Date issue in source data or import |
| 03-20  | 24/0/122,520  | Correct       | 122,520 | 99,010   | User sees raw (inflation hidden) |
| 03-21  | 7/0/34,050    | Correct       | 34,050  | 30,760   | User sees raw (inflation hidden) |
| 03-23  | 15/0/82,450   | 62,950        | 82,450  | 69,450   | Excess from dupes + possible zaglavlje duplication |
| 03-26  | 40/0/232,840  | 106,110       | 232,840 | 223,640  | 3 receipts where 1–2 expected → zaglavlje duplication |
| 03-27  | 11/0/56,710   | Correct       | 56,710  | 53,510   | User sees raw (inflation hidden) |

### Key Observation: 2026-03-26

3 receipts on this date: `1300985439` (44,580), `1774549739` (99,150), `1774554936` (89,110) = 232,840 total.  
User expected 106,110. Even after stavke dedup (223,640), still ~2.1× expected → **receipt-level duplication**.

---

## 7. Hypothesis Test Results

| # | Hypothesis | Verdict | Evidence |
|---|-----------|---------|----------|
| H1 | UTC+1 timezone shifts dates +1 | **DISPROVEN** | `ConvertToDate` uses `SpecifyKind(UTC)` — no offset adjustment. Dates stored verbatim. |
| H2 | Wrong date field used in import | **INCONCLUSIVE** | `SynthesizeProdajaFromDnevnikAsync` uses `DnevnikPromena.Datum` which could differ from sale date. Cannot verify—DB empty. |
| H3 | Duplicate stavke (line-level) | **CONFIRMED** | 7,690 excess rows globally. Every disputed date had duplicates. |
| H4 | Duplicate zaglavlje (receipt-level) | **CONFIRMED** | 2026-03-26: 3 receipts for same date, total 2.1× expected. `SynthesizeProdajaFromDnevnikAsync` creates separate zaglavlje when `BrojRacuna` is null. |
| H5 | JOIN multiplication (Artikli) | **DISPROVEN** | Artikli JOIN result identical to raw totals. Zero orphan stavke. |
| H6 | Cache serving stale data | **DISPROVEN** | Cache TTL = 2 minutes. Not a factor. |
| H7 | Frontend date display error | **DISPROVEN** | Frontend creates `Date(year, month-1, day)` from string. No timezone conversion. |
| H8 | DataScope filter excludes valid data | **DISPROVEN** | All artikli have matching DataOrigin. |
| H9 | Shift resolution loses data | **DISPROVEN** | All hours=0, all mapped to shift 1. No data loss. |
| H10 | Multiple imports accumulated duplicates | **CONFIRMED** | Import ran without dedup protection, creating cumulative duplicates across re-imports. |
| H11 | SynthesizeProdajaFromDnevnikAsync creates extra zaglavlje | **CONFIRMED** | When `BrojRacuna` is null, each DnevnikPromena entry creates its own zaglavlje (`DN-{d.Id}` key). |

---

## 8. Root Causes

### Root Cause 1: Duplicate prodaja_stavke (CONFIRMED)
- **Mechanism:** `ImportProdajaStavkeAsync` ran multiple times before composite-key dedup was added. When `sourceId=0`, new IDs were generated for each import, bypassing the ID-based dedup.
- **Impact:** 7,690 excess rows globally. Revenue inflated by 3,200–23,510 per date.
- **Status:** Composite-key dedup was added to `ImportProdajaStavkeAsync` (fixed). `SynthesizeProdajaFromDnevnikAsync` had NO dedup (now fixed).

### Root Cause 2: Duplicate prodaja_zaglavlje (CONFIRMED)
- **Mechanism:** `SynthesizeProdajaFromDnevnikAsync` groups DnevnikPromena entries by `BrojRacuna`. When `BrojRacuna` is null/empty, each entry gets key `DN-{d.Id}`, creating a **separate zaglavlje per dnevnik entry**.
- **Impact:** Dates like 2026-03-26 had 3 receipts (232,840 total) where only 1–2 were expected (106,110).
- **Status:** Structural issue in synthesize path. Risk exists on re-import if DnevnikPromena entries lack `BrojRacuna`.

### Root Cause 3: Analytics service has no dedup protection (CONFIRMED)
- **Mechanism:** The main aggregation query (`GROUP BY Date, Hour, SupplierId`) sums ALL stavke rows including duplicates.
- **Impact:** All duplicate rows flow through to UI totals.
- **Status:** Defense-in-depth duplicate detection warning added.

---

## 9. Import Layer Analysis

### Three Import Paths for Prodaja

| Path | Method | DatumProdaje Source | ID Source | Stavke Dedup |
|------|--------|--------------------|-----------|----|
| A | `ImportProdajaAsync` | Access `datumprodaje`/`datum`/`saledate` | Original Access ID | ✅ Composite-key |
| B | `ImportProdajaFromLineTableAsync` | `DnevnikPromena.Datum` fallback to row | `sourceSaleId` (IDDnevnik) | ✅ Composite-key |
| C | `SynthesizeProdajaFromDnevnikAsync` | `DnevnikPromena.Datum` | `first.Id` or `maxId+1` | ✅ **NOW FIXED** |

### Timezone Handling
`ConvertToDate()` uses `DateTime.SpecifyKind(dt, DateTimeKind.Utc)` — stamps raw Access datetime as UTC **without offset adjustment**. A local time `2026-03-14 00:00:00 CET` becomes `2026-03-14T00:00:00Z`, preserving the date correctly.

### Pre-import Reset
`ResetAccessSalesSnapshotAsync` deletes all `data_origin='access'` sales data before full import. On re-import from empty state, cumulative duplicates cannot occur.

---

## 10. Analytics Service Analysis

### Main Aggregation Query
```
prodaja_stavke ps
  INNER JOIN prodaja_zaglavlje pz ON ps.IdProdaja = pz.Id
  INNER JOIN Artikli a ON ps.IdArtikal = a.Id  
  LEFT JOIN Dobavljaci d ON a.IDDobavljac = d.Id
WHERE pz.DatumProdaje >= @from AND pz.DatumProdaje < @toExclusive
GROUP BY (Date, Hour, SupplierId)
SELECT SUM(Kolicina), SUM(Kolicina * Cena)
```

- **No stavke dedup** — all rows (including duplicates) contribute to sums.
- **INNER JOIN on Artikli** — orphan stavke excluded (but none found in this dataset).
- **Date grouping** by `pz.DatumProdaje.Date` — correct for UTC midnight timestamps.
- **Shift resolution** — all hour=0 → shift 1 fallback (correct for Access data without time).

---

## 11. Frontend Analysis

- Dates sent as `YYYY-MM-DD` strings (no timezone info).
- Dates displayed via `fmtDate()` using `new Date(year, month-1, day)` — **local construction, no UTC shift**.
- `totalItemsSold` comes directly from API response, no frontend recalculation.
- **No frontend-side date manipulation or display issues.**

---

## 12. Database State

The database was completely wiped between Apr 4–12 by a `cleanup-non-access` operation:
- `deleted_rows_archive`: 227,707 rows (209,572 stavke + 18,133 zaglavlje)
- All production tables now have 0 rows
- Data must be re-imported from Access

---

## 13. Fixes Applied

### Fix 1: Composite-key dedup in SynthesizeProdajaFromDnevnikAsync
**File:** `Api/Services/AccessImportService.cs`  
Added the same composite-key dedup pattern that exists in `ImportProdajaStavkeAsync` to the synthesize path. This prevents re-inserting stavke that already exist in PostgreSQL when running repeated imports.

### Fix 2: Duplicate detection warning in DailySalesStatsService
**File:** `Api/Services/DailySalesStatsService.cs`  
Added defense-in-depth detection that queries for duplicate stavke (same IdProdaja/IdArtikal/Cena) within the requested date range. If duplicates are found, a Serbian-language warning is included in the API response, alerting users that totals may be inflated.

### Build Status: ✅ 0 errors, 0 warnings

---

## 14. Recommendations

1. **Re-import from Access** — With the DB empty and dedup protection in all import paths, a fresh import should produce clean data.
2. **Verify BrojRacuna population** — Check Access DnevnikPromena entries in the disputed date range. If `BrojRacuna` is consistently populated, the synthesize path will correctly group entries. If null for many entries, consider alternative grouping (e.g., by Datum+KorisnikIme).
3. **Post-import validation** — After next import, run the ground-truth queries from `tmp/investigate.js` to verify totals match Access expectations.
4. **Monitor warnings** — The new duplicate detection warning in the analytics response will flag any future duplicate issues immediately.
