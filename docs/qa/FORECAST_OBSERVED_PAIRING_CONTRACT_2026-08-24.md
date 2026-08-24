# Forecast observed pairing contract (RQ117)

Date: 2026-08-24
Repo: `ivanjovicic/Trendplus`

## Verdict

The forecast/observed pairing path on current main is fail-closed.
Trusted pairings exist only when the forecast snapshot and observed daily stock row match the same scoped window.

## Pairing semantics on current main

| Pairing status | Meaning |
|---|---|
| `paired_observed` | Forecast and observed evidence match the requested sku/store/size/date window. |
| `missing_observed_window` | No observed row exists for the requested window, so the comparison stays unavailable. |
| `unavailable_untrusted_forecast` | The forecast provenance is not trusted, so observed data must not be marketed as a production-authoritative comparison. |
| `unavailable_non_observed_basis` | The basis cannot be paired to a real observed record. |

## Provenance semantics

- `missing_relation` stays the explicit no-table / unavailable case.
- `owner_unknown` stays the explicit readable-but-unproven case.
- `stale` remains explicit and non-authoritative.
- `trusted` is the only authoritative provenance state.

## Operator rule

- Do not borrow observed evidence from a different store, sku, or period.
- Do not collapse missing or stale pairing evidence into zero or trusted comparison values.

