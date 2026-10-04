-- RQ568 additive adversarial fixture. Dates are relative to PostgreSQL CURRENT_DATE
-- so the immature event remains immature when the certification runs.
-- Zero on-hand stock keeps these evidence-only rows out of the unrelated Pre-Nivelacija queue.
INSERT INTO "Artikli"
  ("PLU", "Naziv", "NabavnaCena", "NabavnaCenaDin", "PrvaProdajnaCena", "ProdajnaCena",
   "IDDobavljac", "IDTipObuce", "UpdatedAt", "Kolicina", "MinimalnaKolicina", "IDObjekat",
   "IDSezona", "Kategorija", "Pol", "Velicina", "Boja", "DataOrigin")
VALUES
  ('RQ568-MARKDOWN', 'RQ568 mature markdown', 50, 50, 11, 10, 1, 1, CURRENT_DATE, 0, 0, 1, 1, 'Obuca', 'Unisex', '42', 'Crna', 'existing'),
  ('RQ568-MARKUP', 'RQ568 mature markup', 50, 50, 80, 100, 1, 1, CURRENT_DATE, 0, 0, 1, 1, 'Obuca', 'Unisex', '42', 'Plava', 'existing'),
  ('RQ568-OVERLAP', 'RQ568 overlapping price events', 50, 50, 90, 90, 1, 1, CURRENT_DATE, 0, 0, 1, 1, 'Obuca', 'Unisex', '41', 'Siva', 'existing'),
  ('RQ568-IMMATURE', 'RQ568 immature post window', 50, 50, 120, 100, 1, 1, CURRENT_DATE, 0, 0, 1, 1, 'Obuca', 'Unisex', '40', 'Crvena', 'existing'),
  ('RQ568-STORE2-IMPORTED', 'RQ568 imported store 2', 50, 50, 100, 90, 1, 1, CURRENT_DATE, 0, 0, 2, 1, 'Obuca', 'Unisex', '39', 'Braon', 'access');

INSERT INTO "DnevnikPromena"
  ("Id", "TipPromene", "Datum", "Iznos", "DobavljacId", "ArtikalId", "StaraProdajnaCena", "NovaProdajnaCena", "Kolicina", "IDObjekat", "DataOrigin")
SELECT fixture.event_id, 'Nivelacija', fixture.event_day::timestamp, fixture.new_price - fixture.old_price, 1, article."Id",
       fixture.old_price, fixture.new_price, 1, fixture.store_id, fixture.data_origin
FROM (
  VALUES
    (56801::bigint, 'RQ568-MARKDOWN'::text, CURRENT_DATE - 300, 11::numeric, 10::numeric, 1, 'existing'::text),
    (56802::bigint, 'RQ568-MARKUP'::text, CURRENT_DATE - 300, 80::numeric, 100::numeric, 1, 'existing'::text),
    (56803::bigint, 'RQ568-OVERLAP'::text, CURRENT_DATE - 320, 100::numeric, 80::numeric, 1, 'existing'::text),
    (56804::bigint, 'RQ568-OVERLAP'::text, CURRENT_DATE - 305, 80::numeric, 90::numeric, 1, 'existing'::text),
    (56805::bigint, 'RQ568-IMMATURE'::text, CURRENT_DATE - 10, 120::numeric, 100::numeric, 1, 'existing'::text),
    (56806::bigint, 'RQ568-STORE2-IMPORTED'::text, CURRENT_DATE - 300, 100::numeric, 90::numeric, 2, 'access'::text)
) AS fixture(event_id, plu, event_day, old_price, new_price, store_id, data_origin)
JOIN "Artikli" article ON article."PLU" = fixture.plu;

