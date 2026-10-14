# RQ664 browser/export proof — targeted audit hardening

Date: 2026-10-14
Repository baseline: `485261b78cbd7e0fd8fdc27e7339f3b9720ee3fe`
Task: post-close audit correction of RQ664's **synthetic-only** local proof; no RQ448 certification.

## Finding

The original `assertCsvMatchesFixture` accepted a CSV containing a generic `Promet` label, `Negativni`/`Nepoznat`, and **any** `150` or `240` anywhere in its text. A missing or altered revenue/quantity row could still pass. Its deliberate tamper check only compared a changed in-memory displayed-total field to a constant, not the captured export data. That limited the strength of the claimed route → export parity rehearsal.

## Repair

- Assert every exported Supplier/Shoe Type **revenue/quantity pair** matches the corresponding independent fixture API-response rows, irrespective of sort order; ensure exported sums equal the declared response totals and exported entity identifiers are present as labels.
- Assert the received downloaded CSV bytes equal the captured frontend export payload's CSV serialization, and that the CSV row count is exact (rather than `>= 3`).
- Tamper with one **captured export payload row**, then execute the same independent source-response reconciliation function and require it to reject the corruption.
- Update the stale RQ448 queue note from `RQ664 READY` to `RQ664 DONE` without promoting RQ448, RQ451, RQ452, RQ454/RQ455, RQ565 or RQ566.

## Verification to require before merge

1. `node --check Klijent/clientapp/scripts/qa/rq448-fixture-smoke.mjs`
2. `cd Klijent/clientapp && npm ci --no-audit --no-fund && npm run build && npm run qa:rq448-fixture-smoke`.
3. `node scripts/check-prompt-queues.mjs` and `node scripts/check-agent-instructions.mjs`, plus relevant PR CI.
4. Verify exported Supplier and Shoe Type row counts remain 2/2, unknown and negative-ID cases remain visible, corrupted row rejection occurs, and `unexpectedApiCount` stays 0.

These are required verification commands, **not results claimed by this document**. The original RQ664 verdict is historical evidence from before this hardening change; do not present it as a test execution of the new assertions without a fresh run.

## Unchanged external gates

RQ448 remains `WAITING`. A completed independent deployed certificate still needs the approved read-only raw-source/database oracle, exact deployed frontend/backend SHA and schema, authenticated browser/API session, route/detail/export/screenshots bound to the same evidence ID, genuine freshness and customer signoff. The intercepted `/api/documents/generate` in this smoke is **not** a production document-service test. Do not mint VERIFIED/production or customer-accepted evidence from it.
