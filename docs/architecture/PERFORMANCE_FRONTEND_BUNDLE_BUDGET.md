# Frontend Bundle Budget Contract

Status: measured contract for the current dedicated-customer pilot frontend

Owner: `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md` (`PERF17`)

## Baseline

Measured on 2026-09-02 from current `main` commit `a1631e736fefcb414f1211b89afcedf135b6532e`:

```text
cd Klijent/clientapp
npm run typecheck
npm run build
```

The production build transformed 2,597 modules and produced the following largest JavaScript assets:

| Asset family | Raw size | Gzip | Interpretation |
|---|---:|---:|---|
| `recharts-*.js` | 548.04 kB (548,036 bytes) | 164.13 kB | expected shared chart-library exception |
| `index-*.js` | 207.72 kB | 59.96 kB | main application runtime |
| `SupplierConsolidatedPage-*.js` | 142.20 kB | 37.19 kB | lazy route chunk |
| `InventoryPage-*.js` | 134.96 kB | 31.24 kB | lazy route chunk |
| `InsightStudioPage-*.js` | 109.14 kB | 22.88 kB | lazy route chunk |

The application routes are already loaded with React `lazy()`. Recharts is deliberately isolated by the existing Vite `manualChunks` rule, so chart code is fetched with chart-bearing routes rather than included in the main application entry.

## Budget and enforcement

- The existing Vite 500 kB warning remains visible and is not raised.
- `recharts-*.js` has a measured exception budget of 560,000 raw bytes. The exception has 11,964 bytes of headroom over the 548,036-byte baseline and fails on further unreviewed growth.
- Every other JavaScript asset must remain at or below 500,000 raw bytes; a new oversized non-chart asset fails the check.
- Run `npm run build` followed by `npm run check:bundle-budget` from `Klijent/clientapp`.
- The check reads generated asset sizes, ignores content hashes and fails when the expected shared chart chunk is absent or exceeds its measured exception.

## Rejected alternative

Removing the existing Recharts `manualChunks` rule reduced the largest individual asset to 349.48 kB in a trial build, but Rollup emitted circular-dependency warnings for Recharts exports and warned that execution order could break. That variant is not accepted without a separate runtime proof and is not part of `PERF17`.

## Boundary

This is a frontend measurement/guardrail contract. It does not change analytics calculations, API contracts, worker behavior, tenant authority or the Vite warning threshold. CI wiring and further Recharts import-level optimization require a separate, explicitly promoted follow-up with runtime/browser proof.
