-- RQ612 shadow-only candidate screen for the immutable RQ602 treated events.
-- Read-only. It does not change the RQ602 manifest, estimate uplift, or authorize action.
-- Treated event keys/baselines mirror tools/markdown-pilot-historical-measurement.sql.
-- Reserved article IDs mirror that frozen 40-member manifest so no frozen member
-- can be recycled as a new shadow candidate.
-- Exploratory defaults are deliberately configurable and not statistical cutoffs.
-- Historical current stock is never used as proof of availability in these windows.

WITH
screen_config AS (
    SELECT 23::bigint AS source_batch_id,
           1::numeric AS min_gross_pre_units,
           0.25::numeric AS max_pre_price_deviation,
           0.5::numeric AS min_pre_activity_ratio,
           2.0::numeric AS max_pre_activity_ratio,
           28::integer AS window_days
),
treated(cohort_no, treatment_event_id, action_date, treated_article_id,
        store_id, supplier_id, shoe_type_id, price_before_rsd) AS (
    VALUES
        (1,12256::bigint,DATE '2026-06-18',21609,2082886995,1447386007,-424518832,4990::numeric),
        (2,12257::bigint,DATE '2026-06-18',21621,2082886995,1631301963,-424518832,8990::numeric),
        (3,12258::bigint,DATE '2026-06-18',21647,2082886995,894901603,1846163462,8800::numeric),
        (4,12259::bigint,DATE '2026-06-18',21650,2082886995,894901603,1846163462,5300::numeric),
        (5,12260::bigint,DATE '2026-06-18',21661,2082886995,894901603,1846163462,7900::numeric),
        (6,12231::bigint,DATE '2026-05-18',21164,2082886995,1496651500,-2004188974,7990::numeric),
        (7,12232::bigint,DATE '2026-05-18',21232,2082886995,1496651500,-424518832,7990::numeric),
        (8,12233::bigint,DATE '2026-05-18',21245,2082886995,894901603,1846163462,5200::numeric),
        (9,12237::bigint,DATE '2026-05-18',21257,2082886995,894901603,1846163462,5200::numeric),
        (10,12238::bigint,DATE '2026-05-18',21411,2082886995,-1494110814,1846163462,3200::numeric),
        (11,12239::bigint,DATE '2026-05-18',21460,2082886995,1447386007,1222369825,6990::numeric),
        (12,12240::bigint,DATE '2026-05-18',21472,2082886995,1496651500,-2004188974,10990::numeric),
        (13,12241::bigint,DATE '2026-05-18',21473,2082886995,1681773579,-2004188974,5990::numeric),
        (14,12242::bigint,DATE '2026-05-18',21474,2082886995,1681773579,-2004188974,7990::numeric),
        (15,12243::bigint,DATE '2026-05-18',21475,2082886995,1681773579,-2004188974,7990::numeric),
        (16,12244::bigint,DATE '2026-05-18',21476,2082886995,1681773579,-2004188974,7490::numeric),
        (17,12245::bigint,DATE '2026-05-18',21482,2082886995,1496651500,-2004188974,8990::numeric),
        (18,12246::bigint,DATE '2026-05-18',21485,2082886995,1496651500,-424518832,9590::numeric),
        (19,12247::bigint,DATE '2026-05-18',21488,2082886995,1496651500,-424518832,8990::numeric),
        (20,12248::bigint,DATE '2026-05-18',21565,2082886995,1447386007,-424518832,10490::numeric)
),
reserved_frozen_members(article_id) AS (
    VALUES
        (21609),(21621),(21647),(21650),(21661),(21164),(21232),(21245),(21257),(21411),
        (21460),(21472),(21473),(21474),(21475),(21476),(21482),(21485),(21488),(21565),
        (14071),(20292),(14182),(12762),(12763),(12287),(12251),(12764),(13397),(13572),
        (18092),(12303),(9378),(12234),(20430),(20573),(12304),(12596),(12597),(14072)
),
candidate_pool AS (
    SELECT a."Id" AS article_id,
           a."PLU" AS plu,
           a."IDObjekat" AS store_id,
           a."IDDobavljac" AS supplier_id,
           a."IDTipObuce" AS shoe_type_id,
           a."DataOrigin" AS data_origin,
           a."SourceBatchId" AS source_batch_id
    FROM "Artikli" a
    CROSS JOIN screen_config cfg
    WHERE a."DataOrigin" = 'access'
      AND a."SourceBatchId" = cfg.source_batch_id
      AND a."IDObjekat" IS NOT NULL
      AND a."IDDobavljac" IS NOT NULL
      AND a."IDTipObuce" IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM reserved_frozen_members f WHERE f.article_id = a."Id")
),
exact_pairs AS (
    SELECT t.*, c.article_id AS candidate_article_id, c.plu AS candidate_plu,
           c.data_origin, c.source_batch_id AS candidate_batch_id
    FROM treated t
    JOIN candidate_pool c
      ON c.store_id = t.store_id
     AND c.supplier_id = t.supplier_id
     AND c.shoe_type_id = t.shoe_type_id
     AND c.article_id <> t.treated_article_id
),
unmatched_counts AS (
    SELECT t.cohort_no,
           count(c.article_id) FILTER (WHERE c.store_id = t.store_id AND c.shoe_type_id = t.shoe_type_id
                                           AND c.supplier_id IS DISTINCT FROM t.supplier_id) AS same_store_type_wrong_supplier,
           count(c.article_id) FILTER (WHERE c.store_id = t.store_id AND c.supplier_id = t.supplier_id
                                           AND c.shoe_type_id IS DISTINCT FROM t.shoe_type_id) AS same_store_supplier_wrong_type
    FROM treated t
    LEFT JOIN candidate_pool c ON c.store_id = t.store_id
    GROUP BY t.cohort_no
),
pre_sales AS (
    SELECT p.cohort_no,
           p.candidate_article_id,
           count(DISTINCT h.id) FILTER (WHERE s.id IS NOT NULL) AS pre_receipt_count,
           count(DISTINCT (h.datum_prodaje AT TIME ZONE 'Europe/Belgrade')::date)
               FILTER (WHERE s.id IS NOT NULL) AS pre_active_days,
           count(s.id) AS pre_sale_line_count,
           coalesce(sum(greatest(s.kolicina, 0)), 0)::numeric AS pre_gross_positive_units,
           coalesce(sum(s.kolicina), 0)::numeric AS pre_signed_units,
           count(s.id) FILTER (WHERE s.kolicina < 0) AS pre_return_line_count,
           coalesce(sum(s.kolicina * s.cena), 0)::numeric AS pre_signed_revenue_rsd,
           sum(s.kolicina * s.cena) FILTER (WHERE s.kolicina > 0)::numeric AS pre_gross_revenue_rsd,
           count(s.id) FILTER (WHERE s.nabavna_cena IS NULL OR s.nabavna_cena <= 0) AS pre_unknown_cost_lines,
           sum(s.kolicina * s.cena) FILTER (WHERE s.kolicina > 0)
             / nullif(sum(s.kolicina) FILTER (WHERE s.kolicina > 0), 0) AS pre_weighted_unit_price_rsd
    FROM exact_pairs p
    CROSS JOIN screen_config cfg
    LEFT JOIN prodaja_zaglavlje h
      ON h.id_objekat = p.store_id
     AND h.source_batch_id = cfg.source_batch_id
     AND (h.datum_prodaje AT TIME ZONE 'Europe/Belgrade')::date >= p.action_date - cfg.window_days
     AND (h.datum_prodaje AT TIME ZONE 'Europe/Belgrade')::date < p.action_date
     AND upper(btrim(coalesce(h.broj_racuna, ''))) NOT IN ('DUG', 'KOREKCIJA')
    LEFT JOIN prodaja_stavke s
      ON s.id_prodaja = h.id
     AND s.id_artikal = p.candidate_article_id
     AND s.source_batch_id = cfg.source_batch_id
    GROUP BY p.cohort_no, p.candidate_article_id
),
treated_pre_sales AS (
    SELECT t.cohort_no,
           coalesce(sum(greatest(s.kolicina, 0)), 0)::numeric AS treated_pre_gross_positive_units,
           coalesce(sum(s.kolicina), 0)::numeric AS treated_pre_signed_units,
           sum(s.kolicina * s.cena) FILTER (WHERE s.kolicina > 0)
             / nullif(sum(s.kolicina) FILTER (WHERE s.kolicina > 0), 0) AS treated_pre_weighted_unit_price_rsd
    FROM treated t
    CROSS JOIN screen_config cfg
    LEFT JOIN prodaja_zaglavlje h
      ON h.id_objekat = t.store_id
     AND h.source_batch_id = cfg.source_batch_id
     AND (h.datum_prodaje AT TIME ZONE 'Europe/Belgrade')::date >= t.action_date - cfg.window_days
     AND (h.datum_prodaje AT TIME ZONE 'Europe/Belgrade')::date < t.action_date
     AND upper(btrim(coalesce(h.broj_racuna, ''))) NOT IN ('DUG', 'KOREKCIJA')
    LEFT JOIN prodaja_stavke s
      ON s.id_prodaja = h.id
     AND s.id_artikal = t.treated_article_id
     AND s.source_batch_id = cfg.source_batch_id
    GROUP BY t.cohort_no
),
markdown_events AS (
    SELECT p.cohort_no, p.candidate_article_id, count(d."Id") AS markdown_event_count_56d
    FROM exact_pairs p
    CROSS JOIN screen_config cfg
    LEFT JOIN "DnevnikPromena" d
      ON d."ArtikalId" = p.candidate_article_id
     AND d."IDObjekat" = p.store_id
     AND lower(btrim(coalesce(d."TipPromene", ''))) IN ('nivelacija', 'nivelacija cena')
     AND d."StaraProdajnaCena" IS NOT NULL
     AND d."NovaProdajnaCena" < d."StaraProdajnaCena"
     AND (d."Datum" AT TIME ZONE 'Europe/Belgrade')::date >= p.action_date - cfg.window_days
     AND (d."Datum" AT TIME ZONE 'Europe/Belgrade')::date < p.action_date + cfg.window_days
    GROUP BY p.cohort_no, p.candidate_article_id
),
assessed AS (
    SELECT p.*,
           coalesce(ps.pre_receipt_count, 0) AS pre_receipt_count,
           coalesce(ps.pre_active_days, 0) AS pre_active_days,
           coalesce(ps.pre_sale_line_count, 0) AS pre_sale_line_count,
           coalesce(ps.pre_gross_positive_units, 0) AS pre_gross_positive_units,
           coalesce(ps.pre_signed_units, 0) AS pre_signed_units,
           coalesce(ps.pre_return_line_count, 0) AS pre_return_line_count,
           coalesce(ps.pre_signed_revenue_rsd, 0) AS pre_signed_revenue_rsd,
           ps.pre_gross_revenue_rsd,
           coalesce(ps.pre_unknown_cost_lines, 0) AS pre_unknown_cost_lines,
           ps.pre_weighted_unit_price_rsd,
           coalesce(tp.treated_pre_gross_positive_units, 0) AS treated_pre_gross_positive_units,
           coalesce(tp.treated_pre_signed_units, 0) AS treated_pre_signed_units,
           tp.treated_pre_weighted_unit_price_rsd,
           coalesce(me.markdown_event_count_56d, 0) AS markdown_event_count_56d,
           ps.pre_weighted_unit_price_rsd / nullif(p.price_before_rsd, 0) - 1 AS price_distance_ratio,
           ps.pre_signed_units / nullif(tp.treated_pre_signed_units, 0) AS pre_activity_ratio,
           CASE
             WHEN coalesce(ps.pre_gross_positive_units, 0) < cfg.min_gross_pre_units THEN 'zero_signal_pre_sales'
             WHEN ps.pre_weighted_unit_price_rsd IS NULL THEN 'missing_comparable_pre_price'
             WHEN abs(ps.pre_weighted_unit_price_rsd / nullif(p.price_before_rsd, 0) - 1) > cfg.max_pre_price_deviation THEN 'pre_price_mismatch'
             WHEN tp.treated_pre_signed_units <= 0 THEN 'treated_pre_demand_missing'
             WHEN ps.pre_signed_units / nullif(tp.treated_pre_signed_units, 0)
                    NOT BETWEEN cfg.min_pre_activity_ratio AND cfg.max_pre_activity_ratio THEN 'pre_demand_mismatch'
             WHEN coalesce(me.markdown_event_count_56d, 0) > 0 THEN 'overlapping_or_repeated_markdown'
             ELSE 'screen_pass_availability_unverified'
           END AS screen_status
    FROM exact_pairs p
    LEFT JOIN pre_sales ps USING (cohort_no, candidate_article_id)
    LEFT JOIN treated_pre_sales tp USING (cohort_no)
    LEFT JOIN markdown_events me USING (cohort_no, candidate_article_id)
    CROSS JOIN screen_config cfg
),
reusable_counts AS (
    SELECT candidate_article_id, count(*) AS qualifying_treated_events
    FROM assessed
    WHERE screen_status = 'screen_pass_availability_unverified'
    GROUP BY candidate_article_id
),
unique_screened_rank AS (
    SELECT a.cohort_no,
           a.candidate_article_id,
           row_number() OVER (
               PARTITION BY a.cohort_no
               ORDER BY abs(coalesce(a.price_distance_ratio, 999)),
                        abs(coalesce(a.pre_activity_ratio, 999) - 1),
                        a.candidate_article_id
           ) AS unique_candidate_rank
    FROM assessed a
    JOIN reusable_counts rc USING (candidate_article_id)
    WHERE a.screen_status = 'screen_pass_availability_unverified'
      AND rc.qualifying_treated_events = 1
),
ranked AS (
    SELECT a.*,
           coalesce(rc.qualifying_treated_events, 0) AS candidate_reuse_count,
           usr.unique_candidate_rank
    FROM assessed a
    LEFT JOIN reusable_counts rc USING (candidate_article_id)
    LEFT JOIN unique_screened_rank usr USING (cohort_no, candidate_article_id)
),
counts AS (
    SELECT t.cohort_no,
           count(r.candidate_article_id) AS exact_store_supplier_type_candidates,
           count(r.candidate_article_id) FILTER (WHERE r.screen_status = 'zero_signal_pre_sales') AS rejected_zero_signal,
           count(r.candidate_article_id) FILTER (WHERE r.screen_status = 'missing_comparable_pre_price') AS rejected_missing_price,
           count(r.candidate_article_id) FILTER (WHERE r.screen_status = 'pre_price_mismatch') AS rejected_price_mismatch,
           count(r.candidate_article_id) FILTER (WHERE r.screen_status = 'treated_pre_demand_missing') AS rejected_treated_demand_missing,
           count(r.candidate_article_id) FILTER (WHERE r.screen_status = 'pre_demand_mismatch') AS rejected_demand_mismatch,
           count(r.candidate_article_id) FILTER (WHERE r.screen_status = 'overlapping_or_repeated_markdown') AS rejected_markdown_confounded,
           count(r.candidate_article_id) FILTER (WHERE r.candidate_reuse_count > 1) AS rejected_reuse,
           count(r.candidate_article_id) FILTER (WHERE r.unique_candidate_rank = 1) AS unique_screened_candidates,
           0::bigint AS fully_qualified_pairs
    FROM treated t
    LEFT JOIN ranked r USING (cohort_no)
    GROUP BY t.cohort_no
),
screen_summary AS (
    SELECT c.*,
           u.same_store_type_wrong_supplier,
           u.same_store_supplier_wrong_type
    FROM counts c
    JOIN unmatched_counts u USING (cohort_no)
)
SELECT 'cohort_summary'::text AS record_type,
       s.cohort_no,
       t.action_date,
       t.treated_article_id,
       NULL::integer AS candidate_article_id,
       NULL::text AS candidate_plu,
       s.exact_store_supplier_type_candidates,
       s.same_store_type_wrong_supplier,
       s.same_store_supplier_wrong_type,
       s.rejected_zero_signal,
       s.rejected_missing_price,
       s.rejected_price_mismatch,
       s.rejected_treated_demand_missing,
       s.rejected_demand_mismatch,
       s.rejected_markdown_confounded,
       s.rejected_reuse,
       s.unique_screened_candidates,
       s.fully_qualified_pairs,
       'unverified_no_historical_daily_stockout_or_availability_evidence'::text AS availability_status,
       'unverified_batch23_per_store_day_source_coverage'::text AS source_coverage_status,
       NULL::text AS screen_status,
       NULL::numeric AS pre_gross_positive_units,
       NULL::numeric AS pre_signed_units,
       NULL::numeric AS pre_signed_revenue_rsd,
       NULL::numeric AS pre_weighted_unit_price_rsd,
       NULL::numeric AS price_distance_ratio,
       NULL::numeric AS pre_activity_ratio,
       NULL::bigint AS pre_receipt_count,
       NULL::bigint AS pre_active_days,
       NULL::bigint AS pre_return_line_count,
       NULL::bigint AS pre_unknown_cost_lines,
       NULL::bigint AS markdown_event_count_56d,
       NULL::bigint AS candidate_reuse_count,
       CASE WHEN s.unique_screened_candidates > 0
            THEN 'insufficient_control_population_due_to_unverified_availability_and_source_coverage'
            ELSE 'insufficient_control_population' END::text AS qualification_status
