-- Manual verification for /api/analytics/supplier-sales-stats
-- Replace values in params CTE before running in Neon / psql.
--
-- Example:
--   from_utc   = '2026-01-06T00:00:00Z'
--   to_utc     = '2026-04-04T23:59:59Z'
--   store_id   = 1   -- use NULL for all stores
--   supplier_id = 42

WITH params AS (
    SELECT
        TIMESTAMPTZ '2026-01-06T00:00:00Z' AS from_utc,
        TIMESTAMPTZ '2026-04-04T23:59:59Z' AS to_utc,
        NULL::integer AS store_id,
        42::integer AS supplier_id
),
prva_nivelacija AS (
    SELECT
        dp."ArtikalId" AS artikal_id,
        MIN(dp."Datum") AS prva_nivelacija_utc
    FROM "DnevnikPromena" dp
    CROSS JOIN params p
    WHERE dp."ArtikalId" IS NOT NULL
      AND dp."TipPromene" IN ('Nivelacija', 'Nivelacija cena')
      AND dp."Datum" <= p.to_utc
      AND (p.store_id IS NULL OR dp."IDObjekat" IS NULL OR dp."IDObjekat" = p.store_id)
    GROUP BY dp."ArtikalId"
),
sales_rows AS (
    SELECT
        a."IDDobavljac" AS supplier_id,
        COALESCE(NULLIF(BTRIM(d."Naziv"), ''), 'Nepoznato') AS supplier_name,
        a."Id" AS artikal_id,
        pz."DatumProdaje" AS datum_prodaje,
        SUM(ps."Kolicina") AS kolicina,
        SUM(ps."Kolicina" * ps."Cena") AS prihod,
        CASE
            WHEN ps."NabavnaCena" > 0 THEN ps."NabavnaCena"
            WHEN a."NabavnaCenaDin" > 0 THEN a."NabavnaCenaDin"
            WHEN a."NabavnaCena" > 0 THEN a."NabavnaCena"
            ELSE NULL
        END AS nabavna_cena
    FROM "ProdajaStavke" ps
    JOIN "ProdajaZaglavlja" pz ON ps."IdProdaja" = pz."Id"
    JOIN "Artikli" a ON ps."IdArtikal" = a."Id"
    LEFT JOIN "Dobavljaci" d ON a."IDDobavljac" = d."Id"
    CROSS JOIN params p
    WHERE pz."DatumProdaje" >= p.from_utc
      AND pz."DatumProdaje" <= p.to_utc
      AND (p.store_id IS NULL OR pz."IDObjekat" = p.store_id)
      AND a."IDDobavljac" = p.supplier_id
    GROUP BY
        a."IDDobavljac",
        COALESCE(NULLIF(BTRIM(d."Naziv"), ''), 'Nepoznato'),
        a."Id",
        pz."DatumProdaje",
        CASE
            WHEN ps."NabavnaCena" > 0 THEN ps."NabavnaCena"
            WHEN a."NabavnaCenaDin" > 0 THEN a."NabavnaCenaDin"
            WHEN a."NabavnaCena" > 0 THEN a."NabavnaCena"
            ELSE NULL
        END
)
SELECT
    sr.supplier_id,
    MAX(sr.supplier_name) AS supplier_name,
    ROUND(SUM(sr.prihod), 2) AS ukupan_promet,
    SUM(sr.kolicina) AS ukupna_kolicina,
    COUNT(DISTINCT sr.artikal_id) AS broj_artikala_ukupno,
    COUNT(DISTINCT CASE WHEN pn.artikal_id IS NOT NULL THEN sr.artikal_id END) AS broj_artikala_sa_nivelacijom,
    ROUND(SUM(CASE WHEN pn.prva_nivelacija_utc IS NOT NULL AND sr.datum_prodaje < pn.prva_nivelacija_utc THEN sr.prihod ELSE 0 END), 2) AS pre_nivelacije_promet,
    ROUND(SUM(CASE WHEN pn.prva_nivelacija_utc IS NOT NULL AND sr.datum_prodaje >= pn.prva_nivelacija_utc THEN sr.prihod ELSE 0 END), 2) AS posle_nivelacije_promet,
    ROUND(SUM(CASE WHEN sr.nabavna_cena IS NOT NULL THEN sr.prihod ELSE 0 END), 2) AS revenue_with_cost,
    ROUND(SUM(CASE WHEN sr.nabavna_cena IS NOT NULL THEN sr.prihod - (sr.kolicina * sr.nabavna_cena) ELSE 0 END), 2) AS margin_contribution,
    ROUND(
        CASE
            WHEN SUM(CASE WHEN sr.nabavna_cena IS NOT NULL THEN sr.prihod ELSE 0 END) > 0
                THEN (
                    SUM(CASE WHEN sr.nabavna_cena IS NOT NULL THEN sr.prihod - (sr.kolicina * sr.nabavna_cena) ELSE 0 END)
                    / SUM(CASE WHEN sr.nabavna_cena IS NOT NULL THEN sr.prihod ELSE 0 END)
                ) * 100
            ELSE 0
        END
    , 2) AS margin_pct,
    ROUND(
        CASE
            WHEN SUM(sr.prihod) > 0
                THEN (
                    SUM(CASE WHEN sr.nabavna_cena IS NOT NULL THEN sr.prihod ELSE 0 END)
                    / SUM(sr.prihod)
                ) * 100
            ELSE 0
        END
    , 2) AS margin_data_coverage_pct
FROM sales_rows sr
LEFT JOIN prva_nivelacija pn ON pn.artikal_id = sr.artikal_id
GROUP BY sr.supplier_id;
