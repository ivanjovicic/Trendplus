# Supply-Chain Assurance Policy

Status: authoritative SEC04 policy
Date: 2026-08-11
Backlog slice: `S2-2` in `docs/architecture/SECURITY_ASSURANCE_BACKLOG_PLAN.md`
Roadmap: `docs/roadmaps/SECURITY_EVOLUTION_ROADMAP.md`
Ownership map: `docs/architecture/SECURITY_OWNERSHIP_THREAT_MAP.md`

## Purpose

Define the supply-chain assurance policy for the current supported runtime surfaces in Trendplus, with explicit ecosystem coverage, severity gates, exception handling and accepted-risk formatting.

This document is policy and evidence guidance only. It does **not**:

- implement dependency upgrades, lockfile churn or runtime remediation;
- claim CI wiring already exists;
- turn missing scan output into PASS;
- re-home STAB, MT, QDB or BCI ownership;
- introduce secrets or operational credentials.

## Ownership boundary

SEC owns the policy, severity thresholds, triage rules and accepted-risk vocabulary.

BCI is a collaborator for future pipeline wiring only. BCI may own workflow/job implementation later, but BCI does not own the supply-chain policy decision, severity gate or exception approval.

STAB remains the owner of current pilot/release security defects.
MT remains the owner of shared-SaaS tenant isolation.
QDB remains the owner of connector credential behavior.

## Supported ecosystem coverage

The policy covers the dependency graphs that are actually supported in this repo today:

| Ecosystem | Supported surface | Reproducible scan command(s) | Notes |
|---|---|---|---|
| .NET 8 solution | `Trendplus2.sln` covering `Api`, `Application`, `Domain`, `Infrastructure`, `Workers`, `Api.Tests`, `Trendplus.POS` | `dotnet restore Trendplus2.sln` then `dotnet list Trendplus2.sln package --vulnerable --include-transitive` and `dotnet list Trendplus2.sln package --outdated --include-transitive` | Primary backend/runtime graph |
| npm/Vite frontend | `Klijent/clientapp` | `npm ci --prefix Klijent/clientapp` then `npm audit --prefix Klijent/clientapp --audit-level=high` | Primary analytics client |
| npm/Vite POS UI | `Trendplus.POS.Ui` | `npm ci --prefix Trendplus.POS.Ui` then `npm audit --prefix Trendplus.POS.Ui --audit-level=high` | Secondary UI/runtime surface |
| repo-root npm tooling | root `package.json` + `package-lock.json` | `npm ci` then `npm audit --audit-level=high` | Tooling package only; keep in scope while the lockfile exists |

Not currently in scope:

- Python packages, because no first-class Python runtime surface is present today;
- container/base-image scanning, because no shipping container policy is currently established here;
- ad hoc dependency checks outside the lockfile-backed manifests above.

## Severity gates

The policy fails closed. Missing data is not green.

| Signal | Gate | Result |
|---|---|---|
| `critical` in any shipped runtime dependency tree | fail | merge blocked until fixed, pinned or accepted-risked |
| `high` in any shipped runtime dependency tree | fail | merge blocked unless a documented accepted-risk entry exists |
| `moderate` in a direct or reachable shipped dependency | fail | triage required; may only proceed with an explicit accepted-risk entry |
| `moderate` in dev-only or test-only tooling | warn | track and review, but do not call PASS from silence |
| `low` | warn | log and review in the next SEC planning cycle |
| package metadata, advisory feed or registry status cannot be resolved | unknown | not PASS; rerun later or record the unknown state explicitly |

Shipped runtime dependency tree means a package that is part of the supported backend or UI build/runtime surface above.

## Exception handling

Only three exception outcomes are allowed:

1. `pin` - lock to a fixed version when a safe version exists and no behavioral change is required.
2. `replace` - swap the package when the package is abandoned, unavailable or the vulnerability cannot be closed by pinning.
3. `accepted-risk` - time-box the issue when neither pinning nor replacing is viable immediately.

Rules:

- accepted risk never becomes silent PASS;
- accepted risk must have an expiry date and a named reviewer;
- accepted risk must include compensating controls and the reason remediation is deferred;
- accepted risk cannot hide missing scan output;
- BCI may wire the workflow that runs the scan, but BCI does not approve the security exception itself;
- a direct package that is abandoned or unavailable should move to `replace` unless a short-lived accepted-risk entry is explicitly approved.

## Reproducible scan procedure

Run the scan commands for the supported ecosystems above against the current repo state and retain the exact output in the evidence pack.

Minimum order:

```text
dotnet restore Trendplus2.sln
dotnet list Trendplus2.sln package --vulnerable --include-transitive
dotnet list Trendplus2.sln package --outdated --include-transitive
npm ci --prefix Klijent/clientapp
npm audit --prefix Klijent/clientapp --audit-level=high
npm ci --prefix Trendplus.POS.Ui
npm audit --prefix Trendplus.POS.Ui --audit-level=high
npm ci
npm audit --audit-level=high
```

Interpretation rules:

- do not claim PASS when the command was not run;
- do not claim PASS when the command failed to resolve advisory data;
- do not convert `warn` into `pass` just because the scan output is noisy;
- keep the raw output path or command transcript with the evidence pack when possible.

## CI placeholders

The repo does not claim these workflows already exist.

When BCI wires pipeline support later, the job placeholders should be named so that they are easy to map back to the policy:

- `SEC-supply-chain-dotnet`
- `SEC-supply-chain-npm-clientapp`
- `SEC-supply-chain-npm-posui`
- `SEC-supply-chain-npm-root`

BCI owns the workflow mechanics if/when those jobs are added. SEC owns the policy thresholds and the triage decision.

## Accepted-risk template

Use one record per package or advisory.

```text
| Field | Value |
|---|---|
| Risk ID | SC-YYYYMMDD-<short id> |
| Ecosystem | dotnet / npm-clientapp / npm-posui / npm-root |
| Manifest | path to the lockfile-backed manifest |
| Package | package name and version |
| Severity | critical / high / moderate / low |
| Reachability | direct shipped / transitive shipped / dev-only / test-only |
| Advisory or evidence | CVE/GHSA/NuGet advisory link or scan output reference |
| Decision | pin / replace / accepted-risk |
| Why accepted now | short business or technical reason |
| Compensating controls | what reduces exposure until fix lands |
| Owner | named SEC owner |
| Code owner | named product/code owner |
| Reviewer | named reviewer approving the exception |
| Expiry | YYYY-MM-DD |
| Next review | YYYY-MM-DD |
| Follow-up | next action to close or revisit the risk |
```

Rules for accepted-risk records:

- keep them in docs/evidence only;
- do not store secrets, tokens or customer payloads;
- do not use them to waive unrelated advisories;
- expire them quickly enough that they stay reviewable, not permanent.

## Validation rules

- every supported ecosystem above has at least one reproducible scan command;
- missing advisory data is `unknown`, not green;
- severity gates are explicit before CI wiring exists;
- BCI collaboration is named without moving policy ownership;
- no runtime remediation is implied by this document alone.

## Acceptance

- a durable supply-chain assurance policy exists for the supported runtime surfaces;
- severity gates and exception handling are explicit;
- reproducible scan commands and CI placeholders are named;
- later SEC prompts can reuse the same vocabulary without re-analysis.
