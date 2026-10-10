-- Read-only evidence queries for Access import batch #23.
-- Run against the local PostgreSQL database only; this file never opens or mutates the MDB.

-- 1. Batch coverage and recorded source-artifact lineage.
SELECT "Id", "Status", "SourceFileName", "SourceFilePath", "SourceFileHash",
       "RowsRead", "RowsAccepted", "RowsWritten", "SkippedRowCount", "TotalErrors",
       "StartedAtUtc", "CompletedAtUtc"
FROM "DataImportBatches"
WHERE "Id" = 23;

-- 2. Target coverage by source table and source lineage.
SELECT "SourceTableKey", count(*) AS target_rows,
       count(DISTINCT "SourceRowId") AS source_rows,
       min("Datum") AS first_event, max("Datum") AS last_event
FROM "DnevnikPromena"
WHERE "SourceBatchId" = 23
GROUP BY "SourceTableKey"
ORDER BY "SourceTableKey";

-- 3. Event counts by type, source table and calendar year.
SELECT "SourceTableKey", "TipPromene", date_trunc('year', "Datum")::date AS year,
       count(*) AS target_rows,
       count(DISTINCT "SourceRowId") AS source_events,
       count(DISTINCT "IDObjekat") AS stores,
       sum(coalesce("Iznos", 0)) AS amount,
       sum(coalesce("Kolicina", 0)) AS quantity
FROM "DnevnikPromena"
WHERE "SourceBatchId" = 23
GROUP BY 1, 2, 3
ORDER BY 1, 2, 3;

-- 4. Event counts by type and store (including NULL store for source tables
--    whose row has no store columns).
SELECT "SourceTableKey", "TipPromene",
       coalesce("IDObjekat"::text, 'NULL') AS store,
       count(*) AS target_rows,
       count(DISTINCT "SourceRowId") AS source_events,
       min("Datum")::date AS first_day,
       max("Datum")::date AS last_day
FROM "DnevnikPromena"
WHERE "SourceBatchId" = 23
GROUP BY 1, 2, 3
ORDER BY 1, 2, 3;

-- 5. The 16 receipt groups reported by the batch diagnostics. The source ID,
--    line count and line signature show whether equal document numbers are
--    separate source events rather than rows to collapse.
WITH duplicate_groups AS (
    SELECT datum_prodaje::date AS sale_day, broj_racuna, id_objekat
    FROM prodaja_zaglavlje
    WHERE source_batch_id = 23
      AND nullif(trim(broj_racuna), '') IS NOT NULL
    GROUP BY 1, 2, 3
    HAVING count(*) > 1
)
SELECT z.datum_prodaje::date AS sale_day, z.broj_racuna, z.id_objekat,
       z.id, z.source_row_id,
       count(ps.id) AS line_count,
       coalesce(sum(ps.kolicina * ps.cena), 0) AS amount,
       string_agg(format('%s:%s@%s', ps.id_artikal, ps.kolicina, ps.cena),
                  ',' ORDER BY ps.id_artikal, ps.id) AS line_signature
FROM prodaja_zaglavlje z
LEFT JOIN prodaja_stavke ps ON ps.id_prodaja = z.id
JOIN duplicate_groups g
  ON g.sale_day = z.datum_prodaje::date
 AND g.broj_racuna = z.broj_racuna
 AND g.id_objekat IS NOT DISTINCT FROM z.id_objekat
WHERE z.source_batch_id = 23
GROUP BY z.datum_prodaje::date, z.broj_racuna, z.id_objekat,
         z.id, z.source_row_id
ORDER BY 1, 2, 4;

-- 6. Non-standard receipt population and its amount, case-insensitive.
SELECT CASE
         WHEN lower(coalesce(broj_racuna, '')) LIKE '%korekcija%' THEN 'Korekcija'
         WHEN lower(coalesce(broj_racuna, '')) LIKE '%dug%' THEN 'DUG'
         WHEN broj_racuna !~ '^[0-9]+$' THEN 'other-nonstandard'
         ELSE 'standard'
       END AS document_class,
       count(*) AS headers,
       count(DISTINCT source_row_id) AS source_rows,
       sum((SELECT coalesce(sum(ps.kolicina * ps.cena), 0)
            FROM prodaja_stavke ps WHERE ps.id_prodaja = z.id)) AS amount
FROM prodaja_zaglavlje z
WHERE source_batch_id = 23
GROUP BY 1
ORDER BY 1;

