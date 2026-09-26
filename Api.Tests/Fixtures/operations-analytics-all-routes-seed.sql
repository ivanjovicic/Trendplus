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
  ('Dobavljac C', 'access');

INSERT INTO "TipoviObuce" ("Naziv", "DataOrigin") VALUES
  ('Patike', 'existing'),
  ('Cipele', 'access');

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
  ('NIV-101', 'RQ407 comparable nivelacija', 50, 50, 100, 110, 1, 1, '2026-07-01T00:00:00Z', 0, 0, 1, 1, 'Obuca', 'Unisex', '38', 'Crna', 'existing');

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

INSERT INTO "DnevnikPromena"
  ("Id", "TipPromene", "Datum", "Iznos", "DobavljacId", "ArtikalId", "StaraProdajnaCena", "NovaProdajnaCena", "Kolicina", "IDObjekat", "DataOrigin")
VALUES
  (1, 'Nivelacija cena', '2026-08-01T00:00:00Z', 110, 1, 7, 100, 110, 1, 1, 'existing');

SELECT setval(pg_get_serial_sequence('"Dobavljaci"', 'Id'), COALESCE((SELECT MAX("Id") FROM "Dobavljaci"), 1), true);
SELECT setval(pg_get_serial_sequence('"TipoviObuce"', 'Id'), COALESCE((SELECT MAX("Id") FROM "TipoviObuce"), 1), true);
SELECT setval(pg_get_serial_sequence('"Sezone"', 'Id'), COALESCE((SELECT MAX("Id") FROM "Sezone"), 1), true);
SELECT setval(pg_get_serial_sequence('"Artikli"', 'Id'), COALESCE((SELECT MAX("Id") FROM "Artikli"), 1), true);
SELECT setval(pg_get_serial_sequence('prodaja_zaglavlje', 'id'), 6, true);
SELECT setval(pg_get_serial_sequence('prodaja_stavke', 'id'), 6, true);
SELECT setval(pg_get_serial_sequence('"DnevnikPromena"', 'Id'), 1, true);