WITH base AS (
  SELECT CURRENT_DATE - 300 AS anchor_day
), fixture_sales AS (
  SELECT 'RQ568-MD-PRE'::text AS receipt_prefix, 'RQ568-MARKDOWN'::text AS plu,
         (base.anchor_day - 15 + day_offset)::date AS sale_day, 10::integer AS qty, 10::numeric AS price,
         1::integer AS store_id, 'existing'::text AS data_origin
    FROM base CROSS JOIN generate_series(0, 14) AS day_offset
  UNION ALL
  SELECT 'RQ568-MD-POST', 'RQ568-MARKDOWN', (base.anchor_day + day_offset)::date, 10, 10, 1, 'existing'
    FROM base CROSS JOIN generate_series(0, 14) AS day_offset
  UNION ALL
  SELECT 'RQ568-MU-PRE', 'RQ568-MARKUP', (base.anchor_day - 18 + day_offset)::date, 1, 80, 1, 'existing'
    FROM base CROSS JOIN generate_series(0, 17) AS day_offset
  UNION ALL
  SELECT 'RQ568-MU-POST', 'RQ568-MARKUP', (base.anchor_day + day_offset)::date, 1, 100, 1, 'existing'
    FROM base CROSS JOIN generate_series(0, 29) AS day_offset
  UNION ALL
  SELECT 'RQ568-OVERLAP-PRE', 'RQ568-OVERLAP', (base.anchor_day - 27 + day_offset)::date, 1, 100, 1, 'existing'
    FROM base CROSS JOIN generate_series(0, 6) AS day_offset
  UNION ALL
  SELECT 'RQ568-OVERLAP-POST', 'RQ568-OVERLAP', (base.anchor_day - 20 + day_offset)::date, 1, 80, 1, 'existing'
    FROM base CROSS JOIN generate_series(0, 8) AS day_offset
  UNION ALL
  SELECT 'RQ568-IMMATURE-PRE', 'RQ568-IMMATURE', (CURRENT_DATE - 22 + day_offset)::date, 1, 120, 1, 'existing'
    FROM generate_series(0, 11) AS day_offset
  UNION ALL
  SELECT 'RQ568-IMMATURE-POST', 'RQ568-IMMATURE', (CURRENT_DATE - 9 + day_offset)::date, 1, 100, 1, 'existing'
    FROM generate_series(0, 4) AS day_offset
  UNION ALL
  SELECT 'RQ568-STORE2-PRE', 'RQ568-STORE2-IMPORTED', (base.anchor_day - 10 + day_offset)::date, 1, 100, 2, 'access'
    FROM base CROSS JOIN generate_series(0, 9) AS day_offset
  UNION ALL
  SELECT 'RQ568-STORE2-POST', 'RQ568-STORE2-IMPORTED', (base.anchor_day + day_offset)::date, 1, 90, 2, 'access'
    FROM base CROSS JOIN generate_series(0, 9) AS day_offset
), inserted_headers AS (
  INSERT INTO prodaja_zaglavlje
    (broj_racuna, datum_prodaje, id_objekat, korisnik_ime, data_origin, source_timestamp_basis)
  SELECT fixture_sales.receipt_prefix || '-' || to_char(fixture_sales.sale_day, 'YYYYMMDD'),
         fixture_sales.sale_day::timestamp + TIME '12:00', fixture_sales.store_id, 'rq568',
         fixture_sales.data_origin, 'utc_instant'
    FROM fixture_sales
  RETURNING id, broj_racuna
)
INSERT INTO prodaja_stavke
  (id_prodaja, id_artikal, kolicina, cena, nabavna_cena, supplier_id_at_sale, shoe_type_id_at_sale, attribution_basis)
SELECT headers.id, article."Id",
       CASE WHEN headers.broj_racuna LIKE 'RQ568-MD-%' THEN 10 ELSE 1 END,
       CASE
         WHEN headers.broj_racuna LIKE 'RQ568-MD-%' THEN 10
         WHEN headers.broj_racuna LIKE 'RQ568-MU-PRE-%' THEN 80
         WHEN headers.broj_racuna LIKE 'RQ568-MU-POST-%' THEN 100
         WHEN headers.broj_racuna LIKE 'RQ568-OVERLAP-PRE-%' THEN 100
         WHEN headers.broj_racuna LIKE 'RQ568-OVERLAP-POST-%' THEN 80
         WHEN headers.broj_racuna LIKE 'RQ568-IMMATURE-PRE-%' THEN 120
         WHEN headers.broj_racuna LIKE 'RQ568-IMMATURE-POST-%' THEN 100
         WHEN headers.broj_racuna LIKE 'RQ568-STORE2-PRE-%' THEN 100
         ELSE 90
       END,
       50, 1, 1, 'sale_snapshot'
FROM inserted_headers headers
JOIN "Artikli" article ON article."PLU" = CASE
  WHEN headers.broj_racuna LIKE 'RQ568-MD-%' THEN 'RQ568-MARKDOWN'
  WHEN headers.broj_racuna LIKE 'RQ568-MU-%' THEN 'RQ568-MARKUP'
  WHEN headers.broj_racuna LIKE 'RQ568-OVERLAP-%' THEN 'RQ568-OVERLAP'
  WHEN headers.broj_racuna LIKE 'RQ568-IMMATURE-%' THEN 'RQ568-IMMATURE'
  ELSE 'RQ568-STORE2-IMPORTED'
END;

SELECT setval(pg_get_serial_sequence('"DnevnikPromena"', 'Id'),
              COALESCE((SELECT MAX("Id") FROM "DnevnikPromena"), 1), true);