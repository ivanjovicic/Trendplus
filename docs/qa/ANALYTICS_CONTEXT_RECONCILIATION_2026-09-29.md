# Analytics context reconciliation — 2026-09-29

Scope: `RQ509` deterministic, read-only context identity and differential proof.

Machine-readable implementation:

- `Application/Analytics/AnalyticsContextFingerprintPolicy.cs`
- `Application/Analytics/AnalyticsContextReconciliation.cs`
- `Api.Tests/AnalyticsContextFingerprintTests.cs`

The descriptor is additive `AnalyticsResponseMeta.context` metadata. Its SHA-256 fingerprint is derived from normalized requested/effective/observed period, half-open UTC boundary, scope, declared population filters, source dataset/generation, formula/contract version, materializer/cache generation and row-limit semantics. Missing source/generation or an unavailable result fails closed with no comparable fingerprint.

The read-only reconciliation result contains both endpoint identities, metric key, context fingerprint, expected/actual values, exact decimal delta, classification and explanation:

| Classification | Meaning | Numeric delta |
|---|---|---|
| `reconciled` | Same fingerprint and exact deterministic value match | `0` |
| `unexplained_delta` | Same fingerprint but values differ or one value is missing | exact delta when both values exist |
| `non_comparable_context` | Fingerprints differ, so populations/source generations are not comparable | omitted |
| `unavailable_context` | Either side has no comparable fingerprint | omitted |

The deterministic test fixture exercises all four states. It uses synthetic values only; no customer/source data is committed and no production mutation or live-schema proof is implied. A legitimate cohort difference remains visible as `non_comparable_context` rather than being forced into a numeric mismatch.
