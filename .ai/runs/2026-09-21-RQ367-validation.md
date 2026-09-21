# RQ367 generated validation evidence

- Schema version: 1
- Task: RQ367
- Commit: b419dad3baac0d8c6afbf3780ea8150e714f9860
- Generated: 2026-09-21T15:44:30.266Z

## Validation results

### tests — **PASS**
- validation evidence fixtures: **PASS**; exitCode=0; command=`npm run test:validation-evidence` —   ... # Subtest: main verification distinguishes exact tip and ancestor commits ok 3 - main verification distinguishes exact tip and ancestor commits   ---   duration_ms: 90.768166   ... # Subtest: CLI writes JSON evidence and exits non-zero for a failed command ok 4 - CLI writes JSON evidence and exits non-zero for a failed command   ---   duration_ms: 43.359752   ... 1..4 # tests 4 # suites 0 # pass 4 # fail 0 # cancelled 0 # skipped 0 # todo 0 # duration_ms 1211.034096

### guardrails — **PASS**
- prompt queue validator: **PASS**; exitCode=0; command=`node scripts/check-prompt-queues.mjs` — OK: prompt-queue governance checks passed (506 tasks).
- frontend analytics guardrails: **PASS**; exitCode=0; command=`npm run check:analytics-guardrails` — > clientapp@0.0.0 check:analytics-guardrails > npm run check:encoding && npm run check:analytics-guardrails:self-test && node ./scripts/check-analytics-guardrails.mjs && npm run typecheck > clientapp@0.0.0 check:encoding > node ./scripts/check-encoding.mjs OK: No mojibake detected in maintained docs/frontend/backend analytics surfaces. > clientapp@0.0.0 check:analytics-guardrails:self-test > node ./scripts/check-analytics-guardrails.mjs --self-test OK: Guardrail baseline self-test passed. OK: Guardrails baseline-only (51 known violation(s), 0 removed). > clientapp@0.0.0 typecheck > tsc -b

### build — **ENVIRONMENT-BLOCKED**
- diff check: **PASS**; exitCode=0; command=`git diff --check`
- backend build and test: **ENVIRONMENT-BLOCKED**; exitCode=null; command=`dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release` — dotnet is unavailable in the VM; backend proof remains CI-only

## Exit codes
- validation evidence fixtures: 0
- prompt queue validator: 0
- frontend analytics guardrails: 0
- diff check: 0
- backend build and test: null

## Skipped
- none

## Main verification
- Status: **EXACT-TIP**
- Commit: b419dad3baac0d8c6afbf3780ea8150e714f9860
- Ref: origin/main
- Ref SHA: b419dad3baac0d8c6afbf3780ea8150e714f9860
- Contains commit: true

## Environment
- {"cwd":"/workspace","node":"v22.14.0","platform":"linux","arch":"x64","hostname":"cursor"}
