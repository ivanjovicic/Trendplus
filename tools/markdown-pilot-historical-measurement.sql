-- RQ602 frozen cohort measurement, batch 23, read-only.
-- Run after reviewing markdown-pilot-cohort-2026-10-10.sql.
-- Cost values are reported as unproven unless the line has an explicit
-- provenance contract; the local batch schema currently has no certified
-- historical cost provenance for these rows.

WITH frozen_cohort(
    cohort_no, treatment_event_id, action_date, treated_article_id,
    store_id, supplier_id, shoe_type_id, price_before_rsd, price_after_rsd,
    control_article_id
) AS (
    VALUES
        (1,12256::bigint,DATE '2026-06-18',21609,2082886995,1447386007,-424518832,4990::numeric,3990::numeric,14071),
        (2,12257::bigint,DATE '2026-06-18',21621,2082886995,1631301963,-424518832,8990::numeric,6990::numeric,20292),
        (3,12258::bigint,DATE '2026-06-18',21647,2082886995,894901603,1846163462,8800::numeric,6390::numeric,14182),
        (4,12259::bigint,DATE '2026-06-18',21650,2082886995,894901603,1846163462,5300::numeric,4500::numeric,12762),
        (5,12260::bigint,DATE '2026-06-18',21661,2082886995,894901603,1846163462,7900::numeric,6200::numeric,12763),
        (6,12231::bigint,DATE '2026-05-18',21164,2082886995,1496651500,-2004188974,7990::numeric,6200::numeric,12287),
        (7,12232::bigint,DATE '2026-05-18',21232,2082886995,1496651500,-424518832,7990::numeric,6200::numeric,12251),
        (8,12233::bigint,DATE '2026-05-18',21245,2082886995,894901603,1846163462,5200::numeric,4200::numeric,12764),
        (9,12237::bigint,DATE '2026-05-18',21257,2082886995,894901603,1846163462,5200::numeric,4200::numeric,13397),
        (10,12238::bigint,DATE '2026-05-18',21411,2082886995,-1494110814,1846163462,3200::numeric,2600::numeric,13572),
        (11,12239::bigint,DATE '2026-05-18',21460,2082886995,1447386007,1222369825,6990::numeric,5490::numeric,18092),
        (12,12240::bigint,DATE '2026-05-18',21472,2082886995,1496651500,-2004188974,10990::numeric,8990::numeric,12303),
        (13,12241::bigint,DATE '2026-05-18',21473,2082886995,1681773579,-2004188974,5990::numeric,4990::numeric,9378),
        (14,12242::bigint,DATE '2026-05-18',21474,2082886995,1681773579,-2004188974,7990::numeric,5990::numeric,12234),
        (15,12243::bigint,DATE '2026-05-18',21475,2082886995,1681773579,-2004188974,7990::numeric,6200::numeric,20430),
        (16,12244::bigint,DATE '2026-05-18',21476,2082886995,1681773579,-2004188974,7490::numeric,5490::numeric,20573),
        (17,12245::bigint,DATE '2026-05-18',21482,2082886995,1496651500,-2004188974,8990::numeric,7990::numeric,12304),
        (18,12246::bigint,DATE '2026-05-18',21485,2082886995,1496651500,-2004188974,9590::numeric,7590::numeric,12596),
        (19,12247::bigint,DATE '2026-05-18',21488,2082886995,1496651500,-2004188974,8990::numeric,7990::numeric,12597),
        (20,12248::bigint,DATE '2026-05-18',21565,2082886995,1447386007,-424518832,10490::numeric,7490::numeric,14072)
),
members AS (
    SELECT c.*, 'treated'::text AS role, c.treated_article_id AS article_id
    FROM frozen_cohort c
    UNION ALL
    SELECT c.*, 'control'::text, c.control_article_id
    FROM frozen_cohort c
),
horizon AS (
    SELECT id_objekat AS store_id,
           source_batch_id,
           MIN((datum_prodaje AT TIME ZONE 'Europe/Belgrade')::date) AS source_from_local_date,
           MAX((datum_prodaje AT TIME ZONE 'Europe/Belgrade')::date) AS source_through_local_date,
           COUNT(DISTINCT (datum_prodaje AT TIME ZONE 'Europe/Belgrade')::date) AS observed_sales_days
    FROM prodaja_zaglavlje
    WHERE source_batch_id = 23
    GROUP BY id_objekat, source_batch_id
),
markdowns AS (
    SELECT m.cohort_no,
           m.role,
           COUNT(d."Id") AS markdown_events_in_56_days
    FROM members m
    LEFT JOIN "DnevnikPromena" d
      ON d."SourceBatchId" = 23
     AND d."ArtikalId" = m.article_id
     AND d."IDObjekat" = m.store_id
     AND LOWER(BTRIM(d."TipPromene")) IN ('nivelacija','nivelacija cena')
     AND d."StaraProdajnaCena" IS NOT NULL
     AND d."NovaProdajnaCena" < d."StaraProdajnaCena"
     AND (d."Datum" AT TIME ZONE 'Europe/Belgrade')::date >= m.action_date - 28
     AND (d."Datum" AT TIME ZONE 'Europe/Belgrade')::date < m.action_date + 28
    GROUP BY m.cohort_no, m.role
),
sales AS (
    SELECT m.cohort_no,
           m.role,
           m.article_id,
           m.store_id,
           m.action_date,
           m.price_before_rsd,
           m.price_after_rsd,
           h.id AS receipt_id,
           s.id AS line_id,
           s.kolicina,
           s.cena,
           s.nabavna_cena,
           s.attribution_basis,
           (h.datum_prodaje AT TIME ZONE 'Europe/Belgrade')::date AS sale_date
    FROM members m
    LEFT JOIN prodaja_zaglavlje h
      ON h.id_objekat = m.store_id
     AND h.source_batch_id = 23
     AND (h.datum_prodaje AT TIME ZONE 'Europe/Belgrade')::date >= m.action_date - 28
     AND (h.datum_prodaje AT TIME ZONE 'Europe/Belgrade')::date < m.action_date + 28
     AND UPPER(BTRIM(COALESCE(h.broj_racuna,''))) NOT IN ('DUG','KOREKCIJA')
    LEFT JOIN prodaja_stavke s
      ON s.id_prodaja = h.id
     AND s.id_artikal = m.article_id
     AND s.source_batch_id = 23
),
metrics AS (
    SELECT s.cohort_no,
           s.role,
           s.article_id,
           s.store_id,
           s.action_date,
           s.price_before_rsd,
           s.price_after_rsd,
           COUNT(s.line_id) FILTER (WHERE s.sale_date < s.action_date) AS pre_lines,
           COALESCE(SUM(s.kolicina) FILTER (WHERE s.sale_date < s.action_date),0) AS pre_qty,
           COALESCE(SUM(s.kolicina * s.cena) FILTER (WHERE s.sale_date < s.action_date),0) AS pre_revenue_rsd,
           COUNT(s.line_id) FILTER (WHERE s.sale_date >= s.action_date) AS post_lines,
           COALESCE(SUM(s.kolicina) FILTER (WHERE s.sale_date >= s.action_date),0) AS post_qty,
           COALESCE(SUM(s.kolicina * s.cena) FILTER (WHERE s.sale_date >= s.action_date),0) AS post_revenue_rsd,
           COUNT(s.line_id) FILTER (WHERE s.sale_date < s.action_date AND s.nabavna_cena IS NULL) AS pre_unknown_cost_lines,
           COUNT(s.line_id) FILTER (WHERE s.sale_date >= s.action_date AND s.nabavna_cena IS NULL) AS post_unknown_cost_lines,
           COUNT(s.line_id) FILTER (WHERE s.sale_date < s.action_date AND s.nabavna_cena IS NOT NULL) AS pre_numeric_cost_lines,
           COUNT(s.line_id) FILTER (WHERE s.sale_date >= s.action_date AND s.nabavna_cena IS NOT NULL) AS post_numeric_cost_lines,
           SUM((s.cena - s.nabavna_cena) * s.kolicina) FILTER (WHERE s.sale_date < s.action_date AND s.nabavna_cena IS NOT NULL) AS pre_unproven_margin_rsd,
           SUM((s.cena - s.nabavna_cena) * s.kolicina) FILTER (WHERE s.sale_date >= s.action_date AND s.nabavna_cena IS NOT NULL) AS post_unproven_margin_rsd
    FROM sales s
    GROUP BY s.cohort_no, s.role, s.article_id, s.store_id, s.action_date, s.price_before_rsd, s.price_after_rsd
)
SELECT m.*,
       m.price_after_rsd - m.price_before_rsd AS price_delta_rsd,
       m.post_revenue_rsd - m.pre_revenue_rsd AS revenue_delta_rsd,
       md.markdown_events_in_56_days,
       h.source_from_local_date,
       h.source_through_local_date,
       h.observed_sales_days,
       'unverified_source_coverage_not_proven'::text AS pre_window_status,
       'unverified_source_coverage_not_proven'::text AS post_window_status,
       'descriptive_unproven_cost'::text AS margin_trust_status
FROM metrics m
JOIN markdowns md ON md.cohort_no = m.cohort_no AND md.role = m.role
LEFT JOIN horizon h ON h.store_id = m.store_id AND h.source_batch_id = 23
ORDER BY m.cohort_no, m.role;
