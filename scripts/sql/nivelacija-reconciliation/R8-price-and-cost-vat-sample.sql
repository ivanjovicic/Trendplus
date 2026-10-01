-- RQ547 / R8: compare a bounded sale-line sample with current retail and cost × 1.2.
BEGIN TRANSACTION READ ONLY;

WITH sample AS (
    SELECT ps.id_prodaja,
           ps.id_artikal,
           ps.cena::numeric AS line_price,
           a."ProdajnaCena"::numeric AS current_retail_price,
           ps.nabavna_cena::numeric AS line_cost,
           a."NabavnaCena"::numeric AS current_cost
    FROM prodaja_stavke ps
    JOIN prodaja_zaglavlje pz ON pz.id = ps.id_prodaja
    JOIN "Artikli" a ON a."Id" = ps.id_artikal
    WHERE pz.datum_prodaje >= CURRENT_DATE - INTERVAL '90 days'
    ORDER BY pz.datum_prodaje DESC, ps.id_prodaja, ps.id_artikal
    LIMIT 200
), summary AS (
    SELECT COUNT(*)::bigint AS sample_rows,
           COUNT(*) FILTER (WHERE line_price IS NULL OR current_retail_price IS NULL
                             OR (line_cost IS NULL AND current_cost IS NULL))::bigint AS incomplete_rows,
           COUNT(*) FILTER (WHERE line_price IS NOT NULL AND current_retail_price IS NOT NULL
                             AND ABS(line_price - current_retail_price) > 0.01)::bigint AS line_vs_current_price_mismatches,
           COUNT(*) FILTER (WHERE line_price IS NOT NULL AND line_cost IS NOT NULL
                             AND ABS(line_price - line_cost * 1.2) > 0.01)::bigint AS line_cost_times_1_2_mismatches,
           COUNT(*) FILTER (WHERE line_price IS NOT NULL AND current_cost IS NOT NULL
                             AND ABS(line_price - current_cost * 1.2) > 0.01)::bigint AS article_cost_times_1_2_mismatches,
           COUNT(*) FILTER (WHERE line_cost IS NOT NULL)::bigint AS line_cost_rows,
           COUNT(*) FILTER (WHERE current_cost IS NOT NULL)::bigint AS current_cost_rows
    FROM sample
)
SELECT 'NV-P1-R8'::text AS check_id,
       CASE WHEN sample_rows = 0 OR incomplete_rows > 0 OR line_vs_current_price_mismatches > 0
                  OR line_cost_times_1_2_mismatches > 0 OR article_cost_times_1_2_mismatches > 0 THEN 'WARN'
            ELSE 'PASS' END::text AS verdict,
       format('sample_rows=%s; incomplete=%s; line_vs_current_price_mismatches=%s; line_cost_x_1_2_mismatches=%s; article_cost_x_1_2_mismatches=%s; line_cost_rows=%s; current_cost_rows=%s',
              sample_rows, incomplete_rows, line_vs_current_price_mismatches, line_cost_times_1_2_mismatches,
              article_cost_times_1_2_mismatches, line_cost_rows, current_cost_rows) AS observed,
       'bounded sample reports agreement or explicit variance/missing evidence for line price, current retail price, line cost and article cost'::text AS expected,
       'RQ547'::text AS owner,
       'A 90-day sample of at most 200 rows is descriptive VAT-basis evidence, not a tax or accounting conclusion.'::text AS detail
FROM summary;

ROLLBACK;
