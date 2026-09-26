-- RQ407 shared Operations fixture.
-- One deterministic source drives the eight route families.  Test-only database.

TRUNCATE TABLE
  prodaja_stavke,
  prodaja_zaglavlje,
  "DnevnikPromena",
  "Artikli",
  "Dobavljaci",
  "TipoviObuce",
  "Sezone"
  RESTART IDENTITY CASCADE;

INSERT INTO "Dobavljaci" ("Naziv", "DataOrigin") VALUES
  ('Dobavljac A', 'existing'),
  ('Dobavljac C', 'access'),
  ('Dobavljac D', 'existing'),
  ('Dobavljac E', 'existing'),
  ('Dobavljac F', 'existing'),
  ('Dobavljac G', 'existing'),
  ('Dobavljac H', 'existing');

INSERT INTO "TipoviObuce" ("Naziv", "DataOrigin") VALUES
  ('Patike', 'existing'),
  ('Cipele', 'access'),
  ('Nepoznato', 'existing'),
  ('', 'existing'),
  ('Cizme', 'existing');

INSERT INTO "Sezone" ("Naziv", "DatumOd", "DatumDo", "DataOrigin") VALUES
  ('RQ407 Operations July 2026', '2026-07-01T00:00:00Z', '2026-07-31T23:59:59Z', 'existing');

INSERT INTO "Artikli"
  ("PLU", "Naziv", "NabavnaCena", "NabavnaCenaDin", "PrvaProdajnaCena", "ProdajnaCena",
   "IDDobavljac", "IDTipObuce", "UpdatedAt", "Kolicina", "MinimalnaKolicina", "IDObjekat",
   "IDSezona", "Kategorija", "Pol", "Velicina", "Boja", "DataOrigin")
VALUES
  ('OPS-101', 'RQ407 existing patika', 50, 50, 100, 100, 1, 1, '2026-07-01T00:00:00Z', 0, 5, 1, 1, 'Obuca', 'Unisex', '42', 'Crna', 'existing'),
  ('OPS-102', 'RQ407 imported cipela', NULL, NULL, 130, 130, 2, 2, '2026-07-02T00:00:00Z', 0, 1, 2, 1, 'Obuca', 'Unisex', '42', 'Plava', 'access'),
  ('OPS-103', 'RQ407 unknown dimension', NULL, NULL, 80, 80, NULL, NULL, '2026-07-03T00:00:00Z', 0, 1, 1, 1, NULL, NULL, NULL, NULL, 'existing'),
  ('OOS-101', 'RQ407 out of stock', 50, 50, 100, 100, 1, 1, '2026-07-01T00:00:00Z', 0, 5, 1, 1, 'Obuca', 'Unisex', '41', 'Crna', 'existing'),
  ('EMPTY-104', 'RQ407 insufficient evidence', NULL, NULL, 70, 70, 1, 1, '2026-07-01T00:00:00Z', 0, 2, 2, 1, 'Obuca', 'Unisex', '40', 'Siva', 'existing'),
  ('PRE-105', 'RQ407 pre-nivelacija candidate', 80, 80, 150, 150, 1, 1, '2026-07-01T00:00:00Z', 10, 2, 1, 1, 'Obuca', 'Unisex', '39', 'Crvena', 'existing'),
  ('NIV-101', 'RQ407 comparable nivelacija', 50, 50, 100, 110, 1, 1, '2026-07-01T00:00:00Z', 0, 0, 1, 1, 'Obuca', 'Unisex', '38', 'Crna', 'existing'),
  -- RQ446 adversarial rows.  Sale-line attribution is intentionally not
  -- identical to the current master for ADV-MUTATION.
  ('ADV-MUTATION', 'RQ446 sale-time attribution mutation', 60, 60, 100, 100, 2, 2, '2026-08-20T00:00:00Z', 0, 0, 1, 1, 'Obuca', 'Unisex', '42', 'Crna', 'existing'),
  ('ADV-KNOWN-NEPOZNATO', 'RQ446 known type with Nepoznato label', NULL, NULL, 150, 150, 2, 3, '2026-08-20T00:00:00Z', 0, 0, 2, 1, 'Obuca', 'Unisex', '42', 'Plava', 'access'),
  ('ADV-BLANK-TYPE', 'RQ446 known type with blank label', 45, 45, 90, 90, 3, 4, '2026-08-20T00:00:00Z', 0, 0, 2, 1, 'Obuca', 'Unisex', '41', 'Siva', 'access'),
  ('ADV-PREVIOUS-ONLY', 'RQ446 previous-period-only type', 50, 50, 90, 90, 3, 5, '2026-08-20T00:00:00Z', 0, 0, 1, 1, 'Obuca', 'Unisex', '40', 'Crvena', 'existing'),
  ('ADV-UNKNOWN', 'RQ446 null supplier and type', 100, 100, 200, 200, NULL, NULL, '2026-08-20T00:00:00Z', 0, 0, 1, 1, 'Obuca', 'Unisex', '39', 'Crna', 'existing'),
  ('ADV-SUPPLIER-E', 'RQ446 top N supplier E', 40, 40, 80, 80, 4, 1, '2026-08-20T00:00:00Z', 0, 0, 1, 1, 'Obuca', 'Unisex', '38', 'Crna', 'existing'),
  ('ADV-SUPPLIER-F', 'RQ446 top N supplier F', 50, 50, 70, 70, 5, 1, '2026-08-20T00:00:00Z', 0, 0, 1, 1, 'Obuca', 'Unisex', '37', 'Crna', 'existing'),
  ('ADV-SUPPLIER-G', 'RQ446 top N supplier G', 30, 30, 60, 60, 6, 1, '2026-08-20T00:00:00Z', 0, 0, 1, 1, 'Obuca', 'Unisex', '36', 'Crna', 'existing'),
  ('ADV-SUPPLIER-H', 'RQ446 top N supplier H', 20, 20, 40, 40, 7, 1, '2026-08-20T00:00:00Z', 0, 0, 1, 1, 'Obuca', 'Unisex', '35', 'Crna', 'existing'),
  ('ADV-ZERO-MARGIN', 'RQ446 zero margin slice', 100, 100, 100, 100, 1, 1, '2026-08-20T00:00:00Z', 0, 0, 1, 1, 'Obuca', 'Unisex', '34', 'Crna', 'existing'),
  ('ADV-NEGATIVE-MARGIN', 'RQ446 negative margin slice', 100, 100, 80, 80, 2, 2, '2026-08-20T00:00:00Z', 0, 0, 1, 1, 'Obuca', 'Unisex', '33', 'Crna', 'existing');

