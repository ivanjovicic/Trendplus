-- RQ602: read-only descriptive measurement for pre-registered markdown actions.
-- Replace the VALUES rows below with the frozen cohort and matching evidence.
-- Age bands are selection-time evidence; do not infer historical age from current stock.
-- Windows are half-open: [action_date - 28 days, action_date) and [action_date, action_date + 28 days).
-- Margin uses per-pair prodaja_stavke.nabavna_cena. Unknown cost is counted, never zero.
-- Current Artikli.Kolicina is a current snapshot, not historical end-of-window stock.
-- No writes, action records, or causal conclusions are produced.

-- RQ602_QUERY_START
WITH
pilot_actions(action_id, article_id, store_id, action_date, supplier_id, shoe_type_id, age_band) AS (
    VALUES
        ('fixture-action-1'::text, 1::integer, 1::integer, DATE '2026-08-01', 10::integer, 1::integer, '91-180'::text),
        ('fixture-action-2'::text, 3::integer, 1::integer, DATE '2026-08-01', 20::integer, 2::integer, '31-90'::text)
),
matched_comparisons(action_id, article_id, store_id, supplier_id, shoe_type_id, age_band) AS (
    VALUES
        ('fixture-action-1'::text, 2::integer, 1::integer, 10::integer, 1::integer, '91-180'::text),
        ('fixture-action-2'::text, 4::integer, 1::integer, 20::integer, 2::integer, '31-90'::text)
),
sales_horizon AS (
    SELECT MIN((datum_prodaje AT TIME ZONE 'Europe/Belgrade')::date) AS observed_from_local_date,
           MAX((datum_prodaje AT TIME ZONE 'Europe/Belgrade')::date) AS observed_through_local_date
    FROM prodaja_zaglavlje
),
cohort AS (
    SELECT a.action_id, 'treated'::text AS cohort_role, a.article_id, a.store_id, a.action_date,
           a.supplier_id, a.shoe_type_id, a.age_band, 'treated'::text AS match_status
    FROM pilot_actions a
    UNION ALL
    SELECT a.action_id, 'comparison'::text, c.article_id, c.store_id, a.action_date,
           c.supplier_id, c.shoe_type_id, c.age_band,
           CASE WHEN c.store_id = a.store_id
                     AND c.supplier_id = a.supplier_id
                     AND c.shoe_type_id = a.shoe_type_id
                     AND c.age_band = a.age_band
                THEN 'matched' ELSE 'unmatched' END
    FROM pilot_actions a
    JOIN matched_comparisons c ON c.action_id = a.action_id
),
validated_cohort AS (
    SELECT c.*,
           CASE WHEN c.cohort_role = 'treated' THEN EXISTS (
                    SELECT 1
                    FROM "DnevnikPromena" d
                    WHERE d."ArtikalId" = c.article_id
                      AND d."IDObjekat" = c.store_id
                      AND (d."Datum" AT TIME ZONE 'Europe/Belgrade')::date = c.action_date
                      AND LOWER(BTRIM(d."TipPromene")) IN ('nivelacija', 'nivelacija cena')
                      AND d."StaraProdajnaCena" IS NOT NULL
                      AND d."NovaProdajnaCena" < d."StaraProdajnaCena"
                ) ELSE FALSE END AS markdown_event_verified,
           CASE WHEN c.cohort_role <> 'comparison' THEN c.match_status
                WHEN c.match_status = 'unmatched' THEN 'unmatched'
                WHEN EXISTS (
                    SELECT 1
                    FROM "DnevnikPromena" d
                    WHERE d."ArtikalId" = c.article_id
                      AND d."IDObjekat" = c.store_id
                      AND (d."Datum" AT TIME ZONE 'Europe/Belgrade')::date >= c.action_date - 28
                      AND (d."Datum" AT TIME ZONE 'Europe/Belgrade')::date <  c.action_date + 28
                      AND LOWER(BTRIM(d."TipPromene")) IN ('nivelacija', 'nivelacija cena')
                      AND d."StaraProdajnaCena" IS NOT NULL
                      AND d."NovaProdajnaCena" < d."StaraProdajnaCena"
                ) THEN 'unmatched_markdown_in_window'
                ELSE 'matched' END AS validated_match_status
    FROM cohort c
),
eligible_sales AS (
    SELECT c.action_id, c.cohort_role, c.validated_match_status AS match_status,
           c.markdown_event_verified,
           c.article_id, c.store_id, c.action_date,
           s.id AS sale_line_id, s.kolicina, s.cena, s.nabavna_cena,
           (h.datum_prodaje AT TIME ZONE 'Europe/Belgrade')::date AS sale_date
    FROM validated_cohort c
    LEFT JOIN prodaja_zaglavlje h
      ON h.id_objekat = c.store_id
     AND (h.datum_prodaje AT TIME ZONE 'Europe/Belgrade')::date >= c.action_date - 28
     AND (h.datum_prodaje AT TIME ZONE 'Europe/Belgrade')::date <  c.action_date + 28
     AND UPPER(BTRIM(COALESCE(h.broj_racuna, ''))) NOT IN ('DUG', 'KOREKCIJA')
    LEFT JOIN prodaja_stavke s
      ON s.id_prodaja = h.id
     AND s.id_artikal = c.article_id
),
period_metrics AS (
    SELECT action_id, cohort_role, match_status, markdown_event_verified,
           article_id, store_id, action_date,
           COUNT(sale_line_id) FILTER (WHERE sale_date >= action_date - 28
                                         AND sale_date < action_date) AS pre_sale_line_count,
           COALESCE(SUM(kolicina) FILTER (WHERE sale_date >= action_date - 28
                                            AND sale_date < action_date), 0)::bigint AS pre_pairs_sold,
           COALESCE(SUM(kolicina * cena) FILTER (WHERE sale_date >= action_date - 28
                                                   AND sale_date < action_date), 0)::numeric AS pre_revenue_rsd,
           COUNT(sale_line_id) FILTER (WHERE sale_date >= action_date
                                         AND sale_date < action_date + 28) AS post_sale_line_count,
           COALESCE(SUM(kolicina) FILTER (WHERE sale_date >= action_date
                                            AND sale_date < action_date + 28), 0)::bigint AS post_pairs_sold,
           COALESCE(SUM(kolicina * cena) FILTER (WHERE sale_date >= action_date
                                                   AND sale_date < action_date + 28), 0)::numeric AS post_revenue_rsd,
           COUNT(sale_line_id) FILTER (WHERE sale_date >= action_date - 28
                                         AND sale_date < action_date AND nabavna_cena IS NULL
                                         AND kolicina <> 0) AS pre_unknown_cost_line_count,
           COUNT(sale_line_id) FILTER (WHERE sale_date >= action_date
                                         AND sale_date < action_date + 28
                                         AND nabavna_cena IS NULL AND kolicina <> 0) AS post_unknown_cost_line_count,
           SUM((cena - nabavna_cena) * kolicina) FILTER (WHERE sale_date >= action_date - 28
                                                           AND sale_date < action_date
                                                           AND nabavna_cena IS NOT NULL) AS pre_known_cost_margin_rsd,
           SUM((cena - nabavna_cena) * kolicina) FILTER (WHERE sale_date >= action_date
                                                           AND sale_date < action_date + 28
                                                           AND nabavna_cena IS NOT NULL) AS post_known_cost_margin_rsd
    FROM eligible_sales
    GROUP BY action_id, cohort_role, match_status, markdown_event_verified,
             article_id, store_id, action_date
),
member_metrics AS (
    SELECT p.*,
           stock."Kolicina" AS current_stock_snapshot_pairs,
           NOW() AS current_stock_snapshot_at_utc,
           horizon.observed_from_local_date,
           horizon.observed_through_local_date,
           horizon.observed_from_local_date <= p.action_date - 28 AS pre_window_complete,
           horizon.observed_through_local_date >= p.action_date + 27 AS post_window_complete,
           CASE WHEN pre_sale_line_count = 0 OR pre_unknown_cost_line_count > 0 THEN NULL
                ELSE pre_known_cost_margin_rsd END AS pre_realized_gross_margin_rsd,
           CASE WHEN post_sale_line_count = 0 OR post_unknown_cost_line_count > 0 THEN NULL
                ELSE post_known_cost_margin_rsd END AS post_realized_gross_margin_rsd
    FROM period_metrics p
    LEFT JOIN "Artikli" stock ON stock."Id" = p.article_id AND stock."IDObjekat" = p.store_id
    CROSS JOIN sales_horizon horizon
),
reported AS (
    SELECT action_id, cohort_role, match_status,
           COUNT(*) AS member_count,
           BOOL_AND(markdown_event_verified) AS markdown_event_verified,
           COUNT(*) FILTER (WHERE cohort_role = 'comparison' AND match_status LIKE 'unmatched%') AS unmatched_comparison_count,
           SUM(pre_pairs_sold) AS pre_pairs_sold,
           SUM(post_pairs_sold) AS post_pairs_sold,
           SUM(pre_revenue_rsd) AS pre_revenue_rsd,
           SUM(post_revenue_rsd) AS post_revenue_rsd,
           SUM(pre_sale_line_count) AS pre_sale_line_count,
           SUM(post_sale_line_count) AS post_sale_line_count,
           SUM(pre_unknown_cost_line_count) AS pre_unknown_cost_line_count,
           SUM(post_unknown_cost_line_count) AS post_unknown_cost_line_count,
           SUM(pre_sale_line_count - pre_unknown_cost_line_count) AS pre_known_cost_line_count,
           SUM(post_sale_line_count - post_unknown_cost_line_count) AS post_known_cost_line_count,
           SUM(pre_known_cost_margin_rsd) AS pre_known_cost_margin_rsd,
           SUM(post_known_cost_margin_rsd) AS post_known_cost_margin_rsd,
           CASE WHEN SUM(pre_sale_line_count) = 0 OR SUM(pre_unknown_cost_line_count) > 0 THEN NULL
                ELSE SUM(pre_known_cost_margin_rsd) END AS pre_realized_gross_margin_rsd,
           CASE WHEN SUM(post_sale_line_count) = 0 OR SUM(post_unknown_cost_line_count) > 0 THEN NULL
                ELSE SUM(post_known_cost_margin_rsd) END AS post_realized_gross_margin_rsd,
           SUM(current_stock_snapshot_pairs) AS current_stock_snapshot_pairs,
           MAX(current_stock_snapshot_at_utc) AS current_stock_snapshot_at_utc,
           BOOL_AND(pre_window_complete) AS pre_window_complete,
           BOOL_AND(post_window_complete) AS post_window_complete,
           MIN(observed_from_local_date) AS observed_from_local_date,
           MAX(observed_through_local_date) AS observed_through_local_date,
           GROUPING(action_id) = 1 AS is_total
    FROM member_metrics
    GROUP BY GROUPING SETS ((action_id, cohort_role, match_status), (cohort_role, match_status))
)
SELECT action_id, cohort_role, match_status, is_total, member_count, markdown_event_verified,
       unmatched_comparison_count,
       pre_pairs_sold, post_pairs_sold, pre_revenue_rsd, post_revenue_rsd,
       pre_sale_line_count, post_sale_line_count,
       pre_known_cost_line_count, post_known_cost_line_count,
       pre_unknown_cost_line_count, post_unknown_cost_line_count,
       pre_known_cost_margin_rsd, post_known_cost_margin_rsd,
       pre_realized_gross_margin_rsd, post_realized_gross_margin_rsd,
       current_stock_snapshot_pairs, current_stock_snapshot_at_utc,
       pre_window_complete, post_window_complete,
       observed_from_local_date, observed_through_local_date
FROM reported
ORDER BY is_total, action_id NULLS LAST, cohort_role, match_status;
-- RQ602_QUERY_END
