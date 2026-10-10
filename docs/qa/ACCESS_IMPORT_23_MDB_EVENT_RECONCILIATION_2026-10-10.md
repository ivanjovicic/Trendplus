# Access import #23 — MDB event and skipped-row reconciliation

Date: 2026-10-10  
Scope: read-only PostgreSQL reconstruction and code audit; no import, MDB write, data repair or production mutation  
Database: local Docker PostgreSQL `trendplus-postgres`, database `trendplus`  
Batch: `DataImportBatches.Id = 23`

## Executive result

The target-side evidence does not support collapsing the 16 repeated receipt-number groups. Each group contains distinct source IDs and distinct line populations. The 4,654 repeated `DnevnikPromena` source-ID groups are also not duplicate rows in the business sense: a source event/document expands to multiple product/event rows; transfers correctly expand to one outgoing and one incoming event per source transfer.

A real import defect is confirmed in the re-import guard for `ImportPrenosRobeAsync`: it deduplicates by `(TipPromene, ArtikalId, Datum, Iznos)` and does not include source table, source row/document identity, or store endpoints. The minimal repair is to make source lineage the primary idempotency key and retain the composite key only as a legacy fallback when source identity is unavailable. No repair was applied in this audit.

The exact batch-23 MDB is unavailable. The recorded worker path was deleted after completion (`deleteWorkingFileAfterCompletion=true`), and no exact replacement was found locally. Therefore the 244 individual skipped source rows and all 20 receipt IDs cannot be truthfully classified from `tblProdaja`/`DnevnikPromena` in this run. Two other local MDB files were read-only inspected but were rejected as evidence because their hashes/counts differ and they contain none of the batch warning IDs.

## Batch and coverage facts

Recorded batch summary:

| Source population | Source rows | Accepted | Skipped | Target writes | Interpretation |
|---|---:|---:|---:|---:|---|
| `dnevnik_promena` | 16,620 | 16,452 | 168 | 16,452 | 168 source journal rows are not present in the target; exact source-row reasons require the MDB |
| `tblProdaja` / header view | 67,336 | 67,092 | 244 | 10,897 | 67,092 accepted line rows are grouped into 5,530 receipt headers plus updates; 244 rows have no usable journal/date link |
| `tblProdaja` / lines | 67,336 | 67,092 | 244 | 67,092 | accepted line count is preserved |
| `prenos_robe` | 8,962 | 8,962 | 0 | 17,924 | deliberate 2× expansion: outgoing + incoming |

The batch warning says the 244 rows belong to 20 receipts and were skipped to avoid using import time as sale date. That is the correct fail-closed policy. The code must not backfill those rows with the import date.

The target has 5,530 Access receipt headers and 67,092 Access receipt lines. `SalesLineFacts` has 67,092 Access lines with non-null, unique `SourceLineId` and 286 `existing` lines with null source identity. The operational `prodaja_stavke` rows themselves have null `source_row_id` because the legacy `tblProdaja` shape has no independent line ID; the analytics writer currently assigns the analytical source-line identity separately. This distinction must remain explicit.

## All 16 repeated receipt-number groups

The exact groups found in the batch target are:

| Date | Number | Store | Headers | Source IDs |
|---|---|---:|---:|---|
| 2017-02-15 | `39` | 2082886995 | 2 | 1490398422, 1490716880 |
| 2017-08-02 | `1127` | 551466791 | 2 | 446372651, 738181457 |
| 2017-08-02 | `1128` | 551466791 | 2 | 48480574, 996960400 |
| 2018-12-30 | `Korekcija` | 2082886995 | 2 | 63630942, 1970271985 |
| 2019-06-03 | `DUG` | 2082886995 | 2 | 998966909, 1559550472 |
| 2019-07-23 | `Korekcija` | 2082886995 | 3 | 643304270, 1305154438, 1370895334 |
| 2019-07-24 | `Korekcija` | 2082886995 | 4 | 305802024, 758268395, 1322484013, 1563983129 |
| 2020-12-26 | `Korekcija` | 2082886995 | 3 | 950996410, 1609016316, 1893475907 |
| 2021-12-26 | `Korekcija` | 2082886995 | 2 | 1640515549, 2101469574 |
| 2022-04-16 | `Korekcija` | 2082886995 | 3 | 308147181, 728984537, 1451476670 |
| 2022-12-16 | `DUG` | 2082886995 | 2 | 1671187433, 1671222243 |
| 2022-12-20 | `Korekcija` | 2082886995 | 2 | 1027663075, 1416144997 |
| 2023-08-13 | `Korekcija` | 2082886995 | 2 | 1691929137, 1856000579 |
| 2024-09-11 | `Korekcija` | 2082886995 | 2 | 1564238475, 1868981978 |
| 2024-11-26 | `DUG` | 2082886995 | 2 | 1732617930, 1763293512 |
| 2025-12-27 | `Korekcija` | 2082886995 | 2 | 428885561, 1345044542 |

The line signatures differ within every group. Several events also contain repeated product lines within the same source event (for example the same article and price occurring twice or four times). Those are legitimate multisets and must not be reduced to a set.

Case-insensitive target classification of all 355 non-standard documents is:

