# RQ367 generated validation evidence

- Schema version: 1
- Task: RQ367
- Commit: 8ba4d9f6fcdd613924620a061e755ef29173f7e1
- Generated: 2026-09-21T12:10:33.566Z

## Validation results

### tests — **PASS**
- validation evidence fixtures: **PASS**; exitCode=0; command=`npm run test:validation-evidence` —   ... # Subtest: main verification distinguishes exact tip and ancestor commits ok 3 - main verification distinguishes exact tip and ancestor commits   ---   duration_ms: 79.824822   ... # Subtest: CLI writes JSON evidence and exits non-zero for a failed command ok 4 - CLI writes JSON evidence and exits non-zero for a failed command   ---   duration_ms: 43.222777   ... 1..4 # tests 4 # suites 0 # pass 4 # fail 0 # cancelled 0 # skipped 0 # todo 0 # duration_ms 1204.175323
- backend test: **SKIPPED**; exitCode=null; command=not executed — dotnet is unavailable in the VM; backend proof remains CI-only

### guardrails — **PASS**
- prompt queue validator: **PASS**; exitCode=0; command=`node scripts/check-prompt-queues.mjs` — OK: prompt-queue governance checks passed (506 tasks).

### build — **PASS**
- diff check: **PASS**; exitCode=0; command=`git diff --check`

## Exit codes
- validation evidence fixtures: 0
- prompt queue validator: 0
- diff check: 0
- backend test: null

## Skipped
- backend test: dotnet is unavailable in the VM; backend proof remains CI-only

## Main verification
- Status: **EXACT-TIP**
- Commit: 8ba4d9f6fcdd613924620a061e755ef29173f7e1
- Ref: origin/main
- Ref SHA: 8ba4d9f6fcdd613924620a061e755ef29173f7e1
- Contains commit: true

## Environment
- {"cwd":"/workspace","node":"v22.14.0","platform":"linux","arch":"x64","hostname":"cursor"}
