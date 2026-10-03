-- RQ548 isolated PostgreSQL source fixture. Store 1 is imported; store 2 is existing.
CREATE TABLE "StoresDim" (
    "StoreKey" integer PRIMARY KEY,
    "City" character varying(200),
    "DataOrigin" character varying(32) NOT NULL DEFAULT 'existing',
    "Menedzer" character varying(200),
    "Region" character varying(100),
    "StoreId" integer NOT NULL UNIQUE,
    "StoreName" character varying(300) NOT NULL,
    "Telefon" character varying(50)
);

INSERT INTO "StoresDim" ("StoreKey", "StoreId", "StoreName", "DataOrigin")
VALUES (1, 1, 'RQ548 imported store', 'access'), (2, 2, 'RQ548 existing store', 'existing');

INSERT INTO "Dobavljaci" ("Id", "Naziv", "DataOrigin")
VALUES (1, 'RQ548 supplier', 'existing');

INSERT INTO "TipoviObuce" ("Id", "Naziv", "DataOrigin")
VALUES (1, 'RQ548 footwear', 'existing');

INSERT INTO "Artikli"
    ("Id", "PLU", "Naziv", "NabavnaCena", "NabavnaCenaDin", "PrvaProdajnaCena", "ProdajnaCena",
     "IDDobavljac", "IDTipObuce", "UpdatedAt", "Kolicina", "IDObjekat", "Kategorija", "DataOrigin")
VALUES
    (101, 'RQ548-S1-NET',      'RQ548 net signed',       50,  50, 100, 100, 1, 1, CURRENT_TIMESTAMP - INTERVAL '30 days', 10, 1, 'Obuca', 'access'),
    (102, 'RQ548-S1-MARK',     'RQ548 markdown events',  50,  50, 100, 100, 1, 1, CURRENT_TIMESTAMP - INTERVAL '30 days',  6, 1, 'Obuca', 'access'),
    (103, 'RQ548-S1-EXCLUDED', 'RQ548 receipt policy',   50,  50, 100, 100, 1, 1, CURRENT_TIMESTAMP - INTERVAL '30 days',  5, 1, 'Obuca', 'access'),
    (104, 'RQ548-S1-NEVER',    'RQ548 never sold',       50,  50, 100, 100, 1, 1, CURRENT_TIMESTAMP - INTERVAL '200 days', 9, 1, 'Obuca', 'access'),
    (105, 'RQ548-S1-NEW',      'RQ548 recently received',50,  50, 100, 100, 1, 1, CURRENT_TIMESTAMP - INTERVAL '3 days',   4, 1, 'Obuca', 'access'),
    (106, 'RQ548-S1-BELOW',    'RQ548 below cost',      100, 100,  80,  80, 1, 1, CURRENT_TIMESTAMP - INTERVAL '30 days', 8, 1, 'Obuca', 'access'),
    (201, 'RQ548-S2-NET',      'RQ548 net signed',       50,  50, 100, 100, 1, 1, CURRENT_TIMESTAMP - INTERVAL '30 days', 12, 2, 'Obuca', 'existing'),
    (202, 'RQ548-S2-MARK',     'RQ548 markdown events',  50,  50, 100, 100, 1, 1, CURRENT_TIMESTAMP - INTERVAL '30 days',  8, 2, 'Obuca', 'existing'),
    (203, 'RQ548-S2-EXCLUDED', 'RQ548 receipt policy',   50,  50, 100, 100, 1, 1, CURRENT_TIMESTAMP - INTERVAL '30 days',  7, 2, 'Obuca', 'existing'),
    (204, 'RQ548-S2-NEVER',    'RQ548 never sold',       50,  50, 100, 100, 1, 1, CURRENT_TIMESTAMP - INTERVAL '200 days', 10, 2, 'Obuca', 'existing'),
    (205, 'RQ548-S2-NEW',      'RQ548 recently received',50,  50, 100, 100, 1, 1, CURRENT_TIMESTAMP - INTERVAL '3 days',   5, 2, 'Obuca', 'existing'),
    (206, 'RQ548-S2-BELOW',    'RQ548 below cost',      100, 100,  80,  80, 1, 1, CURRENT_TIMESTAMP - INTERVAL '30 days', 11, 2, 'Obuca', 'existing');

INSERT INTO prodaja_zaglavlje (id, broj_racuna, datum_prodaje, id_objekat, korisnik_ime, data_origin)
VALUES
    (1, 'RQ548-S1-NET-7D',      CURRENT_TIMESTAMP - INTERVAL '7 days',  1, 'rq548', 'access'),
    (2, 'RQ548-S1-NET-3D',      CURRENT_TIMESTAMP - INTERVAL '3 days',  1, 'rq548', 'access'),
    (3, 'RQ548-S1-NET-90D',     CURRENT_TIMESTAMP - INTERVAL '90 days', 1, 'rq548', 'access'),
    (4, 'RQ548-S1-MARK-40D',    CURRENT_TIMESTAMP - INTERVAL '40 days', 1, 'rq548', 'access'),
    (5, '  dUg  ',              CURRENT_TIMESTAMP - INTERVAL '1 day',   1, 'rq548', 'access'),
    (6, ' KoReKcIjA ',          CURRENT_TIMESTAMP - INTERVAL '2 days',  1, 'rq548', 'access'),
    (7, 'RQ548-S1-EXCLUDED-8D', CURRENT_TIMESTAMP - INTERVAL '8 days',  1, 'rq548', 'access'),
    (8, 'RQ548-S1-BELOW-20D',   CURRENT_TIMESTAMP - INTERVAL '20 days', 1, 'rq548', 'access'),
    (9, 'RQ548-S2-NET-7D',      CURRENT_TIMESTAMP - INTERVAL '7 days',  2, 'rq548', 'existing'),
    (10,'RQ548-S2-NET-3D',      CURRENT_TIMESTAMP - INTERVAL '3 days',  2, 'rq548', 'existing'),
    (11,'RQ548-S2-NET-90D',     CURRENT_TIMESTAMP - INTERVAL '90 days', 2, 'rq548', 'existing'),
    (12,'RQ548-S2-MARK-40D',    CURRENT_TIMESTAMP - INTERVAL '40 days', 2, 'rq548', 'existing'),
    (13,'  dUg  ',              CURRENT_TIMESTAMP - INTERVAL '1 day',   2, 'rq548', 'existing'),
    (14,' KoReKcIjA ',          CURRENT_TIMESTAMP - INTERVAL '2 days',  2, 'rq548', 'existing'),
    (15,'RQ548-S2-EXCLUDED-8D', CURRENT_TIMESTAMP - INTERVAL '8 days',  2, 'rq548', 'existing'),
    (16,'RQ548-S2-BELOW-20D',   CURRENT_TIMESTAMP - INTERVAL '20 days', 2, 'rq548', 'existing');

