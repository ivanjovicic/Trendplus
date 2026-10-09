-- RQ601: read-only purchase-cost scale reconciliation.
--
-- Run this query against the Trendplus operational PostgreSQL database. It
-- intentionally does not create a view or write any data. InventoryMovementFacts
-- is an analytics-database copy; when the databases are separate, use the
-- DnevnikPromena result as the raw inbound extract and join by ArtikalId.
-- Missing/non-positive costs stay NULL and are never converted to zero.

-- RQ601_REPORT_QUERY_START
WITH master AS (
    SELECT
        a."Id" AS article_id,
        a."PLU" AS plu,
        a."Naziv" AS article_name,
        a."IDDobavljac" AS supplier_id,
        a."IDObjekat" AS store_id,
        a."NabavnaCena" AS master_cost_legacy,
        a."NabavnaCenaDin" AS master_cost_rsd,
        a."ProdajnaCena" AS retail_price,
        a."Kolicina" AS on_hand_units
    FROM "Artikli" a
),
inbound_ranked AS (
    SELECT
        d."ArtikalId" AS article_id,
        d."IDObjekat" AS store_id,
        d."Datum" AS inbound_at,
        round(d."Iznos" / NULLIF(abs(d."Kolicina"), 0), 4) AS inbound_unit_cost,
        row_number() OVER (
            PARTITION BY d."ArtikalId"
            ORDER BY d."Datum" DESC, d."Id" DESC
        ) AS row_number
    FROM "DnevnikPromena" d
    WHERE d."ArtikalId" IS NOT NULL
      AND d."TipPromene" IN ('Ulaz robe', 'Prenos ulaz', 'Povrat kupca')
      AND COALESCE(d."Kolicina", 0) > 0
      AND d."Iznos" > 0
      -- Access import placeholders (zero amount) are not reliable receipts.
      AND NOT (
          lower(COALESCE(d."DataOrigin", 'existing')) = 'access'
          AND d."Iznos" = 0
      )
),
latest_inbound AS (
    SELECT article_id, store_id, inbound_at, inbound_unit_cost
    FROM inbound_ranked
    WHERE row_number = 1
),
sale_ranked AS (
    SELECT
        s."id_artikal" AS article_id,
        h."id_objekat" AS sale_store_id,
        h."datum_prodaje" AS sold_at,
        s."nabavna_cena" AS sale_line_cost_raw,
        CASE
            WHEN s."nabavna_cena" > 0 THEN s."nabavna_cena"
            WHEN a."NabavnaCenaDin" > 0 THEN a."NabavnaCenaDin"
            WHEN a."NabavnaCena" > 0 THEN a."NabavnaCena"
            ELSE NULL
        END AS sale_line_cost_effective,
        CASE
            WHEN s."nabavna_cena" > 0 THEN 'source_sale_line'
            WHEN a."NabavnaCenaDin" > 0 THEN 'master_nabavnacena_din_backfill'
            WHEN a."NabavnaCena" > 0 THEN 'master_nabavnacena_backfill'
            ELSE 'unknown'
        END AS sale_line_cost_origin,
        row_number() OVER (
            PARTITION BY s."id_artikal"
            ORDER BY h."datum_prodaje" DESC, s."id" DESC
        ) AS row_number
    FROM "prodaja_stavke" s
    JOIN "prodaja_zaglavlje" h ON h."id" = s."id_prodaja"
    JOIN "Artikli" a ON a."Id" = s."id_artikal"
),
latest_sale AS (
    SELECT
        article_id,
        sale_store_id,
        sold_at,
        sale_line_cost_raw,
        sale_line_cost_effective,
        sale_line_cost_origin
    FROM sale_ranked
    WHERE row_number = 1
)
SELECT
    m.article_id,
    m.plu,
    m.article_name,
    m.supplier_id,
    m.store_id,
    m.master_cost_legacy,
    m.master_cost_rsd,
    i.inbound_unit_cost AS latest_inbound_unit_cost,
    i.inbound_at AS latest_inbound_at,
    s.sale_line_cost_raw AS latest_sale_line_cost_raw,
    s.sale_line_cost_effective AS latest_sale_line_cost,
    s.sale_line_cost_origin,
    s.sold_at AS latest_sale_at,
    s.sale_store_id,
    m.retail_price,
    m.on_hand_units,
    CASE
        WHEN m.master_cost_legacy > 0 AND s.sale_line_cost_effective > 0
            THEN round(m.master_cost_legacy / s.sale_line_cost_effective, 6)
        ELSE NULL
    END AS master_legacy_to_sale_ratio,
    CASE
        WHEN m.master_cost_rsd > 0 AND s.sale_line_cost_effective > 0
            THEN round(m.master_cost_rsd / s.sale_line_cost_effective, 6)
        ELSE NULL
    END AS master_rsd_to_sale_ratio,
    CASE
        WHEN m.master_cost_legacy > 0 AND i.inbound_unit_cost > 0
            THEN round(m.master_cost_legacy / i.inbound_unit_cost, 6)
        ELSE NULL
    END AS master_legacy_to_inbound_ratio,
    CASE
        WHEN m.master_cost_rsd > 0 AND i.inbound_unit_cost > 0
            THEN round(m.master_cost_rsd / i.inbound_unit_cost, 6)
        ELSE NULL
    END AS master_rsd_to_inbound_ratio,
    CASE
        WHEN m.on_hand_units > 0 AND m.master_cost_legacy > 0
            THEN round(m.on_hand_units * m.master_cost_legacy, 2)
        ELSE NULL
    END AS stock_value_master_legacy,
    CASE
        WHEN m.on_hand_units > 0 AND m.master_cost_rsd > 0
            THEN round(m.on_hand_units * m.master_cost_rsd, 2)
        ELSE NULL
    END AS stock_value_master_rsd,
    CASE
        WHEN m.on_hand_units > 0 AND i.inbound_unit_cost > 0
            THEN round(m.on_hand_units * i.inbound_unit_cost, 2)
        ELSE NULL
    END AS stock_value_inbound,
    CASE
        WHEN m.on_hand_units > 0 AND s.sale_line_cost_effective > 0
            THEN round(m.on_hand_units * s.sale_line_cost_effective, 2)
        ELSE NULL
    END AS stock_value_sale_line,
    CASE
        WHEN m.master_cost_legacy > 0
             AND s.sale_line_cost_effective > 0
             AND m.master_cost_legacy < s.sale_line_cost_effective * 0.25
            THEN true
        WHEN m.master_cost_rsd > 0
             AND s.sale_line_cost_effective > 0
             AND m.master_cost_rsd < s.sale_line_cost_effective * 0.25
            THEN true
        ELSE false
    END AS master_scale_suspicious
FROM master m
LEFT JOIN latest_inbound i ON i.article_id = m.article_id
LEFT JOIN latest_sale s ON s.article_id = m.article_id
ORDER BY
    COALESCE(stock_value_inbound, stock_value_sale_line, stock_value_master_rsd, stock_value_master_legacy, 0) DESC,
    m.article_id;
-- RQ601_REPORT_QUERY_END

-- For a supplier-level summary, export the report rows and group by supplier_id:
--   count(*), count(*) FILTER (WHERE latest_sale_line_cost IS NOT NULL),
--   count(*) FILTER (WHERE sale_line_cost_origin LIKE 'master_%_backfill'),
--   sum(stock_value_master_rsd), sum(stock_value_inbound), sum(stock_value_sale_line),
--   percentile_cont(ARRAY[0.25, 0.5, 0.75]) WITHIN GROUP
--       (ORDER BY master_rsd_to_sale_ratio) FILTER (WHERE master_rsd_to_sale_ratio IS NOT NULL).