INSERT INTO prodaja_zaglavlje
  (id, broj_racuna, datum_prodaje, id_objekat, korisnik_ime, data_origin)
VALUES
  (1, 'RQ407-101', '2026-07-01T09:15:00Z', 1, 'rq407', 'existing'),
  (2, 'RQ407-101R', '2026-07-01T16:30:00Z', 1, 'rq407', 'existing'),
  (3, 'RQ407-102', '2026-07-02T10:00:00Z', 2, 'rq407', 'access'),
  (4, 'RQ407-103', '2026-07-03T22:30:00Z', 1, 'rq407', 'existing'),
  (5, 'RQ407-NIV-PRE', '2026-07-20T10:00:00Z', 1, 'rq407', 'existing'),
  (6, 'RQ407-NIV-POST', '2026-08-05T10:00:00Z', 1, 'rq407', 'existing');

INSERT INTO prodaja_stavke
  (id, id_prodaja, id_artikal, kolicina, cena, nabavna_cena, supplier_id_at_sale, shoe_type_id_at_sale, attribution_basis)
VALUES
  (1, 1, 1, 3, 100, 50, 1, 1, 'sale_snapshot'),
  (2, 2, 1, -1, 100, 50, 1, 1, 'sale_snapshot'),
  (3, 3, 2, 2, 130, NULL, 2, 2, 'sale_snapshot'),
  (4, 4, 3, 1, 80, NULL, NULL, NULL, 'sale_snapshot'),
  (5, 5, 7, 2, 100, 50, 1, 1, 'sale_snapshot'),
  (6, 6, 7, 3, 90, 50, 1, 1, 'sale_snapshot');

-- RQ446 adversarial sales are outside the RQ407 July route window so the
-- original eight-route arithmetic remains unchanged.  The exact timestamps
-- exercise lower/upper boundaries, adjacent-day non-overlap, two stores,
-- Access/POS origins, signed returns, unknown identity and cost coverage.
INSERT INTO prodaja_zaglavlje
  (id, broj_racuna, datum_prodaje, id_objekat, korisnik_ime, data_origin)
