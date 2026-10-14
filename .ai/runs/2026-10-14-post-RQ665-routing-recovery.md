# Post-RQ665 current-main queue review and qualified promotion — 2026-10-14

- Read-only inspection base: `a159d579135fe881263f9325937c863e1a6cc83d` (GitHub main at 2026-10-14).
- Open pull requests at inspection: none; tracked `.ai/task-locks` files: none. GitHub cannot inspect other agents' uncommitted local working files; READY is not a reservation.
- Delivery mode: direct user-instructed queue/documentation repair, **no active runtime task claimed**.
- Product boundary: footwear-retail decision-grade analytics first; no new marketplace/AI/tenant/planned POC features and no production mutation.

## Verified completion and current CI

- RQ662 DONE via `b48fc1cf`: Supplier Sales scoped store-filter loading/failure, tests, and previous-scope protection.
- RQ663 DONE via `11fc16ba`/merge `275d1c30`: Daily Sales missing vs partial vs unavailable shift counts, quality/trust consistency and tests.
- RQ664 DONE via `c3751208`: local **synthetic** Puppeteer Supplier/Shoe render→CSV smoke; PR #159 `a159d579` hardened independently asserted fixture row/amount parity, actual downloaded bytes and tampered-row detection, made it a CI quality-gate step.
- RQ665 DONE via PR #158: test-only EF ManyServiceProvidersCreatedWarning convention; no production DbContext behavior change.
- Main `a159d579135fe881263f9325937c863e1a6cc83d` Analytics Quality Gates `38587580206` SUCCESS, Planning Governance `38587580373` SUCCESS. Backend suite `38586045195` SUCCESS on prior tested main; do not imply the latest docs commit was re-tested against PostgreSQL.
- The previous zero-READY run `.ai/runs/2026-10-14-zero-ready-recovery-evidence.md` did correctly keep externally gated old task states WAITING/BLOCKED. Its conclusion about *existing named prompts* is not permission to stop searching for concrete newly discovered code gaps after a separate user audit.

## Source-confirmed new risks (not assumed production incidents)

1. **RQ666 P1 READY:** `ProductDecisionCenterPage.tsx` builds persisted queue/status `sourceKey` from product + action + period + store + supplier, **omitting `dataScope`**, while fetching data and supplier choices for `dataScope`. Existing imported and existing rows can reuse the same key (conditional on backend action identity policy); `getStores()` runs only on mount without active scope even though the API supports `getStores(true,dataScope)`. Needs failing-first same/cross-scope/legacy-action tests, data authority decision and versioned compatibility plan; do not bulk migrate saved actions.
2. **RQ667 P1 READY:** `InventoryPage.tsx` fetches `getStores(true)` without `inventoryDataScope`, does not include scope as a dependency, and its scope-change event only clears `selectedSupplierId` while retaining selected and comparison store IDs. New-scope inventory API list/balance/action/forecast/comparison requests receive those retained IDs before they are validated. Needs failing-first filter lineage/compareStore tests and same-scope counterexample; zero real DB writes.
3. Documentation stale: the product playbook/scope still described RQ659/RQ661 as open and fixed Home figures as live though code+queue already report DONE; corrected the stale product timeline. RQ448 note said PR #159 hardening was in progress although merged/CI green; corrected note without promotion.

## Old prompts re-evaluated, gate class and action

- `RQ448` P0 WAITING, `RQ565` P0 WAITING: RQ664 local browser fixtures now run in CI, but deployed authenticated API/browser, raw source reconciliation and exact runtime SHA remain unavailable. Do not falsify production certification.
- `RQ451`, `RQ452`, `RQ454`, `RQ455`, `RQ566` WAITING: require RQ448/source or approved operator/deployment/customer evidence.
- `RQ610` BLOCKED: source-keyed `Artikli` current row does not prove reliable generation across Access/POS/manual/transfer updates; do not schedule false observed stock.
- `STAB16` BLOCKED and `PERF16` BLOCKED: provider deploy/worker/read access and isolated shared-SaaS/MT10 fixtures respectively are real start gates.
- `RQ530`, `RQ601`, `RQ602`, `RQ606` PARTIAL: missing authoritative supplier buying source, original cost calculations, mature real cohorts and original MDB exceptions; no safe status promotion by doc edits.
- `RQ531`, `RQ506`, `RQ558`, `RQ559`, `SEC05` WAITING: require explicit owner scorecard/snapshot/privacy policy or proven source dimensions/cohorts; the user's generic ask to find claimable work does not waive product-owner authorizations.
- RQ/SQL/BCI/P-UI core DONE owners not reopened; MT/GAI future scope not activated. New tasks RQ666/RQ667 target unrelated, observable page-specific filter/action correctness; no duplicate RQ660/RQ662/664/665 implementations.

## Post-registration routing

Primary `RQ666 READY`, independent `RQ667 READY`. Neither claimed; before work, refresh `origin/main`, check remote branches/PRs, shared queue reservations and local locks; reserve exactly one prompt via READY→IN_PROGRESS on current main. Feature files do not overlap. Do not let a historical `Current READY: none` line override the current canonical queue. After either task finishes, rerun full post-close dependency cascade. Exact CI for this queue-only commit is Planning Governance, not backend/production certification.
