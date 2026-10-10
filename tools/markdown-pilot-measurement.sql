-- RQ602: read-only descriptive measurement for pre-registered markdown actions.
-- Replace the VALUES rows with a frozen, owner-approved cohort before a pilot.
--
-- The query deliberately keeps two different notions separate:
--   * a per-store source horizon, which is diagnostic only; and
--   * a complete daily source manifest, which is not present in the current
--     operational schema. A horizon alone therefore never certifies a window.
--
-- Windows are half-open in Europe/Belgrade local dates:
--   pre  [action_date - 28 days, action_date)
--   post [action_date, action_date + 28 days)
-- DUG/KOREKCIJA are excluded, signed quantities (including returns) remain.
-- Current Artikli.Kolicina is labelled as a current snapshot only; it is not
-- historical end-of-window stock. No write, action record or causal claim is
-- produced.

-- RQ602_QUERY_START
WITH
measurement_batch(batch_id) AS (
    VALUES (23::bigint)
),
completed_batches AS (
    SELECT b."Id" AS batch_id
    FROM "DataImportBatches" b
    JOIN measurement_batch mb ON mb.batch_id = b."Id"
    WHERE LOWER(BTRIM(b."Status")) = 'completed'
),
pilot_actions(action_id, article_id, store_id, action_date, supplier_id, shoe_type_id, age_band, source_batch_id) AS (
    VALUES
        ('fixture-action-1'::text, 1::integer, 1::integer, DATE '2026-08-01', 10::integer, 1::integer, '91-180'::text, 23::bigint),
        ('fixture-action-2'::text, 3::integer, 1::integer, DATE '2026-08-01', 20::integer, 2::integer, '31-90'::text, 23::bigint),
        ('fixture-action-3'::text, 5::integer, 2::integer, DATE '2026-08-01', 30::integer, 3::integer, 'unknown'::text, 23::bigint)
),
matched_comparisons(action_id, article_id, store_id, supplier_id, shoe_type_id, age_band, source_batch_id) AS (
    VALUES
        ('fixture-action-1'::text, 2::integer, 1::integer, 10::integer, 1::integer, '91-180'::text, 23::bigint),
        ('fixture-action-2'::text, 4::integer, 1::integer, 20::integer, 2::integer, '31-90'::text, 23::bigint),
        ('fixture-action-3'::text, 6::integer, 2::integer, 30::integer, 3::integer, 'unknown'::text, 23::bigint)
),
source_horizon AS (
    SELECT h.id_objekat AS store_id,
           h.source_batch_id,
           MIN((h.datum_prodaje AT TIME ZONE 'Europe/Belgrade')::date) AS observed_from_local_date,
           MAX((h.datum_prodaje AT TIME ZONE 'Europe/Belgrade')::date) AS observed_through_local_date,
           COUNT(DISTINCT (h.datum_prodaje AT TIME ZONE 'Europe/Belgrade')::date) AS observed_sales_days
    FROM prodaja_zaglavlje h
    JOIN completed_batches b ON b.batch_id = h.source_batch_id
    GROUP BY h.id_objekat, h.source_batch_id
),
cohort AS (
    SELECT a.action_id,
           'treated'::text AS cohort_role,
           a.article_id,
           a.store_id,
           a.action_date,
           a.supplier_id,
           a.shoe_type_id,
           a.age_band,
           a.source_batch_id,
           'treated'::text AS match_status
    FROM pilot_actions a
    UNION ALL
    SELECT a.action_id,
           'comparison'::text,
           c.article_id,
           c.store_id,
           a.action_date,
           c.supplier_id,
           c.shoe_type_id,
           c.age_band,
           c.source_batch_id,
           CASE WHEN c.store_id = a.store_id
                     AND c.supplier_id = a.supplier_id
                     AND c.shoe_type_id = a.shoe_type_id
                     AND c.age_band = a.age_band
                THEN 'matched' ELSE 'unmatched' END
    FROM pilot_actions a
    JOIN matched_comparisons c ON c.action_id = a.action_id
),
markdown_window AS (
    SELECT c.action_id,
           c.cohort_role,
           c.article_id,
           c.store_id,
           c.action_date,
           COUNT(d."Id") FILTER (
               WHERE (d."Datum" AT TIME ZONE 'Europe/Belgrade')::date >= c.action_date - 28
                 AND (d."Datum" AT TIME ZONE 'Europe/Belgrade')::date < c.action_date + 28
           )::integer AS markdown_event_count_in_window,
           COUNT(d."Id") FILTER (
               WHERE (d."Datum" AT TIME ZONE 'Europe/Belgrade')::date = c.action_date
           )::integer AS markdown_event_count_on_action_date
    FROM cohort c
    LEFT JOIN "DnevnikPromena" d
      ON d."ArtikalId" = c.article_id
     AND d."IDObjekat" = c.store_id
     AND d."SourceBatchId" = c.source_batch_id
     AND LOWER(BTRIM(d."TipPromene")) IN ('nivelacija', 'nivelacija cena')
     AND d."StaraProdajnaCena" IS NOT NULL
     AND d."NovaProdajnaCena" < d."StaraProdajnaCena"
     AND (d."Datum" AT TIME ZONE 'Europe/Belgrade')::date >= c.action_date - 28
     AND (d."Datum" AT TIME ZONE 'Europe/Belgrade')::date < c.action_date + 28
    GROUP BY c.action_id, c.cohort_role, c.article_id, c.store_id, c.action_date
),
validated_cohort AS (
    SELECT c.*,
           COALESCE(m.markdown_event_count_in_window, 0) AS markdown_event_count_in_window,
           COALESCE(m.markdown_event_count_on_action_date, 0) AS markdown_event_count_on_action_date,
           CASE WHEN c.cohort_role = 'treated'
                     AND COALESCE(m.markdown_event_count_on_action_date, 0) > 0
                THEN TRUE ELSE FALSE END AS markdown_event_verified,
           CASE WHEN c.cohort_role = 'treated'
                     AND COALESCE(m.markdown_event_count_in_window, 0) > 1
                THEN 'repeated_markdown_in_window'
                WHEN c.cohort_role <> 'comparison' THEN c.match_status
                WHEN c.match_status = 'unmatched' THEN 'unmatched'
                WHEN COALESCE(m.markdown_event_count_in_window, 0) > 0
                THEN 'unmatched_markdown_in_window'
                ELSE 'matched' END AS validated_match_status
    FROM cohort c
    LEFT JOIN markdown_window m
      ON m.action_id = c.action_id
     AND m.cohort_role = c.cohort_role
     AND m.article_id = c.article_id
     AND m.store_id = c.store_id
     AND m.action_date = c.action_date
),
eligible_sales AS (
    SELECT c.action_id,
           c.cohort_role,
           c.validated_match_status AS match_status,
           c.markdown_event_verified,
           c.markdown_event_count_in_window,
           c.article_id,
           c.store_id,
           c.action_date,
           c.source_batch_id,
           s.id AS sale_line_id,
           s.kolicina,
           s.cena,
           s.nabavna_cena,
           -- RQ601 provenance is independent from sale-dimension attribution_basis.
           -- to_jsonb preserves read-only compatibility with pre-migration batch-23
           -- databases: a missing column yields unknown provenance, never confirmed.
           CASE
               WHEN s.nabavna_cena IS NULL OR s.nabavna_cena <= 0 THEN 'unknown_cost'
               WHEN LOWER(BTRIM(COALESCE(to_jsonb(s)->>'purchase_cost_provenance', '')))
                    IN ('source_sale_line', 'confirmed_calculation')
                   THEN 'confirmed'
               WHEN LOWER(BTRIM(COALESCE(to_jsonb(s)->>'purchase_cost_provenance', '')))
                    IN ('master_nabavnacena_din_backfill', 'master_nabavnacena_backfill')
                   THEN 'estimated'
               ELSE 'unknown_provenance'
           END AS cost_provenance_status,
           (h.datum_prodaje AT TIME ZONE 'Europe/Belgrade')::date AS sale_date
    FROM validated_cohort c
    LEFT JOIN prodaja_zaglavlje h
      ON h.id_objekat = c.store_id
     AND h.source_batch_id = c.source_batch_id
     AND (h.datum_prodaje AT TIME ZONE 'Europe/Belgrade')::date >= c.action_date - 28
     AND (h.datum_prodaje AT TIME ZONE 'Europe/Belgrade')::date < c.action_date + 28
     AND UPPER(BTRIM(COALESCE(h.broj_racuna, ''))) NOT IN ('DUG', 'KOREKCIJA')
    LEFT JOIN prodaja_stavke s
      ON s.id_prodaja = h.id
     AND s.id_artikal = c.article_id
     AND s.source_batch_id = c.source_batch_id
),
period_metrics AS (
    SELECT action_id,
           cohort_role,
           match_status,
           markdown_event_verified,
           markdown_event_count_in_window,
           article_id,
           store_id,
           action_date,
           source_batch_id,
           COUNT(sale_line_id) FILTER (WHERE sale_date >= action_date - 28 AND sale_date < action_date) AS pre_sale_line_count,
           COALESCE(SUM(kolicina) FILTER (WHERE sale_date >= action_date - 28 AND sale_date < action_date), 0)::bigint AS pre_pairs_sold,
           COALESCE(SUM(kolicina * cena) FILTER (WHERE sale_date >= action_date - 28 AND sale_date < action_date), 0)::numeric AS pre_revenue_rsd,
           COUNT(sale_line_id) FILTER (WHERE sale_date >= action_date AND sale_date < action_date + 28) AS post_sale_line_count,
           COALESCE(SUM(kolicina) FILTER (WHERE sale_date >= action_date AND sale_date < action_date + 28), 0)::bigint AS post_pairs_sold,
           COALESCE(SUM(kolicina * cena) FILTER (WHERE sale_date >= action_date AND sale_date < action_date + 28), 0)::numeric AS post_revenue_rsd,
           COUNT(sale_line_id) FILTER (WHERE sale_date >= action_date - 28 AND sale_date < action_date AND cost_provenance_status = 'unknown_cost' AND kolicina <> 0) AS pre_unknown_cost_line_count,
           COUNT(sale_line_id) FILTER (WHERE sale_date >= action_date AND sale_date < action_date + 28 AND cost_provenance_status = 'unknown_cost' AND kolicina <> 0) AS post_unknown_cost_line_count,
           COUNT(sale_line_id) FILTER (WHERE sale_date >= action_date - 28 AND sale_date < action_date AND cost_provenance_status = 'estimated' AND kolicina <> 0) AS pre_estimated_cost_line_count,
           COUNT(sale_line_id) FILTER (WHERE sale_date >= action_date AND sale_date < action_date + 28 AND cost_provenance_status = 'estimated' AND kolicina <> 0) AS post_estimated_cost_line_count,
           COUNT(sale_line_id) FILTER (WHERE sale_date >= action_date - 28 AND sale_date < action_date AND cost_provenance_status = 'unknown_provenance' AND kolicina <> 0) AS pre_unproven_cost_line_count,
           COUNT(sale_line_id) FILTER (WHERE sale_date >= action_date AND sale_date < action_date + 28 AND cost_provenance_status = 'unknown_provenance' AND kolicina <> 0) AS post_unproven_cost_line_count,
           SUM((cena - nabavna_cena) * kolicina) FILTER (WHERE sale_date >= action_date - 28 AND sale_date < action_date AND nabavna_cena > 0) AS pre_known_cost_margin_rsd,
           SUM((cena - nabavna_cena) * kolicina) FILTER (WHERE sale_date >= action_date AND sale_date < action_date + 28 AND nabavna_cena > 0) AS post_known_cost_margin_rsd,
           SUM((cena - nabavna_cena) * kolicina) FILTER (WHERE sale_date >= action_date - 28 AND sale_date < action_date AND cost_provenance_status = 'confirmed') AS pre_confirmed_cost_margin_rsd,
           SUM((cena - nabavna_cena) * kolicina) FILTER (WHERE sale_date >= action_date AND sale_date < action_date + 28 AND cost_provenance_status = 'confirmed') AS post_confirmed_cost_margin_rsd,
           SUM((cena - nabavna_cena) * kolicina) FILTER (WHERE sale_date >= action_date - 28 AND sale_date < action_date AND cost_provenance_status = 'estimated') AS pre_estimated_cost_margin_rsd,
           SUM((cena - nabavna_cena) * kolicina) FILTER (WHERE sale_date >= action_date AND sale_date < action_date + 28 AND cost_provenance_status = 'estimated') AS post_estimated_cost_margin_rsd,
           SUM((cena - nabavna_cena) * kolicina) FILTER (WHERE sale_date >= action_date - 28 AND sale_date < action_date AND cost_provenance_status = 'unknown_provenance') AS pre_unproven_cost_margin_rsd,
           SUM((cena - nabavna_cena) * kolicina) FILTER (WHERE sale_date >= action_date AND sale_date < action_date + 28 AND cost_provenance_status = 'unknown_provenance') AS post_unproven_cost_margin_rsd
    FROM eligible_sales
    GROUP BY action_id, cohort_role, match_status, markdown_event_verified, markdown_event_count_in_window,
             article_id, store_id, action_date, source_batch_id
),
member_metrics AS (
    SELECT p.*,
           stock."Kolicina" AS current_stock_snapshot_pairs,
           NOW() AS current_stock_snapshot_at_utc,
           horizon.observed_from_local_date,
           horizon.observed_through_local_date,
           horizon.observed_sales_days,
           CASE WHEN horizon.store_id IS NULL THEN 'unverified_store_horizon_missing'
                ELSE 'unverified_source_coverage_not_proven' END AS pre_window_status,
           CASE WHEN horizon.store_id IS NULL THEN 'unverified_store_horizon_missing'
                ELSE 'unverified_source_coverage_not_proven' END AS post_window_status,
           FALSE AS pre_window_complete,
           FALSE AS post_window_complete,
           CASE WHEN p.pre_sale_line_count > 0 THEN 'observed_sales'
                ELSE 'unverified_no_source_evidence' END AS pre_sales_status,
           CASE WHEN p.post_sale_line_count > 0 THEN 'observed_sales'
                ELSE 'unverified_no_source_evidence' END AS post_sales_status,
           CASE WHEN p.pre_sale_line_count = 0 THEN 'no_sales'
                WHEN p.pre_unknown_cost_line_count > 0 THEN 'insufficient_unknown_cost'
                WHEN p.pre_estimated_cost_line_count > 0 THEN 'estimated_cost'
                WHEN p.pre_unproven_cost_line_count > 0 THEN 'unknown_provenance'
                ELSE 'certified_confirmed_cost' END AS pre_margin_trust_status,
           CASE WHEN p.post_sale_line_count = 0 THEN 'no_sales'
                WHEN p.post_unknown_cost_line_count > 0 THEN 'insufficient_unknown_cost'
                WHEN p.post_estimated_cost_line_count > 0 THEN 'estimated_cost'
                WHEN p.post_unproven_cost_line_count > 0 THEN 'unknown_provenance'
                ELSE 'certified_confirmed_cost' END AS post_margin_trust_status,
           CASE WHEN p.pre_sale_line_count = 0
                     OR p.pre_unknown_cost_line_count > 0
                     OR p.pre_estimated_cost_line_count > 0
                     OR p.pre_unproven_cost_line_count > 0
                THEN NULL ELSE p.pre_confirmed_cost_margin_rsd END AS pre_confirmed_margin_rsd,
           CASE WHEN p.post_sale_line_count = 0
                     OR p.post_unknown_cost_line_count > 0
                     OR p.post_estimated_cost_line_count > 0
                     OR p.post_unproven_cost_line_count > 0
                THEN NULL ELSE p.post_confirmed_cost_margin_rsd END AS post_confirmed_margin_rsd
    FROM period_metrics p
    LEFT JOIN "Artikli" stock
      ON stock."Id" = p.article_id
     AND stock."IDObjekat" = p.store_id
    LEFT JOIN source_horizon horizon
      ON horizon.store_id = p.store_id
     AND horizon.source_batch_id = p.source_batch_id
),
reported AS (
    SELECT action_id,
           cohort_role,
           match_status,
           COUNT(*) AS member_count,
           BOOL_AND(markdown_event_verified) AS markdown_event_verified,
           MAX(markdown_event_count_in_window) AS markdown_event_count_in_window,
           COUNT(*) FILTER (WHERE cohort_role = 'comparison' AND match_status LIKE 'unmatched%') AS unmatched_comparison_count,
           SUM(pre_pairs_sold) AS pre_pairs_sold,
           SUM(post_pairs_sold) AS post_pairs_sold,
           SUM(pre_revenue_rsd) AS pre_revenue_rsd,
           SUM(post_revenue_rsd) AS post_revenue_rsd,
           SUM(pre_sale_line_count) AS pre_sale_line_count,
           SUM(post_sale_line_count) AS post_sale_line_count,
           SUM(pre_unknown_cost_line_count) AS pre_unknown_cost_line_count,
           SUM(post_unknown_cost_line_count) AS post_unknown_cost_line_count,
           SUM(pre_estimated_cost_line_count) AS pre_estimated_cost_line_count,
           SUM(post_estimated_cost_line_count) AS post_estimated_cost_line_count,
           SUM(pre_unproven_cost_line_count) AS pre_unproven_cost_line_count,
           SUM(post_unproven_cost_line_count) AS post_unproven_cost_line_count,
           SUM(pre_sale_line_count - pre_unknown_cost_line_count - pre_estimated_cost_line_count - pre_unproven_cost_line_count) AS pre_confirmed_cost_line_count,
           SUM(post_sale_line_count - post_unknown_cost_line_count - post_estimated_cost_line_count - post_unproven_cost_line_count) AS post_confirmed_cost_line_count,
           SUM(pre_known_cost_margin_rsd) AS pre_known_cost_margin_rsd,
           SUM(post_known_cost_margin_rsd) AS post_known_cost_margin_rsd,
           SUM(pre_confirmed_cost_margin_rsd) AS pre_confirmed_cost_margin_rsd,
           SUM(post_confirmed_cost_margin_rsd) AS post_confirmed_cost_margin_rsd,
           SUM(pre_estimated_cost_margin_rsd) AS pre_estimated_cost_margin_rsd,
           SUM(post_estimated_cost_margin_rsd) AS post_estimated_cost_margin_rsd,
           SUM(pre_unproven_cost_margin_rsd) AS pre_unproven_cost_margin_rsd,
           SUM(post_unproven_cost_margin_rsd) AS post_unproven_cost_margin_rsd,
           CASE WHEN SUM(pre_sale_line_count) = 0
                     OR SUM(pre_unknown_cost_line_count) > 0
                     OR SUM(pre_estimated_cost_line_count) > 0
                     OR SUM(pre_unproven_cost_line_count) > 0
                     OR NOT BOOL_AND(pre_window_complete)
                THEN NULL ELSE SUM(pre_confirmed_cost_margin_rsd) END AS pre_realized_gross_margin_rsd,
           CASE WHEN SUM(post_sale_line_count) = 0
                     OR SUM(post_unknown_cost_line_count) > 0
                     OR SUM(post_estimated_cost_line_count) > 0
                     OR SUM(post_unproven_cost_line_count) > 0
                     OR NOT BOOL_AND(post_window_complete)
                THEN NULL ELSE SUM(post_confirmed_cost_margin_rsd) END AS post_realized_gross_margin_rsd,
           SUM(current_stock_snapshot_pairs) AS current_stock_snapshot_pairs,
           MAX(current_stock_snapshot_at_utc) AS current_stock_snapshot_at_utc,
           BOOL_AND(pre_window_complete) AS pre_window_complete,
           BOOL_AND(post_window_complete) AS post_window_complete,
           MIN(pre_window_status) AS pre_window_status,
           MIN(post_window_status) AS post_window_status,
           MIN(pre_sales_status) AS pre_sales_status,
           MIN(post_sales_status) AS post_sales_status,
           MIN(pre_margin_trust_status) AS pre_margin_trust_status,
           MIN(post_margin_trust_status) AS post_margin_trust_status,
           MIN(observed_from_local_date) AS observed_from_local_date,
           MAX(observed_through_local_date) AS observed_through_local_date,
           MAX(observed_sales_days) AS observed_sales_days,
           GROUPING(action_id) = 1 AS is_total
    FROM member_metrics
    GROUP BY GROUPING SETS ((action_id, cohort_role, match_status), (cohort_role, match_status))
)
SELECT action_id,
       cohort_role,
       match_status,
       is_total,
       member_count,
       markdown_event_verified,
       markdown_event_count_in_window,
       unmatched_comparison_count,
       pre_pairs_sold,
       post_pairs_sold,
       pre_revenue_rsd,
       post_revenue_rsd,
       pre_sale_line_count,
       post_sale_line_count,
       pre_confirmed_cost_line_count,
       post_confirmed_cost_line_count,
       pre_unknown_cost_line_count,
       post_unknown_cost_line_count,
       pre_estimated_cost_line_count,
       post_estimated_cost_line_count,
       pre_unproven_cost_line_count,
       post_unproven_cost_line_count,
       pre_known_cost_margin_rsd,
       post_known_cost_margin_rsd,
       pre_confirmed_cost_margin_rsd,
       post_confirmed_cost_margin_rsd,
       pre_estimated_cost_margin_rsd,
       post_estimated_cost_margin_rsd,
       pre_unproven_cost_margin_rsd,
       post_unproven_cost_margin_rsd,
       pre_realized_gross_margin_rsd,
       post_realized_gross_margin_rsd,
       current_stock_snapshot_pairs,
       current_stock_snapshot_at_utc,
       pre_window_complete,
       post_window_complete,
       pre_window_status,
       post_window_status,
       pre_sales_status,
       post_sales_status,
       pre_margin_trust_status,
       post_margin_trust_status,
       observed_from_local_date,
       observed_through_local_date,
       observed_sales_days
FROM reported
ORDER BY is_total, action_id NULLS LAST, cohort_role, match_status;
-- RQ602_QUERY_END
