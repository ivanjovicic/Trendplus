# PERF14 Evidence

- Date: 2026-08-12
- Prompt: PERF14 - Unblock D6 import-overlap evidence
- Pack: PERF14-G10-import-overlap-01
- Milestone / mode: G10 / dedicated
- Dataset: trendplus_perf_m (M-PERF-01)
- Import fixture: C:\Users\Ivan\Downloads\Trend plus.mdb
- Staged fixture: C:\Users\Ivan\AppData\Local\Temp\perf14-import.mdb
- Import fixture size: 11689984 bytes
- Raw JSON: .ai/runs/2026-08-12-PERF14-raw.json

## Method

1. Start the web API on port 8080 and the worker process on port 8081.
2. Submit POST /api/access-import/run with the fixture file and admin key.
3. Poll GET /api/access-import/jobs/{batchId} until terminal.
4. Probe GET /api/analytics/cached/dashboard/bootstrap?dataScope=all during the import window.
5. Sample GET /api/workers/configuration from the worker process.

## Dimension status

| Id | Status | Result |
|---|---|---|
| D1 | cite_PERF11 | already measured |
| D2 | cite_PERF10 | already measured |
| D3 | cite_PERF10 | already measured |
| D4 | cite_PERF12 | already measured |
| D5 | cite_PERF13 | already measured |
| D6 import overlap | **measured** | analytics probes during import: 360; batch stayed running through harness window |
| D7 | n/a | not exercised in this pack |
| D8 | `n/a_dedicated` | MT-owned |

## Interpretation

1. D6 is measured because analytics probes succeeded while the batch was in running status.
2. The worker process on port 8081 kept the import job honest without inventing a fixture.
3. The batch did not reach terminal state within the harness window, so that remains a residual risk but not a D6 overlap blocker.

## Files

- tmp/perf14_measure.ps1
- .ai/runs/2026-08-12-PERF14-raw.json
- .ai/runs/2026-08-12-PERF14-evidence.md
