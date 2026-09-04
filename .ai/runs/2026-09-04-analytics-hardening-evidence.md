# Analytics hardening evidence

- Date: 2026-09-04
- Queue: direct-user-request
- Owner: analytics backend/frontend boundary
- Branch: `main`, HEAD before delivery: `8ceb345c`
- Delivery: local worktree only; no commit, push, deployment or target-branch verification was performed.

## Interpreted outcome

Harden the existing Trendplus analytics decision surfaces so missing, malformed,
stale, partial and fallback evidence cannot be presented as trusted metrics or
actionable recommendations. Preserve valid zero values and keep backend-owned
decision metadata authoritative.

## Changed and reviewed

- Added backend `RecommendationAllowed` to recommendation/meta projections and
  fail-closed recommendation engine output.
- Removed query/generation-time values from `LastRefreshAtUtc` construction in
  supplier stats and daily sales metadata; cache metadata remains the refresh
  source where available.
- Preserved nullable denominator-derived shares, cost coverage, split coverage,
  confidence and reliability across supplier, color, shoe-type and pre/post
  nivelacija UI mappings.
- Made shared analytics formatters reject both `NaN` and positive/negative
  `Infinity`.
- Kept empty response handling distinct from errors and kept blocked/fallback
  recommendation rationale visible.
- Added/updated regression fixtures and tests for recommendation ownership,
  refresh provenance, null/valid-zero and non-finite display behavior.
- Added the screen mapping and trust matrix at
  `docs/qa/ANALYTICS_TRUST_SCREEN_MATRIX_2026-09-04.md`.

## Validation

Passed:

- `npm run check:analytics-guardrails`
- `npm run typecheck`
- `npm run build` (frontend production build; existing large-chunk warning)
- focused frontend metric test: `5/5`
- focused analytics frontend screens/services: `35/35` after fixture contract
  updates
- focused backend analytics tests: `76/76`
- refresh-provenance follow-up tests: `22/22` with no console warnings
- `dotnet build Trendplus2.Backend.slnf --no-restore --configuration Release`
  (0 errors; 94 existing analyzer warnings)
- `git diff --check` (no whitespace errors; Git emitted line-ending notices)

Failed or not completed:

- Full backend `Api.Tests` result: `1061 passed, 16 failed, 0 skipped`.
  Failures are environment/baseline issues including missing SQL connection
  string, Neon authentication, SQL Server availability, endpoint test-host
  binding drift and unrelated SQL expectation drift.
- Full `npm run test:analytics` was started with a valid single-thread Vitest
  configuration but produced no test output for over two minutes and was
  terminated. This is not treated as a pass.
- Browser console/theme/chart zero-size smoke was not proven in this shell.
- Production API, refresh workers, migration state and deployed SHA were not
  accessible/proven in this run.
- Final static search found no maintained analytics `lastRefreshAt` assignment
  from `generatedAt`.

## Remaining acceptance blockers

- `RQ128`: deployed Product Decision Center/Decision Board actionability parity.
- `RQ132`: dashboard support-signal explanation.
- `RQ137`: canonical requested/effective/observed period lineage.
- `RQ138`: authoritative Trend Models evaluation contract.
- `STAB16`: exact-deploy parity, durable refresh workers/history and production
  migration/provider state.

## Truthful conclusion

Local backend/frontend hardening is implemented and the focused proof is green,
but the overall master prompt is not 100% complete. The remaining items require
production/runtime evidence or separate backend contracts and stay explicitly
open rather than being marked done.
