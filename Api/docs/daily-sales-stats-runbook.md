# Daily Sales Stats Runbook

## Endpoint

- Method: `GET`
- Path: `/api/analytics/daily-sales`
- Tag: `Analytics`
- Rate limit policy: `analytics`

## Purpose

Daily table for analytics decision support:

- per-day sold items in first shift (`06:00-14:00`)
- per-day sold items in second shift (`14:00-22:00`)
- total daily revenue
- top-N suppliers (global rank for requested range) shown as daily item-count columns
- `Others` count for suppliers outside top-N
- daily total item reconciliation

## Query Parameters

- `fromDate` (optional, `YYYY-MM-DD`, UTC date only); `from` is an alias
- `toDate` (optional, `YYYY-MM-DD`, UTC date only); `to` is an alias
- `storeId` (optional, integer)
- `topN` (optional, default `15`, clamped to `1..25`)
- `dataScope` (optional, one of `all | existing | imported`, default `all`)

If no date range is provided, default window is last 30 days (inclusive).
When both spellings for one boundary are supplied, `fromDate` / `toDate` take precedence over `from` / `to` respectively.

## Validation Rules

- `fromDate <= toDate`
- max range is `365` days (inclusive)
- `topN` is clamped to safe bounds `1..25`

## Data Semantics

- Top suppliers are ranked by **total sold items** in the full requested range.
- Supplier columns are aligned by `TopSuppliersOrder` and remain stable across all day rows.
- `OthersCount = TotalItemsSold - sum(TopSupplierCounts)`.
- `TotalItemsSold = FirstShiftTotalItems + SecondShiftTotalItems`.
- Rows for days without sales are still returned with zero values.
- Shift assignment is performed in the configured `DailySales:TimeZoneId` business timezone.
  The deployment default is `UTC`; production is configured as `Europe/Belgrade`.
- `metadata.shiftTimeZone` is the timezone actually used. `metadata.shiftTimestampBasis`
  reports `utc_instant`, `legacy_access_wall_clock`, `mixed`, or `unknown`.
- `metadata.shiftTimestampBasisKnownRows` and `shiftTimestampBasisUnknownRows` expose
  affected source-line counts. Unknown-basis lines remain in daily/supplier totals but are
  not assigned to a shift; affected daily shift cells are null.

## Shift Definition

- Shift 1: `06:00 <= hour < 14:00`
- Shift 2: `14:00 <= hour < 22:00`
- Off-shift (`22:00-06:00`) is excluded from shift item totals and reported in metadata:
  - `offShiftItems`
  - `offShiftRevenue`
  - warnings list
- If source rows contain no classifiable hour, existing RQ383 behavior is preserved: daily totals
  remain visible, shift cells are null, and `shiftAssignmentStatus=no_time_fallback`.
- Access imports use `legacy_access_wall_clock`: the imported source wall clock is preserved and
  is not offset-converted a second time. True UTC sources use timezone conversion, including DST.

## Response Contract (summary)

`DailySalesTableResponse`

- `requestedFrom`, `requestedTo`
- `storeId`, `topN`, `dataScope`
- `topSuppliers[]` (id/name/totalQty/totalRevenue)
- `topSuppliersOrder[]` (header labels)
- `dateRows[]` (daily table rows)
- `metadata` (quality + consistency info)

## OpenAPI/Swagger

The endpoint is registered with:

- `Produces<DailySalesTableResponse>(200)`
- `Produces(400)`

Swagger UI location:

- `/swagger`

## Indexing & Performance Notes

Daily-sales path relies on existing analytics indexes plus two additional indexes:

- `IX_prodaja_zaglavlje_id_objekat_datum_prodaje`
- `IX_prodaja_stavke_id_prodaja_id_artikal_cover_qty_price`

Important:

- `Dobavljaci.Id` is already indexed by PK, so no duplicate index is introduced.

## Expected Runtime

Typical windows (30-90 days, medium dataset) should stay under ~1s query time.
For very large windows or high concurrency, keep `topN` conservative and maintain 365-day cap.

## Troubleshooting

1. 400 Bad Request on date range:
   - verify `fromDate` <= `toDate`
   - verify range <= 365 days
2. Unexpectedly high unknown share:
   - check article supplier mapping quality (`IDDobavljac`)
3. Off-shift warning appears:
   - inspect sales timestamps outside 06-22 operational window
4. Unknown timestamp basis:
   - inspect `source_timestamp_basis` and `data_origin` on `prodaja_zaglavlje`
   - verify `shiftTimestampBasisUnknownRows`; those rows intentionally have no shift assignment
5. Slow query:
   - verify indexes exist and run `EXPLAIN ANALYZE` on staging/production snapshot