INSERT INTO prodaja_stavke
    (id, id_prodaja, id_artikal, kolicina, cena, nabavna_cena, supplier_id_at_sale, shoe_type_id_at_sale, attribution_basis)
VALUES
    (1, 1, 101,  5, 100, 50, 1, 1, 'sale_snapshot'),
    (2, 2, 101, -2, 100, 50, 1, 1, 'sale_snapshot'),
    (3, 3, 101,  4, 100, 50, 1, 1, 'sale_snapshot'),
    (4, 4, 102,  3, 100, 50, 1, 1, 'sale_snapshot'),
    (5, 5, 103, 50, 100, 50, 1, 1, 'sale_snapshot'),
    (6, 6, 103, 20, 100, 50, 1, 1, 'sale_snapshot'),
    (7, 7, 103,  1, 100, 50, 1, 1, 'sale_snapshot'),
    (8, 8, 106, 10,  80,100, 1, 1, 'sale_snapshot'),
    (9, 9, 201,  5, 100, 50, 1, 1, 'sale_snapshot'),
    (10,10, 201, -2, 100, 50, 1, 1, 'sale_snapshot'),
    (11,11, 201,  4, 100, 50, 1, 1, 'sale_snapshot'),
    (12,12, 202,  3, 100, 50, 1, 1, 'sale_snapshot'),
    (13,13, 203, 50, 100, 50, 1, 1, 'sale_snapshot'),
    (14,14, 203, 20, 100, 50, 1, 1, 'sale_snapshot'),
    (15,15, 203,  1, 100, 50, 1, 1, 'sale_snapshot'),
    (16,16, 206, 10,  80,100, 1, 1, 'sale_snapshot');

INSERT INTO "DnevnikPromena"
    ("Id", "TipPromene", "Datum", "Iznos", "ArtikalId", "StaraProdajnaCena", "NovaProdajnaCena", "IDObjekat", "DataOrigin")
VALUES
    (1, 'Nivelacija',      CURRENT_TIMESTAMP - INTERVAL '40 days', 0, 102, 100,  80, 1, 'access'),
    (2, 'Nivelacija cena', CURRENT_TIMESTAMP - INTERVAL '35 days', 0, 102, 100,  90, 1, 'access'),
    (3, 'Nivelacija',      CURRENT_TIMESTAMP - INTERVAL '30 days', 0, 102,  80, 100, 1, 'access'),
    (4, 'Nivelacija',      CURRENT_TIMESTAMP - INTERVAL '25 days', 0, 102, 100,  80, NULL, 'access'),
    (5, 'Nivelacija',      CURRENT_TIMESTAMP - INTERVAL '40 days', 0, 202, 100,  80, 2, 'existing'),
    (6, 'Nivelacija cena', CURRENT_TIMESTAMP - INTERVAL '35 days', 0, 202, 100,  90, 2, 'existing'),
    (7, 'Nivelacija',      CURRENT_TIMESTAMP - INTERVAL '30 days', 0, 202,  80, 100, 2, 'existing'),
    (8, 'Ulaz robe',       CURRENT_TIMESTAMP - INTERVAL '3 days',   0, 105, NULL, NULL, 1, 'access'),
    (9, 'Ulaz robe',       CURRENT_TIMESTAMP - INTERVAL '3 days',   0, 205, NULL, NULL, 2, 'existing'),
    (10,'Ulaz robe',       CURRENT_TIMESTAMP - INTERVAL '200 days', 0, 104, NULL, NULL, 1, 'access'),
    (11,'Ulaz robe',       CURRENT_TIMESTAMP - INTERVAL '200 days', 0, 204, NULL, NULL, 2, 'existing'),
    (12,'Ulaz robe',       CURRENT_TIMESTAMP - INTERVAL '40 days',  0, 106, NULL, NULL, 1, 'access'),
    (13,'Ulaz robe',       CURRENT_TIMESTAMP - INTERVAL '40 days',  0, 206, NULL, NULL, 2, 'existing');

SELECT setval(pg_get_serial_sequence('"Dobavljaci"', 'Id'), 1, true);
SELECT setval(pg_get_serial_sequence('"TipoviObuce"', 'Id'), 1, true);
SELECT setval(pg_get_serial_sequence('"Artikli"', 'Id'), 206, true);
SELECT setval(pg_get_serial_sequence('prodaja_zaglavlje', 'id'), 16, true);
SELECT setval(pg_get_serial_sequence('prodaja_stavke', 'id'), 16, true);
SELECT setval(pg_get_serial_sequence('"DnevnikPromena"', 'Id'), 13, true);