-- 7. Transfer expansion and transfer-pair completeness. SourceRowId is a
--    document/event identity and may repeat for several article rows. Compare
--    the outgoing/incoming multisets at source-document + line-signature grain;
--    a zero-row result is required. Equal-valued repeated lines are retained
--    because their multiplicities must match, not collapse to one row.
WITH transfer_line_multiset AS (
    SELECT "SourceTableKey", "SourceRowId", "ArtikalId", "Datum",
           abs(coalesce("Kolicina", 0)) AS absolute_quantity,
           coalesce("Iznos", 0) AS amount,
           count(*) FILTER (WHERE "TipPromene" = 'Prenos izlaz') AS outgoing_rows,
           count(*) FILTER (WHERE "TipPromene" = 'Prenos ulaz') AS incoming_rows,
           sum(CASE WHEN "TipPromene" = 'Prenos izlaz'
                    THEN coalesce("Kolicina", 0) ELSE 0 END) AS outgoing_quantity,
           sum(CASE WHEN "TipPromene" = 'Prenos ulaz'
                    THEN coalesce("Kolicina", 0) ELSE 0 END) AS incoming_quantity,
           sum(CASE WHEN "TipPromene" = 'Prenos izlaz'
                    THEN coalesce("Iznos", 0) ELSE 0 END) AS outgoing_amount,
           sum(CASE WHEN "TipPromene" = 'Prenos ulaz'
                    THEN coalesce("Iznos", 0) ELSE 0 END) AS incoming_amount
    FROM "DnevnikPromena"
    WHERE "SourceBatchId" = 23
      AND lower("SourceTableKey") IN ('prenosrobe', 'prenos_robe')
      AND "SourceRowId" IS NOT NULL
      AND "TipPromene" IN ('Prenos izlaz', 'Prenos ulaz')
    GROUP BY "SourceTableKey", "SourceRowId", "ArtikalId", "Datum",
             abs(coalesce("Kolicina", 0)), coalesce("Iznos", 0)
)
SELECT *
FROM transfer_line_multiset
WHERE outgoing_rows <> incoming_rows
   OR outgoing_rows = 0
   OR incoming_rows = 0
   OR outgoing_quantity + incoming_quantity <> 0
   OR outgoing_amount <> incoming_amount
ORDER BY "SourceTableKey", "SourceRowId", "ArtikalId", "Datum";

-- Transfer rows without source document identity cannot be certified by the
-- source-event contract and must remain visible for manual review.
SELECT "TipPromene", "ArtikalId", "Datum", "Kolicina", "Iznos", "IDObjekat"
FROM "DnevnikPromena"
WHERE "SourceBatchId" = 23
  AND lower("SourceTableKey") IN ('prenosrobe', 'prenos_robe')
  AND "SourceRowId" IS NULL
ORDER BY "Datum", "ArtikalId", "TipPromene";

-- 8. Repeated source IDs are event/document identities with multiple product
--    rows. This must not be treated as duplicate data.
SELECT "SourceTableKey", "SourceRowId", count(*) AS rows,
       count(DISTINCT "TipPromene") AS types,
       count(DISTINCT "ArtikalId") AS products,
       min("Datum") AS first_event, max("Datum") AS last_event,
       string_agg(DISTINCT "TipPromene", ', ' ORDER BY "TipPromene") AS types_seen
FROM "DnevnikPromena"
WHERE "SourceBatchId" = 23
GROUP BY 1, 2
HAVING count(*) > 1
ORDER BY rows DESC, 1, 2;

-- 9. Operational and analytical line identity coverage.
SELECT source_table_key, count(*) AS lines,
       count(DISTINCT source_row_id) AS source_line_ids,
       count(*) FILTER (WHERE source_row_id IS NULL) AS null_source_line_ids
FROM prodaja_stavke
WHERE source_batch_id = 23
GROUP BY source_table_key;

SELECT "DataOrigin", "SourceTableKey", count(*) AS lines,
       count(DISTINCT "SourceLineId") AS source_line_ids,
       count(*) FILTER (WHERE "SourceLineId" IS NULL) AS null_source_line_ids,
       count(DISTINCT "SaleId") AS sales
FROM "SalesLineFacts"
GROUP BY 1, 2
ORDER BY 1, 2;

-- 10. Cross-batch collision candidates for the current transfer dedup key.
--     A non-empty result is a proof that (type, article, timestamp, amount)
--     is too weak to identify a source event.
SELECT "TipPromene", "ArtikalId", "Datum", "Iznos",
       count(*) AS rows,
       count(DISTINCT concat_ws(':', "SourceTableKey", "SourceRowId"::text)) AS source_events,
       string_agg(DISTINCT concat_ws(':', "SourceBatchId"::text,
                                     "SourceTableKey", "SourceRowId"::text), ',') AS sources
FROM "DnevnikPromena"
WHERE "TipPromene" IN ('Prenos izlaz', 'Prenos ulaz')
GROUP BY 1, 2, 3, 4
HAVING count(DISTINCT concat_ws(':', "SourceTableKey", "SourceRowId"::text)) > 1
ORDER BY rows DESC;
