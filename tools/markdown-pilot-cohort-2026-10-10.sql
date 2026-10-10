-- RQ602 frozen historical cohort, batch 23, read-only manifest.
--
-- This is an evidence manifest, not a data migration. It intentionally keeps
-- the source event identity separate from the article identity. The 20
-- treated rows are the deterministic action events; each control is a distinct
-- article in the same store/supplier/shoe-type stratum.
--
-- Selection contract:
--   * source batch 23, completed at extraction time;
--   * one canonical downward markdown event per article/store/local date;
--   * exactly one downward markdown in [action_date - 28, action_date + 28);
--   * same-store/supplier/shoe-type control, deterministic article-id order;
--   * controls have no markdown or price-change event in that 56-day window;
--   * repeated markdowns, unmatched controls, missing source horizons and
--     unknown age bands remain explicit evidence, never silently promoted;
--   * dates are Europe/Belgrade local dates and all measurement windows are
--     half-open: pre [action_date - 28, action_date), post [action_date,
--     action_date + 28).
--
-- The current schema has no per-store daily source-coverage manifest. The
-- companion measurement query therefore reports the store horizon for
-- diagnostics but keeps every window unverified unless such evidence exists.

WITH frozen_cohort(
    cohort_no, treatment_event_id, treatment_source_row_id, action_date,
    treatment_article_id, treatment_store_id, treatment_supplier_id,
    treatment_shoe_type_id, price_before_rsd, price_after_rsd,
    control_article_id, control_store_id, control_supplier_id,
    control_shoe_type_id, control_plu, control_name, selection_status
) AS (
    VALUES
        (1, 12256::bigint, 1781792073::bigint, DATE '2026-06-18', 21609, 2082886995, 1447386007, -424518832, 4990::numeric, 3990::numeric, 14071, 2082886995, 1447386007, -424518832, '46', '180', 'matched_same_stratum'),
        (2, 12257::bigint, 1781792073::bigint, DATE '2026-06-18', 21621, 2082886995, 1631301963, -424518832, 8990::numeric, 6990::numeric, 20292, 2082886995, 1631301963, -424518832, '1922', '22WN0101', 'matched_same_stratum'),
        (3, 12258::bigint, 1781792073::bigint, DATE '2026-06-18', 21647, 2082886995, 894901603, 1846163462, 8800::numeric, 6390::numeric, 14182, 2082886995, 894901603, -424518832, '123', 'F109', 'matched_supplier_only'),
        (4, 12259::bigint, 1781792073::bigint, DATE '2026-06-18', 21650, 2082886995, 894901603, 1846163462, 5300::numeric, 4500::numeric, 12762, 2082886995, 894901603, 1846163462, '132', 'B-100', 'matched_same_stratum'),
        (5, 12260::bigint, 1781792073::bigint, DATE '2026-06-18', 21661, 2082886995, 894901603, 1846163462, 7900::numeric, 6200::numeric, 12763, 2082886995, 894901603, 1846163462, '884', 'B-200', 'matched_same_stratum'),
        (6, 12231::bigint, 743035353::bigint, DATE '2026-05-18', 21164, 2082886995, 1496651500, -2004188974, 7990::numeric, 6200::numeric, 12287, 2082886995, 1496651500, -2004188974, '1627', '43759-42', 'matched_same_stratum'),
        (7, 12232::bigint, 743035353::bigint, DATE '2026-05-18', 21232, 2082886995, 1496651500, -424518832, 7990::numeric, 6200::numeric, 12251, 2082886995, 1496651500, -424518832, '655', '41356-80', 'matched_same_stratum'),
        (8, 12233::bigint, 743035353::bigint, DATE '2026-05-18', 21245, 2082886995, 894901603, 1846163462, 5200::numeric, 4200::numeric, 12764, 2082886995, 894901603, 1846163462, '134', 'B-300', 'matched_same_stratum'),
        (9, 12237::bigint, 743035353::bigint, DATE '2026-05-18', 21257, 2082886995, 894901603, 1846163462, 5200::numeric, 4200::numeric, 13397, 2082886995, 894901603, 1846163462, '0', 'B-210', 'matched_same_stratum'),
        (10, 12238::bigint, 743035353::bigint, DATE '2026-05-18', 21411, 2082886995, -1494110814, 1846163462, 3200::numeric, 2600::numeric, 13572, 2082886995, -1494110814, 1846163462, '1299', 'D76', 'matched_same_stratum'),
        (11, 12239::bigint, 743035353::bigint, DATE '2026-05-18', 21460, 2082886995, 1447386007, 1222369825, 6990::numeric, 5490::numeric, 18092, 2082886995, 1447386007, 1222369825, '801', '72141', 'matched_same_stratum'),
        (12, 12240::bigint, 743035353::bigint, DATE '2026-05-18', 21472, 2082886995, 1496651500, -2004188974, 10990::numeric, 8990::numeric, 12303, 2082886995, 1496651500, -2004188974, '683', '41371-40', 'matched_same_stratum'),
        (13, 12241::bigint, 743035353::bigint, DATE '2026-05-18', 21473, 2082886995, 1681773579, -2004188974, 5990::numeric, 4990::numeric, 9378, 2082886995, 1681773579, -2004188974, '222', '1251', 'matched_same_stratum'),
        (14, 12242::bigint, 743035353::bigint, DATE '2026-05-18', 21474, 2082886995, 1681773579, -2004188974, 7990::numeric, 5990::numeric, 12234, 2082886995, 1681773579, -2004188974, '1', '1251', 'matched_same_stratum'),
        (15, 12243::bigint, 743035353::bigint, DATE '2026-05-18', 21475, 2082886995, 1681773579, -2004188974, 7990::numeric, 6200::numeric, 20430, 2082886995, 1681773579, -2004188974, '3028', '5259', 'matched_same_stratum'),
        (16, 12244::bigint, 743035353::bigint, DATE '2026-05-18', 21476, 2082886995, 1681773579, -2004188974, 7490::numeric, 5490::numeric, 20573, 2082886995, 1681773579, -2004188974, '2093', '3011', 'matched_same_stratum'),
        (17, 12245::bigint, 743035353::bigint, DATE '2026-05-18', 21482, 2082886995, 1496651500, -2004188974, 8990::numeric, 7990::numeric, 12304, 2082886995, 1496651500, -2004188974, '684', '43787-64', 'matched_same_stratum'),
        (18, 12246::bigint, 743035353::bigint, DATE '2026-05-18', 21485, 2082886995, 1496651500, -2004188974, 9590::numeric, 7590::numeric, 12596, 2082886995, 1496651500, -2004188974, '833', '43763', 'matched_same_stratum'),
        (19, 12247::bigint, 743035353::bigint, DATE '2026-05-18', 21488, 2082886995, 1496651500, -2004188974, 8990::numeric, 7990::numeric, 12597, 2082886995, 1496651500, -2004188974, '834', '43762', 'matched_same_stratum'),
        (20, 12248::bigint, 743035353::bigint, DATE '2026-05-18', 21565, 2082886995, 1447386007, -424518832, 10490::numeric, 7490::numeric, 14072, 2082886995, 1447386007, -424518832, '47', '175', 'matched_same_stratum')
)
SELECT c.*,
       'Europe/Belgrade'::text AS measurement_timezone,
       c.action_date - 28 AS pre_from_inclusive,
       c.action_date AS pre_to_exclusive,
       c.action_date AS post_from_inclusive,
       c.action_date + 28 AS post_to_exclusive,
       CASE WHEN c.treatment_supplier_id = c.control_supplier_id
                  AND c.treatment_shoe_type_id = c.control_shoe_type_id
            THEN 'same_store_supplier_shoe_type; deterministic lowest unused control article'
            ELSE 'same_store; supplier/shoe-type mismatch retained as evidence'
       END AS control_selection_criteria,
       'unknown'::text AS age_band_evidence,
       'source batch 23; original per-line cost provenance not available in local schema'::text AS cost_evidence,
       'historical descriptive only; no causal or recommendation claim'::text AS trust_limit
FROM frozen_cohort c
ORDER BY c.cohort_no;