| Class | Headers | Amount (RSD) |
|---|---:|---:|
| `DUG` | 262 | 4,821,220 |
| `Korekcija` | 43 | 970,610 |
| other non-standard | 50 | 1,991,520 |
| Total | 355 | 7,783,350 |

The warning's `DUG` count/amount (`261`, `4,803,340`) is case-sensitive; the target-side case-insensitive classification includes one additional `dug`/`Dug` row. This is a diagnostic-count inconsistency, not evidence that rows should be deleted.

## Journal and transfer identity

Batch #23 target counts:

| Source table | Target rows | Source events | Expected identity |
|---|---:|---:|---|
| `dnevnikpromena` | 16,452 | 16,452 | one journal row per source event |
| `nivelacije` | 10,976 | 309 | one source document expands to article rows |
| `unosrobe` | 19,761 | 6,623 | one receipt/document expands to article rows |
| `povratnice` | 2,516 | 1,198 | one return document expands to article rows |
| `prenosrobe` | 17,924 | 1,186 | every source row expands to outgoing + incoming |

For `prenosrobe`, the database contains 8,962 outgoing and 8,962 incoming rows, with 1,186 source document IDs and no incomplete pair. This is correct expansion, not duplication. The `SourceRowId` repeats across products because it identifies the source event/document, not the line.

The current code at `Api/Services/AccessImportService.cs:6105-6143` uses a composite multiset key for transfers and skips a whole pair when an outgoing composite is already present. It omits source lineage and store endpoints. This is unsafe for two legitimate transfers with equal type/article/time/amount. The current batch has no collision for this weak key, but absence of a collision in one batch is not a proof of correctness; the code path is demonstrably under-keyed.

The minimal queued repair is:

1. Resolve existing transfer rows by `(SourceTableKey, SourceRowId, event direction, article/line identity)` first.
2. Preserve both directions and all source-line occurrences.
3. Use the existing composite multiset only for legacy rows with no source identity, and include store endpoints/document number where available.
4. Add a real PostgreSQL integration fixture with two equal-valued transfers from different source IDs plus a repeated product line; assert two outgoing/two incoming pairs after two imports.

No unique index on document ID is appropriate.

## Exact 244/20-row outcome and missing information

| Population | Current conclusion | Safe action now | Missing proof |
|---|---|---|---|
| 244 `tblProdaja` lines | correctly fail-closed according to batch warning; no target rows written | none | original `tblProdaja` rows, corresponding `DnevnikPromena` rows, and date validity for each source ID |
| 20 receipt IDs containing those lines | cannot classify as bad, repairable, or date-missing without source | none; do not synthesize date | exact MDB and its original journal/document rows |
| 168 skipped journal rows | target absence is recorded; reason is not persisted per source row | none | exact MDB row values and import log/source-row diagnostics |
| 16 repeated receipt-number groups | legitimate distinct events, not duplicates | keep all | none for target-side classification; original MDB still needed for byte-level source confirmation |
| 4,654 repeated journal source-ID groups | legitimate document/event expansion | keep all | none for target-side identity conclusion |

## Validation

Executed read-only checks:

- PostgreSQL batch summary/coverage and source lineage queries.
- Per-type, per-store and per-year source/target event reconciliation.
- Full 16-group receipt identity and line-signature query.
- Transfer pair completeness query.
- Operational/analytical line identity coverage query.
- Candidate MDB read-only inspection through the Windows Access ODBC driver; no source file write.
- Focused existing tests: 51/51 passed with `--no-build`.

The first test invocation attempted a build but was blocked by an already-running .NET host locking build outputs; it was not classified as a product failure. The no-build focused test run passed.

## Not done / blocker

- No Access import was started.
- No PostgreSQL or MDB data was changed.
- No repair migration or deletion plan was executed.
- No real-MDB regression test was added because the exact batch-23 MDB is unavailable.
- A final row-by-row outcome for 244 lines and 20 receipts is not claimed. Restore/provide the exact MDB (or a non-destructive archive of it) before closing that acceptance item.

SQL evidence: `tools/access-import-23-mdb-event-reconciliation.sql`.

## RQ606 repository-local verification — 2026-10-10

The safe repository-local slice was implemented without reopening the historical MDB claim. `ImportPrenosRobeAsync` now uses normalized source lineage plus source event/article/date/amount/absolute-quantity and direction as its primary idempotency identity. Because `SourceRowId` can identify a source document/event rather than a unique line, equal-valued rows from one source ID are counted as an occurrence multiset. Rows without usable source identity use the bounded legacy key with direction, article/date/amount/absolute quantity, endpoint and document number.

The isolated PostgreSQL fixture used a synthetic MDB reader containing two different transfers with equal article/date/amount values but different source IDs/endpoints, a valid negative source ID, plus two repeated article rows in one source document. The four-test certification passed with `4/4 executed`, `0 failed`, `0 skipped`: the first run preserved both directions and repeated-line multiset, the negative-ID case repaired only a missing pair side, the second import persisted zero additional rows, and cancellation after a real PostgreSQL flush rolled back all rows before a retry restored exactly one complete set. This proves the repository-local event identity/idempotency and rollback behavior and does not certify the unavailable batch-23 source rows.

The exact 244 skipped rows, 20 receipts and 168 skipped journal rows remain unresolved and are intentionally still marked as a historical residual. No source MDB, local business data or production database was modified, and no import was started.

