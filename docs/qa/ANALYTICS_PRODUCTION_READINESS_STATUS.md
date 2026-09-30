# Analytics Production Readiness Status

> This document is generated from the machine-readable snapshot. It is not a hand-maintained global release verdict.
> A missing, expired, SHA-mismatched or externally blocked proof is intentionally not rendered as PASS.

- Snapshot generated: 2026-09-30T07:52:28.039Z
- Repository SHA: `d393c63695f7f28a1d77c32b71c1de1c6b8d36ad`
- Deployed SHA: `unknown / not supplied`
- Overall state: **blocked**
- Schema/contract/context generations: `{"schema":"operations-analytics-integrity-schema-v1","contract":"analytics-readiness-contract-v1","context":"analytics-context-fingerprint-policy-v1"}`
- Expiry policy: Code evidence: 7 days. Browser/provider/runtime evidence: 24 hours unless a source-specific expiry is shorter.

## Evidence by family

| Family | State | Evidence | Checked SHA | Checked at | Expires | Limitation |
| --- | --- | --- | --- | --- | --- | --- |
| readiness-tooling | **code-ready** | .ai/runs/2026-09-30-RQ514-readiness-validation.json | `d393c63695f7f28a1d77c32b71c1de1c6b8d36ad` | 2026-09-30T07:52:06.607Z | 2026-10-07T07:52:06.607Z | Code-level tooling proof does not imply deployed or browser proof. |
| frontend-runtime | **stale-evidence** | docs/qa/ANALYTICS_PRODUCTION_READINESS_STATUS.md (historical evidence reference) | `817964c63560eb7f442b8c57b0099544d8667a97` | 2026-06-19T17:20:00.000Z | 2026-06-20T17:20:00.000Z | Retained historical live evidence; it is not current release proof. |
| provider-runtime | **blocked** | Current run environment | `unknown` | unknown | unknown | No connected browser/provider session is available for current runtime proof. |
| operations-integrity | **blocked** | RQ513 enrolled-family evidence | `unknown` | unknown | unknown | Inventory, Data Quality and Decision Board probes are explicitly unverified until executed. |
| prepost-runtime | **blocked** | Q83 live-schema gate | `unknown` | unknown | unknown | Q83 is PARTIAL; this readiness snapshot does not promote RQ491 or bypass the gate. |

## Limitations

- No deployed SHA or connected provider/browser session was supplied for this repository-local run.
- Q83 remains PARTIAL and its live-schema gate is not bypassed; Pre/Post production verification is blocked.
- RQ513 integrity families without an executed probe remain unverified rather than green.

## State semantics

- `code-ready`: current repository evidence is fresh and SHA-bound.
- `runtime-unproven`: code evidence is current, but matching deployed/runtime proof is absent.
- `verified-current`: required runtime proof is fresh and matches repository, deployment and generations.
- `stale-evidence`: evidence expired or no longer matches SHA/generation/deployment.
- `blocked`: proof was skipped, unavailable, integrity-unverified or externally gated.
- `failed`: executed evidence failed.