FROM screen_summary s
JOIN treated t USING (cohort_no)

UNION ALL

SELECT 'candidate_detail', r.cohort_no, r.action_date, r.treated_article_id,
       r.candidate_article_id, r.candidate_plu,
       s.exact_store_supplier_type_candidates,
       s.same_store_type_wrong_supplier,
       s.same_store_supplier_wrong_type,
       s.rejected_zero_signal,
       s.rejected_missing_price,
       s.rejected_price_mismatch,
       s.rejected_treated_demand_missing,
       s.rejected_demand_mismatch,
       s.rejected_markdown_confounded,
       s.rejected_reuse,
       s.unique_screened_candidates,
       s.fully_qualified_pairs,
       'unverified_no_historical_daily_stockout_or_availability_evidence',
       'unverified_batch23_per_store_day_source_coverage',
       CASE WHEN r.candidate_reuse_count > 1 THEN 'candidate_reused_across_treated_events'
            ELSE r.screen_status END,
       r.pre_gross_positive_units,
       r.pre_signed_units,
       r.pre_signed_revenue_rsd,
       r.pre_weighted_unit_price_rsd,
       r.price_distance_ratio,
       r.pre_activity_ratio,
       r.pre_receipt_count,
       r.pre_active_days,
       r.pre_return_line_count,
       r.pre_unknown_cost_lines,
       r.markdown_event_count_56d,
       r.candidate_reuse_count,
       CASE WHEN r.unique_candidate_rank = 1 THEN 'screened_candidate_not_qualified_availability_unknown'
            ELSE 'excluded_or_nonselected_candidate' END
