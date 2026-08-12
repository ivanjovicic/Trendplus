# PERF07 Evidence

- Date: 2026-08-12
- Prompt: PERF07 - Capture bootstrap section timings on M-tier
- Dataset: `trendplus_perf_m`
- Sample: `PERF07-01`
- Raw JSON: `.ai/runs/2026-08-12-PERF07-raw.json`

## Result

Cold bootstrap on M-tier completed and emitted section timing logs for all bootstrap sections.

- Cold bootstrap wall time: `7236.04 ms`
- HTTP status: `200`
- Response meta: `success=true`, `isPartial=true`, `warningCode=ANALYTICS_PARTIAL_DATA`
- Data quality: `good`
- Raw response totals:
  - `totalRevenue = 2,159,940,000.00 RSD`
  - `totalTransactions = 45,000`
  - `totalUnits = 360,000`
  - `totalSkuCount = 12,015`

## Section Summary

Measured section time by priority:

- P0 total: `1978.11 ms` (`27.3%` of wall time)
- P1 total: `4811.25 ms` (`66.5%` of wall time)
- P2 total: `204.19 ms` (`2.8%` of wall time)
- Total measured section time: `6993.55 ms` (`96.6%` of wall time)

### P0 sections

| Section | ms | Success | Errors |
|---|---:|---:|---:|
| Summary | 758.23 | True | 0 |
| Inventory | 467.60 | True | 0 |
| DailySales | 669.53 | True | 0 |
| Advanced | 43.54 | False | 2 |
| ProductDecisionCenter | 39.21 | False | 3 |

### P1 sections

| Section | ms | Success | Errors |
|---|---:|---:|---:|
| PaymentData | 1057.85 | True | 0 |
| SupplierData | 941.76 | True | 0 |
| QuickInsights | 707.61 | False | 1 |
| HourData | 629.59 | True | 0 |
| WeekdayData | 554.47 | True | 0 |
| CategoryData | 391.18 | True | 0 |
| GenderData | 312.19 | True | 0 |
| TransactionStats | 203.61 | True | 1 |
| TopAdvanced | 12.99 | True | 3 |

### P2 sections

| Section | ms | Success | Errors |
|---|---:|---:|---:|
| SupplierOptions | 184.80 | True | 0 |
| ValidationCompleteness | 5.74 | True | 3 |
| ValidationFreshness | 5.68 | True | 3 |
| ValidationLostSales | 3.57 | True | 3 |
| Executive | 2.50 | True | 3 |
| DecisionActions | 1.90 | True | 3 |

## Notes

- The cold request returned `isPartial=true` because three sections hit schema gaps:
  - `QuickInsights`: `a.MinimalnaKolicina` missing
  - `Advanced`: `p.DataOrigin` missing
  - `ProductDecisionCenter`: `a.MinimalnaKolicina` missing
- The section timing hook is working and captures `section`, `priority`, `elapsedMs`, `success`, and `errors` on the cold outer cache miss.
- This prompt intentionally did not change cache TTLs, indexes, or partial semantics.

## Files

- `Api/Endpoints/CachedAnalyticsEndpoints.cs`
- `tmp/perf07_measure.ps1`
- `.ai/runs/2026-08-12-PERF07-raw.json`
