# Lost Sales Validation Contract

Date: 2026-08-04
Repo: `ivanjovicic/Trendplus`
Status: contract locked by RQ03

## Source hierarchy

1. `vw_analytics_oos_lost_sales` (unfiltered `dataScope=all`, no store/supplier)
2. Recent-sales + current-stock SQL fallback
3. Unavailable (no connection / query failure / no readable row)

## SourceStatus vocabulary

| SourceStatus | Meaning | Validation Status when estimate ≤ 0 | LostSalesEstimate |
|---|---|---|---|
| `view` | Trusted view returned a positive estimate | n/a (positive path) | numeric |
| `true_zero` | Trusted view returned zero | `good` | `0` |
| `fallback` | Fallback SQL used | `warning` (not green) | numeric, including `0` |
| `unavailable` | Evidence missing | `insufficient_data` | `null` (never fake zero) |

Positive estimates (`view` or `fallback`):

- `< 50000` → `warning`
- `>= 50000` → `critical`

## Non-negotiable

- Unavailable must never be presented as `good` or as `0 RSD`.
- Fallback zero is not the same as trusted true zero.
- Shared vocabulary with SQL queue Q80; do not invent a second status model.

## Tests

- `Api.Tests/LostSalesValidationSourceStatusTests.cs`

## Verification

- 2026-08-05: Q80 verified against the current endpoint behavior in `Api/Endpoints/CachedAnalyticsEndpoints.cs`.
- `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "FullyQualifiedName~LostSalesValidationSourceStatusTests"` - pass
