-- Manual verification for /api/analytics/supplier-sales-stats
-- Replace values in params CTE before running in Neon / psql.
--
-- Example:
--   from_utc    = '2026-01-06T00:00:00Z'
--   to_utc      = '2026-04-04T23:59:59Z'
--   store_id    = 1    -- use NULL for all stores
--   supplier_id = 42   -- use NULL for unknown bucket check

WITH params AS (
    SELECT
        TIMESTAMPTZ '2026-01-06T00:00:00Z' AS from_utc,
        TIMESTAMPTZ '2026-04-04T23:59:59Z' AS to_utc,
        NULL::integer AS store_id,
        42::integer AS supplier_id
),
previous_period AS (
    SELECT
        (p.from_utc - ((p.to_utc - p.from_utc) + INTERVAL '1 second')) AS previous_from_utc,
        (p.from_utc - INTERVAL '1 second') AS previous_to_utc
    FROM params p
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
current_sales_rows AS (
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
      AND (
          (p.supplier_id IS NOT NULL AND a."IDDobavljac" = p.supplier_id)
          OR (p.supplier_id IS NULL AND d."Id" IS NULL)
      )
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
),
previous_sales_rows AS (
    SELECT
        SUM(ps."Kolicina" * ps."Cena") AS previous_period_revenue,
        SUM(ps."Kolicina") AS previous_period_units
    FROM "ProdajaStavke" ps
    JOIN "ProdajaZaglavlja" pz ON ps."IdProdaja" = pz."Id"
    JOIN "Artikli" a ON ps."IdArtikal" = a."Id"
    LEFT JOIN "Dobavljaci" d ON a."IDDobavljac" = d."Id"
    CROSS JOIN params p
    CROSS JOIN previous_period pp
    WHERE pz."DatumProdaje" >= pp.previous_from_utc
      AND pz."DatumProdaje" <= pp.previous_to_utc
      AND (p.store_id IS NULL OR pz."IDObjekat" = p.store_id)
      AND (
          (p.supplier_id IS NOT NULL AND a."IDDobavljac" = p.supplier_id)
          OR (p.supplier_id IS NULL AND d."Id" IS NULL)
      )
),
aggregated AS (
    SELECT
        MAX(sr.supplier_id) AS supplier_id,
        MAX(sr.supplier_name) AS supplier_name,
        ROUND(SUM(sr.prihod), 2) AS ukupan_promet,
        SUM(sr.kolicina) AS ukupna_kolicina,
        COUNT(DISTINCT sr.artikal_id) AS broj_artikala_ukupno,
        COUNT(DISTINCT CASE WHEN pn.artikal_id IS NOT NULL THEN sr.artikal_id END) AS broj_artikala_sa_nivelacijom,
        ROUND(SUM(CASE WHEN pn.prva_nivelacija_utc IS NOT NULL AND sr.datum_prodaje < pn.prva_nivelacija_utc THEN sr.prihod ELSE 0 END), 2) AS pre_nivelacije_promet,
        ROUND(SUM(CASE WHEN pn.prva_nivelacija_utc IS NOT NULL AND sr.datum_prodaje >= pn.prva_nivelacija_utc THEN sr.prihod ELSE 0 END), 2) AS posle_nivelacije_promet,
        SUM(CASE WHEN pn.prva_nivelacija_utc IS NOT NULL AND sr.datum_prodaje < pn.prva_nivelacija_utc THEN sr.kolicina ELSE 0 END) AS pre_nivelacije_kolicina,
        SUM(CASE WHEN pn.prva_nivelacija_utc IS NOT NULL AND sr.datum_prodaje >= pn.prva_nivelacija_utc THEN sr.kolicina ELSE 0 END) AS posle_nivelacije_kolicina,
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
        , 2) AS margin_data_coverage_pct,
        ROUND(
            CASE
                WHEN SUM(sr.prihod) > 0
                    THEN (
                        SUM(CASE WHEN pn.prva_nivelacija_utc IS NOT NULL THEN sr.prihod ELSE 0 END)
                        / SUM(sr.prihod)
                    ) * 100
                ELSE 0
            END
        , 2) AS pre_post_coverage_pct
    FROM current_sales_rows sr
    LEFT JOIN prva_nivelacija pn ON pn.artikal_id = sr.artikal_id
)
SELECT
    a.supplier_id,
    a.supplier_name,
    a.ukupan_promet,
    a.ukupna_kolicina,
    a.broj_artikala_ukupno,
    a.broj_artikala_sa_nivelacijom,
    a.pre_nivelacije_promet,
    a.posle_nivelacije_promet,
    a.pre_nivelacije_kolicina,
    a.posle_nivelacije_kolicina,
    a.revenue_with_cost,
    a.margin_contribution,
    a.margin_pct,
    a.margin_data_coverage_pct,
    a.pre_post_coverage_pct AS pre_post_nivelacija_revenue_coverage_pct,
    ROUND(COALESCE(p.previous_period_revenue, 0), 2) AS previous_period_revenue,
    COALESCE(p.previous_period_units, 0) AS previous_period_units,
    CASE
        WHEN COALESCE(p.previous_period_revenue, 0) > 0
            THEN ROUND(((a.ukupan_promet - p.previous_period_revenue) / p.previous_period_revenue) * 100, 2)
        ELSE NULL
    END AS pop_revenue_change_pct,
    CASE
        WHEN COALESCE(p.previous_period_units, 0) > 0
            THEN ROUND(((a.ukupna_kolicina - p.previous_period_units)::numeric / p.previous_period_units) * 100, 2)
        ELSE NULL
    END AS pop_units_change_pct,
    CASE
        WHEN a.pre_nivelacije_promet > 0
            THEN ROUND(((a.posle_nivelacije_promet - a.pre_nivelacije_promet) / a.pre_nivelacije_promet) * 100, 2)
        ELSE NULL
    END AS pre_post_nivelacija_revenue_impact_pct,
    CASE
        WHEN a.pre_nivelacije_kolicina > 0
            THEN ROUND(((a.posle_nivelacije_kolicina - a.pre_nivelacije_kolicina)::numeric / a.pre_nivelacije_kolicina) * 100, 2)
        ELSE NULL
    END AS pre_post_nivelacija_units_impact_pct
FROM aggregated a
CROSS JOIN previous_sales_rows p;
