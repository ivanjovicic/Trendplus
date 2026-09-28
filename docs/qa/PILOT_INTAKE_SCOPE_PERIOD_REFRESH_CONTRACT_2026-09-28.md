# Pilot intake scope, period and refresh contract

Date: 2026-09-28  
Owner: Analytics Reliability / Data Quality  
Prompt: RQ466

## Canonical behaviour

- `scope` takes precedence over `dataScope` on the durable report route.
- The accepted values are `all`, `existing` and `imported`, case-insensitive and whitespace-trimmed. Any other value returns `invalid_scope`; it is never silently treated as `all`.
- `existing` includes `DataOrigin = existing`, null or empty origin. `imported` includes `DataOrigin = access`. The same predicate is applied to article counts and sale-header revenue/counts.
- Pilot health metrics use the requested half-open interval `[fromDate, toDate + 1 day)`. They do not use a second now-anchored lookback window.
- `StoresCount` is the number of distinct stores represented by the filtered sales rows; an explicitly selected store remains one store even when the period has no rows.
- `lastRefreshAtUtc` is only `LastSuccessfulRefreshAtUtc` from refresh status. Report generation time is not a refresh event, so it stays null when the last successful refresh is unknown.
- Recommended actions are emitted only when the corresponding issue exists, in canonical order. An empty/clean report has no synthetic action list.
- The durable report route accepts `refresh=true` to bypass the cached report and then stores the newly generated result under the normal cache key. This is the backend cache-bypass contract for a future UI refresh action; the frontend owner decides when to send the flag.
- Durable-report failures are returned through the existing safe error payload and logged with report ID and filter dimensions, without persisting exception text in the response.

## Proof

Focused backend proof is recorded in `.ai/runs/2026-09-28-RQ466-evidence.md`:

- `AnalyticsDataQualityHealthServiceTests`: explicit requested-window regression and existing scope compatibility, 10/10 passed.
- `AnalyticsReportsContractTests`: scope validation, invalid-scope short circuit, conditional actions and existing report contracts, 52/52 passed.
- `Api.csproj` build: passed; existing analyzer warnings remain unrelated to RQ466.

RQ467 remains the owner of readiness/population policy details called out as out of scope in its addendum, including default period anchoring and DUG/KOREKCIJA/cost-fallback semantics.