VALUES
  (7, 'RQ446-LOWER', '2026-09-01T00:00:00Z', 1, 'pos', 'existing'),
  (8, '  dUg  ', '2026-09-01T09:00:00Z', 1, 'pos', 'existing'),
  (9, 'RQ446-RETURN', '2026-09-01T12:00:00Z', 1, 'pos', 'existing'),
  (10, 'RQ446-UPPER-FRACTION', '2026-09-01T23:59:59.999999Z', 2, 'access', 'access'),
  (11, 'RQ446-NEXT-DAY', '2026-09-02T00:00:00Z', 1, 'pos', 'existing'),
  (12, ' KoReKcIjA ', '2026-09-02T10:00:00Z', 2, 'access', 'access'),
  (13, 'RQ446-PREVIOUS-ONLY', '2026-08-31T23:59:59.999999Z', 1, 'pos', 'existing'),
  (14, 'RQ446-SUPPLIER-D', '2026-09-01T13:00:00Z', 2, 'access', 'access'),
  (15, 'RQ446-SUPPLIER-E', '2026-09-01T14:00:00Z', 1, 'pos', 'existing'),
  (16, 'RQ446-SUPPLIER-F', '2026-09-01T15:00:00Z', 1, 'pos', 'existing'),
  (17, 'RQ446-SUPPLIER-G', '2026-09-01T16:00:00Z', 1, 'pos', 'existing'),
  (18, 'RQ446-SUPPLIER-H', '2026-09-01T17:00:00Z', 1, 'pos', 'existing'),
  (19, 'RQ446-UNKNOWN', '2026-09-01T18:00:00Z', 1, 'pos', 'existing'),
  (20, 'RQ446-ZERO-MARGIN', '2026-09-04T10:00:00Z', 1, 'pos', 'existing'),
  (21, 'RQ446-NEGATIVE-MARGIN', '2026-09-04T11:00:00Z', 1, 'pos', 'existing');

INSERT INTO prodaja_stavke
  (id, id_prodaja, id_artikal, kolicina, cena, nabavna_cena, supplier_id_at_sale, shoe_type_id_at_sale, attribution_basis)
VALUES
  (7, 7, 8, 2, 100, 60, 1, 1, 'sale_snapshot'),
  -- Excluded certified retail population: mixed case and surrounding spaces.
  (8, 8, 8, 4, 100, 60, 1, 1, 'sale_snapshot'),
  (9, 9, 8, -3, 100, 60, 1, 1, 'sale_snapshot'),
  (10, 10, 9, 1, 150, NULL, 2, 3, 'sale_snapshot'),
  -- Exact next-day boundary is outside the 2026-09-01 half-open window.
  (11, 11, 10, 1, 120, 120, NULL, NULL, 'sale_snapshot'),
  (12, 12, 9, -1, 150, NULL, 2, 3, 'sale_snapshot'),
  (13, 13, 11, 2, 90, 50, 3, 5, 'sale_snapshot'),
  (14, 14, 10, 1, 90, 45, 3, 4, 'sale_snapshot'),
  (15, 15, 13, 1, 80, 40, 4, 1, 'sale_snapshot'),
  (16, 16, 14, 1, 70, 50, 5, 1, 'sale_snapshot'),
  (17, 17, 15, 1, 60, 30, 6, 1, 'sale_snapshot'),
  (18, 18, 16, 1, 40, 20, 7, 1, 'sale_snapshot'),
  (19, 19, 12, 1, 200, 100, NULL, NULL, 'sale_snapshot'),
  (20, 20, 17, 1, 100, 100, 1, 1, 'sale_snapshot'),
  (21, 21, 18, 1, 80, 100, 2, 2, 'sale_snapshot');

INSERT INTO "DnevnikPromena"
  ("Id", "TipPromene", "Datum", "Iznos", "DobavljacId", "ArtikalId", "StaraProdajnaCena", "NovaProdajnaCena", "Kolicina", "IDObjekat", "DataOrigin")
VALUES
  (1, 'Nivelacija cena', '2026-08-01T00:00:00Z', 110, 1, 7, 100, 110, 1, 1, 'existing');

SELECT setval(pg_get_serial_sequence('"Dobavljaci"', 'Id'), COALESCE((SELECT MAX("Id") FROM "Dobavljaci"), 1), true);
SELECT setval(pg_get_serial_sequence('"TipoviObuce"', 'Id'), COALESCE((SELECT MAX("Id") FROM "TipoviObuce"), 1), true);
SELECT setval(pg_get_serial_sequence('"Sezone"', 'Id'), COALESCE((SELECT MAX("Id") FROM "Sezone"), 1), true);
SELECT setval(pg_get_serial_sequence('"Artikli"', 'Id'), COALESCE((SELECT MAX("Id") FROM "Artikli"), 1), true);
SELECT setval(pg_get_serial_sequence('prodaja_zaglavlje', 'id'), 21, true);
SELECT setval(pg_get_serial_sequence('prodaja_stavke', 'id'), 21, true);
SELECT setval(pg_get_serial_sequence('"DnevnikPromena"', 'Id'), 1, true);
