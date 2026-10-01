-- RQ547 fixture schema. Isolated Testcontainers database only; never run against production.
CREATE TABLE "Artikli" (
    "Id" bigint PRIMARY KEY,
    "PLU" text,
    "Naziv" text,
    "Kategorija" text,
    "IDDobavljac" bigint,
    "ProdajnaCena" numeric(18, 2),
    "NabavnaCena" numeric(18, 2)
);

CREATE TABLE "DnevnikPromena" (
    "Id" bigint PRIMARY KEY,
    "ArtikalId" bigint,
    "Datum" date,
    "TipPromene" text,
    "BrojRacuna" text,
    "StaraProdajnaCena" numeric(18, 2),
    "NovaProdajnaCena" numeric(18, 2),
    "DobavljacId" bigint,
    "IDObjekat" bigint,
    "DataOrigin" text
);

CREATE TABLE prodaja_zaglavlje (
    id bigint PRIMARY KEY,
    datum_prodaje timestamp with time zone NOT NULL,
    broj_racuna text,
    id_objekat bigint,
    data_origin text
);

CREATE TABLE prodaja_stavke (
    id_prodaja bigint NOT NULL,
    id_artikal bigint NOT NULL,
    kolicina numeric(18, 2) NOT NULL,
    cena numeric(18, 2),
    nabavna_cena numeric(18, 2)
);

CREATE TABLE price_history (
    source_dnevnik_id bigint PRIMARY KEY,
    article_id bigint NOT NULL
);

CREATE TABLE nivelacija_did_fixture (
    article_id bigint NOT NULL,
    control_article_id bigint
);
CREATE VIEW vw_nivelacija_did AS
SELECT article_id, control_article_id FROM nivelacija_did_fixture;

CREATE TABLE nivelacija_view_projection (
    price_event_id bigint PRIMARY KEY,
    event_date date NOT NULL,
    article_id bigint NOT NULL,
    pre_qty numeric,
    post_qty numeric
);
CREATE VIEW vw_vendor_sales_nivelacija AS
SELECT price_event_id, event_date, article_id, pre_qty, post_qty
FROM nivelacija_view_projection;
