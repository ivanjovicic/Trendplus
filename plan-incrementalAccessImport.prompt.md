# Incremental Access Import - Detailed Implementation Guide

## 1) Scope and constraints
- Keep existing full import flow as fallback.
- Add incremental mode per table key used in `AccessImportService`.
- Use cursor-based reads from Access and idempotent upserts in PostgreSQL.
- Never advance cursor before successful batch merge commit.
- Keep parent-before-child consistency for sales and returns.

## 2) Real table keys in current pipeline
- `tipovi_obuce`
- `dobavljaci`
- `sezone`
- `objekti`
- `artikli`
- `dnevnik_promena`
- `prodaja_zaglavlje`
- `prodaja_stavke`
- `povracaj_zaglavlje`
- `povracaj_stavke`

## 3) Canonical incremental profile per table

### 3.1 `tipovi_obuce`
- Cursor mode: `id`
- Cursor column: `Id`
- Tie-breaker: none
- Upsert key: `Id`
- Merge rule: always update mutable columns on conflict
- Notes: small dimension table, low risk

### 3.2 `dobavljaci`
- Cursor mode: `id`
- Cursor column: `Id`
- Tie-breaker: none
- Upsert key: `Id`
- Merge rule: update all business columns on conflict
- Notes: if source can modify old rows without id change, schedule periodic reconciliation

### 3.3 `sezone`
- Cursor mode: `id`
- Cursor column: `Id`
- Tie-breaker: none
- Upsert key: `Id`
- Merge rule: update `Naziv`, `DatumOd`, `DatumDo`
- Notes: dimension table, id cursor is sufficient

### 3.4 `objekti`
- Cursor mode: `none` (for now)
- Reason: currently not persisted to a Trendplus table in import path, only tracked for analytics/store map
- Action: process full source snapshot in-memory and refresh store cache
- Optional future: introduce persisted table and switch to id cursor

### 3.5 `artikli`
- Cursor mode: `timestamp_then_id`
- Preferred timestamp aliases: `updatedat`, `lastmodified`, `datumizmene`, `datumpromene`, `modifiedat`
- Fallback cursor: `Id`
- Tie-breaker for timestamp mode: `(SourceUpdatedAt, Id)`
- Upsert key: `Id`
- Merge guard: do not overwrite newer target with older source
- Notes: most important table for incremental gains

### 3.6 `dnevnik_promena`
- Cursor mode: `id`
- Cursor column aliases: `id`, `iddnevnik`, `idlog`, `seqno`
- Tie-breaker: none
- Upsert key: `Id`
- Merge rule: update all mutable movement fields on conflict
- Notes: strong append pattern, id cursor is best fit

### 3.7 `prodaja_zaglavlje`
- Cursor mode: `id` when real header table exists
- Cursor column aliases: `id`, `idprodaja`, `saleid`, `iddnevnik`
- Tie-breaker: none
- Upsert key: `Id`
- Special case: if synthesized from `dnevnik_promena` or built from line table, drive header refresh from parent source cursor, not a separate cursor

### 3.8 `prodaja_stavke`
- Cursor mode: `id_or_composite`
- Preferred cursor: `Id` if source provides reliable line id
- Fallback: parent-driven incremental from `prodaja_zaglavlje` delta and composite merge key
- Composite upsert key candidate: `(IdProdaja, IdArtikal, Kolicina, Cena)` plus optional source line ordinal if available
- Critical dependency: parent headers must be present before line merge

### 3.9 `povracaj_zaglavlje`
- Cursor mode: `id`
- Cursor column aliases: `id`, `idpovracaj`, `returnid`
- Tie-breaker: none
- Upsert key: `Id`
- Merge rule: update status and business fields

### 3.10 `povracaj_stavke`
- Cursor mode: `id_or_composite`
- Preferred cursor: `Id` if present and stable
- Fallback composite key: `(IdPovracaj, IdArtikal, Kolicina, Cena)`
- Dependency: `povracaj_zaglavlje` must be merged first

## 4) Database schema additions

### 4.1 New cursor table (typed, not text-only)
```sql
CREATE TABLE IF NOT EXISTS "AccessImportCursors" (
  "TableKey" text PRIMARY KEY,
  "CursorMode" text NOT NULL CHECK ("CursorMode" IN ('timestamp','id','none','timestamp_then_id','id_or_composite')),
  "CursorTimestampUtc" timestamp with time zone NULL,
  "CursorId" bigint NULL,
  "CursorTieBreakerId" bigint NULL,
  "OverlapSeconds" integer NOT NULL DEFAULT 60,
  "LastSuccessfulBatchId" bigint NULL,
  "LastRunStartedAtUtc" timestamp with time zone NULL,
  "LastRunCompletedAtUtc" timestamp with time zone NULL,
  "LastError" text NULL,
  "UpdatedAtUtc" timestamp with time zone NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS "IX_AccessImportCursors_LastSuccessfulBatchId"
ON "AccessImportCursors" ("LastSuccessfulBatchId");
```

### 4.2 DataImportBatches columns for incremental telemetry
- `IsIncremental bool`
- `CursorSnapshot jsonb`
- `ProcessedRowCount int`
- `SkippedRowCount int`
- `RowsInserted int`
- `RowsUpdated int`
- `RowsUnchanged int`