FROM ranked r
JOIN screen_summary s USING (cohort_no)

UNION ALL

SELECT 'candidate_detail', t.cohort_no, t.action_date, t.treated_article_id,
       NULL::integer, NULL::text,
       s.exact_store_supplier_type_candidates,
       s.same_store_type_wrong_supplier,
       s.same_store_supplier_wrong_type,
       s.rejected_zero_signal,
       s.rejected_missing_price,
       s.rejected_price_mismatch,
       s.rejected_treated_demand_missing,
       s.rejected_demand_mismatch,
       s.rejected_markdown_confounded,
       s.rejected_reuse,
       s.unique_screened_candidates,
       s.fully_qualified_pairs,
       'unverified_no_historical_daily_stockout_or_availability_evidence',
       'unverified_batch23_per_store_day_source_coverage',
       'no_exact_store_supplier_type_stratum',
       NULL::numeric, NULL::numeric, NULL::numeric, NULL::numeric, NULL::numeric, NULL::numeric,
       NULL::bigint, NULL::bigint, NULL::bigint, NULL::bigint, NULL::bigint, NULL::bigint,
       'no_candidate_in_exact_store_supplier_type_stratum'
FROM treated t
JOIN screen_summary s USING (cohort_no)
WHERE s.exact_store_supplier_type_candidates = 0
ORDER BY cohort_no, record_type, candidate_article_id NULLS FIRST;
