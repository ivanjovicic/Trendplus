# Forecast snapshot provenance contract (RQ97)

Date: 2026-08-20  
Repo: `ivanjovicic/Trendplus`

## Verdict

`analytics_inventory_forecast_snapshot` has a **read-only** API path (`GetInventoryForecastHandler`). Repository inspection on current `main` found **no proven production materializer/model owner**. RQ97 therefore ships a **fail-closed** provenance contract rather than inventing a writer.

## Status codes

| `ProvenanceStatus` | Meaning on current main |
|---|---|
| `missing_relation` | Relation absent (`42P01`). `SnapshotAvailable=false`. |
| `owner_unknown` | Relation readable. Materializer not proven. `MaterializerOwner=none`. `IsAuthoritativeForecast=false`. |
| `stale` | Reserved until a writer exposes freshness lineage that can be evaluated. |
| `trusted` | Reserved for proven generated evidence. **Not reachable** until a real materializer lands. |

## Operator rules

- `GeneratedAtUtc` is response time, not snapshot freshness.
- `SnapshotFreshnessUtc` stays null while ownership is unproven.
- Readable rows must not be marketed as a production forecasting product.
- Nightly analytics workers must not be implied as owners of this table without code evidence.

## Follow-up

RQ98 landed a fail-closed baseline/backtesting contract; measured scores stay unavailable until a trusted forecast materializer and paired outcome window exist.