### 4.3 Optional source metadata columns on target tables
- `SourceUpdatedAtUtc timestamptz null`
- `SourceCursorId bigint null`
- Needed where timestamp-mode merge guard is required

## 5) Appsettings template for per-table profiles
```json
{
  "AccessImport": {
    "Incremental": {
      "Enabled": true,
      "Mode": "shadow",
      "DefaultBatchSize": 2000,
      "DefaultOverlapSeconds": 60,
      "Profiles": [
        { "TableKey": "tipovi_obuce", "CursorMode": "id", "CursorIdAliases": ["id"] },
        { "TableKey": "dobavljaci", "CursorMode": "id", "CursorIdAliases": ["id"] },
        { "TableKey": "sezone", "CursorMode": "id", "CursorIdAliases": ["id","idsezona"] },
        { "TableKey": "objekti", "CursorMode": "none" },
        {
          "TableKey": "artikli",
          "CursorMode": "timestamp_then_id",
          "CursorTimestampAliases": ["updatedat","lastmodified","datumizmene","datumpromene","modifiedat"],
          "CursorIdAliases": ["id","idartikal","productid"],
          "OverlapSeconds": 60
        },
        { "TableKey": "dnevnik_promena", "CursorMode": "id", "CursorIdAliases": ["id","iddnevnik","idlog","seqno"] },
        { "TableKey": "prodaja_zaglavlje", "CursorMode": "id", "CursorIdAliases": ["id","idprodaja","saleid","iddnevnik"] },
        { "TableKey": "prodaja_stavke", "CursorMode": "id_or_composite", "CursorIdAliases": ["id","idstavka","lineid"] },
        { "TableKey": "povracaj_zaglavlje", "CursorMode": "id", "CursorIdAliases": ["id","idpovracaj","returnid"] },
        { "TableKey": "povracaj_stavke", "CursorMode": "id_or_composite", "CursorIdAliases": ["id"] }
      ]
    }
  }
}
```

## 6) Access read filter templates

### 6.1 Timestamp + tie-breaker
```sql
WHERE ([UpdatedAt] > ?)
   OR ([UpdatedAt] = ? AND [Id] > ?)
ORDER BY [UpdatedAt], [Id]
```

### 6.2 Id cursor
```sql
WHERE [Id] > ?
ORDER BY [Id]
```

### 6.3 Overlap for timestamp mode
- Effective read cursor is `saved_cursor_timestamp - overlap_seconds`.
- Keep dedupe via merge key and merge guard.

## 7) PostgreSQL stage + merge templates

### 7.1 Stage schema pattern
- One persistent stage table per large source table.
- Always include `batch_id`.

### 7.2 Merge (id-key tables)
```sql
INSERT INTO target_table (...)
SELECT ...
FROM stg_table s
WHERE s.batch_id = @batch_id
ON CONFLICT (id)
DO UPDATE SET
  col_a = EXCLUDED.col_a,
  col_b = EXCLUDED.col_b,
  source_updated_at_utc = EXCLUDED.source_updated_at_utc
WHERE
  target.source_updated_at_utc IS NULL
  OR EXCLUDED.source_updated_at_utc IS NULL
  OR EXCLUDED.source_updated_at_utc > target.source_updated_at_utc;
```

### 7.3 Merge (composite-key fallback)
```sql
ON CONFLICT (id_prodaja, id_artikal, kolicina, cena)
DO UPDATE SET ...
```

## 8) Execution order and dependency rules
- Order remains:
  - `tipovi_obuce`
  - `dobavljaci`
  - `sezone`
  - `objekti`
  - `artikli`
  - `dnevnik_promena`
  - `prodaja_zaglavlje`
  - `prodaja_stavke`
  - `povracaj_zaglavlje`
  - `povracaj_stavke`
- Parent-child enforcement:
  - `prodaja_zaglavlje` before `prodaja_stavke`
  - `povracaj_zaglavlje` before `povracaj_stavke`
- If parent not available:
  - honor existing `SkipInvalidForeignKeys` and `AutoInsertMissingParents` logic

## 9) Reliable cursor commit algorithm
- Read cursor C before processing table.
- Process in batches with `COPY -> MERGE -> commit`.
- Track `max_seen_timestamp`, `max_seen_id`, `max_seen_tie_id` from successfully merged rows only.
- Update cursor only after final successful table batch.
- On failure:
  - keep old cursor
  - mark batch failed with detailed error

## 10) Rollout checklist
- Deploy migration first.
- Deploy code with `Mode=shadow`.
- Compare shadow stats vs full import for at least 24h.
- Enable `incremental` table-by-table:
  - `artikli`, `dnevnik_promena` first
  - then `prodaja_zaglavlje`, `prodaja_stavke`
  - then returns tables
- Keep manual full import endpoint enabled for rollback.

## 11) Must-have tests
- Cursor parser and SQL filter builder tests.
- Tie-breaker correctness test for equal timestamps.
- Idempotent rerun test for same batch.
- Parent-child dependency test for sales and returns.
- Failure-before-cursor-commit test.
- Parallel worker lease test (same table cannot run twice concurrently).
