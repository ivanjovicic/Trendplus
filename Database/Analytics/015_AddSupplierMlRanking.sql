-- ==========================================================
-- 015_AddSupplierMlRanking.sql
-- Analytics-side supplier ML ranking layer.
--
-- Purpose:
-- - build a training dataset for supplier ranking from historical signals
-- - persist batch ML predictions and explanations
-- - blend ML score into vw_supplier_decision_score without breaking
--   supplier_quality_index / recommendation_code / confidence_score
-- ==========================================================

CREATE TABLE IF NOT EXISTS supplier_ml_predictions (
    id                                BIGSERIAL PRIMARY KEY,
    supplier_id                       integer      NOT NULL,
    snapshot_date                     date         NOT NULL,
    model_type                        text         NOT NULL DEFAULT 'supplier_ranking_v1',
    model_version_id                  bigint       NULL REFERENCES model_version(id) ON DELETE SET NULL,
    ml_supplier_score                 numeric(8,2) NOT NULL,
    predicted_supplier_success_score  numeric(8,2) NOT NULL,
    predicted_revenue_next_30d        numeric(18,2) NULL,
    predicted_margin_next_30d         numeric(18,2) NULL,
    predicted_sellthrough_next_30d    numeric(18,4) NULL,
    success_probability               numeric(10,6) NOT NULL,
    top_feature_1                     text         NULL,
    top_feature_2                     text         NULL,
    top_feature_3                     text         NULL,
    explanation_text                  text         NULL,
    created_at                        timestamptz  NOT NULL DEFAULT NOW()
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_supplier_ml_predictions_supplier_snapshot_model
    ON supplier_ml_predictions (supplier_id, snapshot_date, model_type);

CREATE INDEX IF NOT EXISTS idx_supplier_ml_predictions_supplier
    ON supplier_ml_predictions (supplier_id, snapshot_date DESC);

CREATE INDEX IF NOT EXISTS idx_supplier_ml_predictions_model_supplier_snapshot
    ON supplier_ml_predictions (model_type, supplier_id, snapshot_date DESC, created_at DESC);

CREATE INDEX IF NOT EXISTS idx_supplier_ml_predictions_model_version
    ON supplier_ml_predictions (model_version_id);

COMMENT ON TABLE supplier_ml_predictions IS
'Batch ML predictions for supplier ranking. One row per supplier, snapshot date and model type.';

COMMENT ON COLUMN supplier_ml_predictions.ml_supplier_score IS
'Final AI score on a 0-100 scale used as the ML component inside supplier decision scoring.';

DROP VIEW IF EXISTS vw_supplier_ranking_inference_v1;
DROP MATERIALIZED VIEW IF EXISTS supplier_training_dataset_v1;

CREATE MATERIALIZED VIEW supplier_training_dataset_v1 AS
WITH snapshot_calendar AS (
    -- Use the last observed sales day in each month as the training snapshot anchor.
    SELECT
        MAX(day)::date AS snapshot_date
    FROM mv_daily_sales_facts
    GROUP BY date_trunc('month', day)
),
supplier_universe AS (
    SELECT DISTINCT
        a."IDDobavljac" AS supplier_id,
        COALESCE(d."Naziv", 'Nepoznat dobavljac') AS supplier_name
    FROM "Artikli" a
    LEFT JOIN "Dobavljaci" d ON d."Id" = a."IDDobavljac"
    WHERE a."IDDobavljac" IS NOT NULL
),
supplier_snapshots AS (
    -- Keep only suppliers that were commercially active in the trailing 120-day window.
    SELECT
        su.supplier_id,
        su.supplier_name,
        sc.snapshot_date
    FROM supplier_universe su
    JOIN snapshot_calendar sc ON EXISTS (
        SELECT 1
        FROM mv_daily_sales_facts ds
        JOIN "Artikli" a ON a."Id" = ds.article_id
        WHERE a."IDDobavljac" = su.supplier_id
          AND ds.day > sc.snapshot_date - INTERVAL '120 days'
          AND ds.day <= sc.snapshot_date
        LIMIT 1
    )
),
sales_30d AS (
    SELECT
        ss.supplier_id,
        ss.snapshot_date,
        COALESCE(SUM(ds.revenue), 0)::numeric(18,2) AS revenue_30d,
        COALESCE(SUM(ds.units), 0)::numeric AS units_30d,
        ROUND(COALESCE(SUM(ds.units), 0) / 30.0, 4) AS sales_velocity
    FROM supplier_snapshots ss
    LEFT JOIN mv_daily_sales_facts ds
           ON ds.day > ss.snapshot_date - INTERVAL '30 days'
          AND ds.day <= ss.snapshot_date
    LEFT JOIN "Artikli" a ON a."Id" = ds.article_id
    WHERE a."IDDobavljac" = ss.supplier_id
       OR a."IDDobavljac" IS NULL
    GROUP BY ss.supplier_id, ss.snapshot_date
),
sales_90d_by_category AS (
    SELECT
        ss.supplier_id,
        ss.snapshot_date,
        COALESCE(NULLIF(a."Kategorija", ''), 'Uncategorized') AS category,
        COALESCE(SUM(ds.revenue), 0)::numeric(18,2) AS revenue_90d,
        COALESCE(SUM(ds.units), 0)::numeric AS units_90d
    FROM supplier_snapshots ss
    LEFT JOIN mv_daily_sales_facts ds
           ON ds.day > ss.snapshot_date - INTERVAL '90 days'
          AND ds.day <= ss.snapshot_date
    LEFT JOIN "Artikli" a ON a."Id" = ds.article_id
    WHERE a."IDDobavljac" = ss.supplier_id
       OR a."IDDobavljac" IS NULL
    GROUP BY ss.supplier_id, ss.snapshot_date, COALESCE(NULLIF(a."Kategorija", ''), 'Uncategorized')
),
dominant_category AS (
    SELECT DISTINCT ON (supplier_id, snapshot_date)
        supplier_id,
        snapshot_date,
        category AS primary_category
    FROM sales_90d_by_category
    ORDER BY supplier_id, snapshot_date, revenue_90d DESC, category
),
category_focus AS (
    SELECT
        supplier_id,
        snapshot_date,
        MAX(revenue_90d) / NULLIF(SUM(revenue_90d), 0) AS category_focus_score
    FROM sales_90d_by_category
    GROUP BY supplier_id, snapshot_date
),
markdown_event_features AS (
    -- Reuse existing first-markdown logic as the source of supplier price sensitivity and full-price demand signals.
    SELECT
        ss.supplier_id,
        ss.snapshot_date,
        COUNT(*)::int AS articles_count,
        COUNT(*) FILTER (WHERE fs.signal_quality_flag = 'high')::numeric / NULLIF(COUNT(*), 0) AS signal_quality_share,
        ROUND(
            SUM(COALESCE(fs.pre_qty_30d, 0))
            / NULLIF(SUM(COALESCE(fs.pre_qty_30d, 0) + GREATEST(COALESCE(fs.stock_before_markdown, 0), 0)), 0),
            4
        ) AS fullprice_sellthrough,
        ROUND(
            SUM(COALESCE(vn.post_revenue, 0))
            / NULLIF(SUM(COALESCE(fs.pre_revenue_30d, 0) + COALESCE(vn.post_revenue, 0)), 0),
            4
        ) AS markdown_dependency,
        ROUND(
            AVG(
                CASE
                    WHEN COALESCE(fs.old_price, 0) <= 0 THEN 0
                    ELSE (fs.old_price - fs.new_price) / NULLIF(fs.old_price, 0)
                END
            ),
            4
        ) AS price_change_pct,
        ROUND(COUNT(*)::numeric / 90.0, 4) AS discount_frequency,
        ROUND(
            COUNT(*) FILTER (
                WHERE COALESCE(vn.post_qty, 0) = 0
                  AND GREATEST(COALESCE(fs.stock_before_markdown, 0), 0) > 0
            )::numeric / NULLIF(COUNT(*), 0),
            4
        ) AS dead_stock_rate,
        SUM(GREATEST(COALESCE(fs.stock_before_markdown, 0), 0) * COALESCE(a."NabavnaCena", 0))::numeric(18,2) AS unsold_stock_value,
        ROUND(
            AVG(
                CASE
                    WHEN COALESCE(fs.pre_avg_daily_units, 0) > 0
                    THEN fs.stock_before_markdown / NULLIF(fs.pre_avg_daily_units, 0)
                    ELSE NULL
                END
            ),
            4
        ) AS days_of_cover,
        ROUND(
            AVG(GREATEST(COALESCE(fs.stock_before_markdown, 0), 0)),
            4
        ) AS avg_stock_before_markdown,
        ROUND(
            COUNT(*) FILTER (
                WHERE COALESCE(fs.pre_sellthrough_30d, 0) >= 0.45
                  AND COALESCE(fs.pre_margin_30d, 0) > 0
                  AND COALESCE(fs.had_sales_before_markdown_flag, FALSE)
                  AND fs.signal_quality_flag <> 'low'
            )::numeric / NULLIF(COUNT(*), 0),
            4
        ) AS repeat_winner_rate
    FROM supplier_snapshots ss
    JOIN vw_supplier_fullprice_signals fs
      ON fs.supplier_id = ss.supplier_id
     AND fs.first_markdown_date > ss.snapshot_date - INTERVAL '90 days'
     AND fs.first_markdown_date <= ss.snapshot_date
    LEFT JOIN LATERAL (
        SELECT
            v.post_qty,
            v.post_revenue
        FROM vw_vendor_sales_nivelacija v
        WHERE v.article_id = fs.article_id
          AND v.event_date::date = fs.first_markdown_date
          AND v.old_price = fs.old_price
          AND v.new_price = fs.new_price
        ORDER BY v.price_event_id
        LIMIT 1
    ) vn ON TRUE
    LEFT JOIN "Artikli" a ON a."Id" = fs.article_id
    GROUP BY ss.supplier_id, ss.snapshot_date
),
returns_30d AS (
    SELECT
        ss.supplier_id,
        ss.snapshot_date,
        COALESCE(SUM(ps.kolicina), 0)::numeric AS returned_units_30d
    FROM supplier_snapshots ss
    LEFT JOIN povracaj_zaglavlje pz
           ON pz.id_dobavljac = ss.supplier_id
          AND pz.datum_povracaja::date > ss.snapshot_date - INTERVAL '30 days'
          AND pz.datum_povracaja::date <= ss.snapshot_date
          AND COALESCE(pz.status, '') <> 'Odbijen'
    LEFT JOIN povracaj_stavke ps ON ps.id_povracaj = pz.id
    GROUP BY ss.supplier_id, ss.snapshot_date
),
trend_30d AS (
    SELECT
        ss.supplier_id,
        ss.snapshot_date,
        ROUND(COALESCE(AVG(th."FinalGlobalScore"), 0)::numeric, 4) AS trend_score,
        ROUND(
            (
                COALESCE(AVG(th."FinalGlobalScore") FILTER (
                    WHERE th."Date"::date > ss.snapshot_date - INTERVAL '7 days'
                      AND th."Date"::date <= ss.snapshot_date
                ), 0)
                -
                COALESCE(AVG(th."FinalGlobalScore") FILTER (
                    WHERE th."Date"::date > ss.snapshot_date - INTERVAL '14 days'
                      AND th."Date"::date <= ss.snapshot_date - INTERVAL '7 days'
                ), 0)
            )::numeric,
            4
        ) AS trend_momentum
    FROM supplier_snapshots ss
    JOIN "Artikli" a ON a."IDDobavljac" = ss.supplier_id
    JOIN "TrendHistory" th ON th."LocalProductId" = a."Id"
    WHERE th."Date"::date > ss.snapshot_date - INTERVAL '30 days'
      AND th."Date"::date <= ss.snapshot_date
    GROUP BY ss.supplier_id, ss.snapshot_date
),
future_sales_30d AS (
    SELECT
        ss.supplier_id,
        ss.snapshot_date,
        COALESCE(SUM(ds.revenue), 0)::numeric(18,2) AS label_revenue_next_30d,
        COALESCE(SUM(ds.units), 0)::numeric AS label_units_next_30d
    FROM supplier_snapshots ss
    LEFT JOIN mv_daily_sales_facts ds
           ON ds.day > ss.snapshot_date
          AND ds.day <= ss.snapshot_date + INTERVAL '30 days'
    LEFT JOIN "Artikli" a ON a."Id" = ds.article_id
    WHERE a."IDDobavljac" = ss.supplier_id
       OR a."IDDobavljac" IS NULL
    GROUP BY ss.supplier_id, ss.snapshot_date
),
future_margin_30d AS (
    SELECT
        ss.supplier_id,
        ss.snapshot_date,
        COALESCE(SUM(
            CASE
                WHEN pz.datum_prodaje::date > ss.snapshot_date
                 AND pz.datum_prodaje::date <= ss.snapshot_date + INTERVAL '30 days'
                THEN ps.kolicina * (ps.cena - COALESCE(ps.nabavna_cena, a."NabavnaCena", 0))
                ELSE 0
            END
        ), 0)::numeric(18,2) AS label_margin_next_30d
    FROM supplier_snapshots ss
    LEFT JOIN "Artikli" a ON a."IDDobavljac" = ss.supplier_id
    LEFT JOIN prodaja_stavke ps ON ps.id_artikal = a."Id"
    LEFT JOIN prodaja_zaglavlje pz ON pz.id = ps.id_prodaja
    GROUP BY ss.supplier_id, ss.snapshot_date
),
assembled AS (
    SELECT
        ss.supplier_id,
        ss.supplier_name,
        ss.snapshot_date,
        COALESCE(dc.primary_category, 'Uncategorized') AS primary_category,
        COALESCE(mef.articles_count, 0) AS articles_count,
        COALESCE(s30.revenue_30d, 0)::numeric(18,2) AS revenue_30d,
        COALESCE(s30.units_30d, 0)::numeric AS units_30d,
        COALESCE(s30.sales_velocity, 0) AS sales_velocity,
        COALESCE(mef.fullprice_sellthrough, 0) AS fullprice_sellthrough,
        COALESCE(mef.markdown_dependency, 0) AS markdown_dependency,
        COALESCE(mef.price_change_pct, 0) AS price_change_pct,
        COALESCE(mef.discount_frequency, 0) AS discount_frequency,
        COALESCE(mef.dead_stock_rate, 0) AS dead_stock_rate,
        COALESCE(mef.unsold_stock_value, 0)::numeric(18,2) AS unsold_stock_value,
        COALESCE(mef.days_of_cover, 0) AS days_of_cover,
        COALESCE(mef.repeat_winner_rate, 0) AS repeat_winner_rate,
        COALESCE(r30.returned_units_30d, 0) / NULLIF(COALESCE(s30.units_30d, 0), 0) AS return_rate,
        COALESCE(cf.category_focus_score, 0) AS category_focus_score,
        COALESCE(t30.trend_score, 0) AS trend_score,
        COALESCE(t30.trend_momentum, 0) AS trend_momentum,
        COALESCE(mef.signal_quality_share, 0) AS signal_quality_share,
        COALESCE(f30.label_revenue_next_30d, 0)::numeric(18,2) AS label_revenue_next_30d,
        COALESCE(fm30.label_margin_next_30d, 0)::numeric(18,2) AS label_margin_next_30d,
        ROUND(
            COALESCE(f30.label_units_next_30d, 0)
            / NULLIF(COALESCE(f30.label_units_next_30d, 0) + COALESCE(mef.avg_stock_before_markdown, 0), 0),
            4
        ) AS label_sellthrough_next_30d
    FROM supplier_snapshots ss
    LEFT JOIN sales_30d s30
           ON s30.supplier_id = ss.supplier_id
          AND s30.snapshot_date = ss.snapshot_date
    LEFT JOIN dominant_category dc
           ON dc.supplier_id = ss.supplier_id
          AND dc.snapshot_date = ss.snapshot_date
    LEFT JOIN category_focus cf
           ON cf.supplier_id = ss.supplier_id
          AND cf.snapshot_date = ss.snapshot_date
    LEFT JOIN markdown_event_features mef
           ON mef.supplier_id = ss.supplier_id
          AND mef.snapshot_date = ss.snapshot_date
    LEFT JOIN returns_30d r30
           ON r30.supplier_id = ss.supplier_id
          AND r30.snapshot_date = ss.snapshot_date
    LEFT JOIN trend_30d t30
           ON t30.supplier_id = ss.supplier_id
          AND t30.snapshot_date = ss.snapshot_date
    LEFT JOIN future_sales_30d f30
           ON f30.supplier_id = ss.supplier_id
          AND f30.snapshot_date = ss.snapshot_date
    LEFT JOIN future_margin_30d fm30
           ON fm30.supplier_id = ss.supplier_id
          AND fm30.snapshot_date = ss.snapshot_date
),
benchmarks AS (
    SELECT
        snapshot_date,
        primary_category,
        percentile_cont(0.5) WITHIN GROUP (ORDER BY label_revenue_next_30d) AS category_median_revenue_next_30d
    FROM assembled
    GROUP BY snapshot_date, primary_category
)
SELECT
    a.supplier_id,
    a.supplier_name,
    a.snapshot_date,
    a.primary_category,
    a.articles_count,
    a.revenue_30d,
    a.units_30d,
    a.sales_velocity,
    a.fullprice_sellthrough,
    a.markdown_dependency,
    a.price_change_pct,
    a.discount_frequency,
    a.dead_stock_rate,
    a.unsold_stock_value,
    a.days_of_cover,
    a.repeat_winner_rate,
    COALESCE(a.return_rate, 0) AS return_rate,
    a.category_focus_score,
    a.trend_score,
    a.trend_momentum,
    a.signal_quality_share,
    a.label_revenue_next_30d,
    a.label_margin_next_30d,
    COALESCE(a.label_sellthrough_next_30d, 0) AS label_sellthrough_next_30d,
    CASE
        WHEN a.label_revenue_next_30d > COALESCE(b.category_median_revenue_next_30d, 0)
         AND a.label_margin_next_30d >= 0
        THEN 1
        ELSE 0
    END AS success_label
FROM assembled a
LEFT JOIN benchmarks b
       ON b.snapshot_date = a.snapshot_date
      AND b.primary_category = a.primary_category;

CREATE UNIQUE INDEX IF NOT EXISTS idx_supplier_training_dataset_v1_pk
    ON supplier_training_dataset_v1 (supplier_id, snapshot_date);

CREATE INDEX IF NOT EXISTS idx_supplier_training_dataset_v1_snapshot
    ON supplier_training_dataset_v1 (snapshot_date DESC);

CREATE INDEX IF NOT EXISTS idx_supplier_training_dataset_v1_success
    ON supplier_training_dataset_v1 (success_label);

COMMENT ON MATERIALIZED VIEW supplier_training_dataset_v1 IS
'Monthly supplier ML dataset with trailing supplier signals and forward 30-day labels.';

COMMENT ON COLUMN supplier_training_dataset_v1.fullprice_sellthrough IS
'Trailing supplier sell-through before markdown, derived from first-markdown signal windows.';

COMMENT ON COLUMN supplier_training_dataset_v1.markdown_dependency IS
'Trailing supplier dependence on post-markdown revenue.';

COMMENT ON COLUMN supplier_training_dataset_v1.days_of_cover IS
'Approximate days of cover derived from stock_before_markdown and trailing pre-markdown daily units.';

COMMENT ON COLUMN supplier_training_dataset_v1.success_label IS
'Binary label indicating that supplier next-30-day revenue beat the median supplier in the same dominant category.';

CREATE OR REPLACE VIEW vw_supplier_ranking_inference_v1 AS
SELECT *
FROM supplier_training_dataset_v1
WHERE snapshot_date = (
    SELECT MAX(snapshot_date)
    FROM supplier_training_dataset_v1
);

COMMENT ON VIEW vw_supplier_ranking_inference_v1 IS
'Latest supplier feature snapshot used by the supplier ranking batch prediction step.';

DROP MATERIALIZED VIEW IF EXISTS mv_supplier_recommendations_cache;
DROP MATERIALIZED VIEW IF EXISTS mv_supplier_decision_score_cache;
DROP VIEW IF EXISTS vw_supplier_recommendations;

DO $supplier_ml_rename$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.views
        WHERE table_schema = 'public'
          AND table_name = 'vw_supplier_decision_score'
    )
    AND NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'vw_supplier_decision_score'
          AND column_name = 'ml_supplier_score'
    )
    THEN
        EXECUTE 'DROP VIEW IF EXISTS vw_supplier_decision_score_heuristic_base';
        EXECUTE 'ALTER VIEW vw_supplier_decision_score RENAME TO vw_supplier_decision_score_heuristic_base';
    END IF;
END
$supplier_ml_rename$;

CREATE OR REPLACE VIEW vw_supplier_ml_latest_predictions AS
SELECT DISTINCT ON (supplier_id)
    p.supplier_id,
    p.snapshot_date,
    p.model_type,
    p.model_version_id,
    p.ml_supplier_score,
    p.predicted_supplier_success_score,
    p.predicted_revenue_next_30d,
    p.predicted_margin_next_30d,
    p.predicted_sellthrough_next_30d,
    p.success_probability,
    p.top_feature_1,
    p.top_feature_2,
    p.top_feature_3,
    p.explanation_text,
    p.created_at
FROM supplier_ml_predictions p
LEFT JOIN model_version mv
       ON mv.id = p.model_version_id
WHERE p.model_type = 'supplier_ranking_v1'
  AND COALESCE(mv.is_active, TRUE)
ORDER BY p.supplier_id, p.snapshot_date DESC, p.created_at DESC, p.id DESC;

COMMENT ON VIEW vw_supplier_ml_latest_predictions IS
'Latest available supplier ML prediction row per supplier from the active model version.';

CREATE OR REPLACE VIEW vw_supplier_decision_score AS
WITH blended AS (
    SELECT
        hb.supplier_id,
        hb.supplier_name,
        hb.period_from,
        hb.period_to,
        hb.revenue,
        hb.units,
        hb.fullprice_revenue_share,
        hb.fullprice_sellthrough,
        hb.pre_markdown_margin_pct,
        hb.markdown_dependency_score,
        hb.stock_risk_score,
        hb.return_rate,
        hb.category_focus_score,
        hb.repeat_winner_rate,
        hb.confidence_score,
        hb.recommendation_code AS heuristic_recommendation_code,
        hb.supplier_quality_index AS heuristic_score,
        ROUND(COALESCE(ml.ml_supplier_score, hb.supplier_quality_index), 2) AS ml_supplier_score,
        ROUND(
            LEAST(
                100,
                GREATEST(
                    0,
                    0.60 * COALESCE(ml.ml_supplier_score, hb.supplier_quality_index)
                    + 0.40 * hb.supplier_quality_index
                )
            ),
            2
        ) AS blended_score
    FROM vw_supplier_decision_score_heuristic_base hb
    LEFT JOIN vw_supplier_ml_latest_predictions ml
           ON ml.supplier_id = hb.supplier_id
),
recommendation_logic AS (
    SELECT
        b.*,
        CASE
            WHEN COALESCE(b.return_rate, 0) > 0.12 THEN 'REVIEW_QUALITY'
            WHEN COALESCE(b.heuristic_recommendation_code, '') = 'OOS_FALSE_NEGATIVE' THEN 'OOS_FALSE_NEGATIVE'
            WHEN b.blended_score > 80 THEN 'EXPAND'
            WHEN b.blended_score >= 60 THEN 'EXPAND_SELECTIVELY'
            WHEN b.blended_score >= 40 THEN 'HOLD'
            WHEN b.blended_score >= 25 THEN 'PRICE_NEGOTIATE'
            ELSE 'ASSORTMENT_REDUCE'
        END AS recommendation_code
    FROM blended b
)
SELECT
    supplier_id,
    supplier_name,
    period_from,
    period_to,
    revenue,
    units,
    fullprice_revenue_share,
    fullprice_sellthrough,
    pre_markdown_margin_pct,
    markdown_dependency_score,
    stock_risk_score,
    return_rate,
    category_focus_score,
    repeat_winner_rate,
    ml_supplier_score,
    blended_score AS supplier_quality_index,
    recommendation_code,
    confidence_score
FROM recommendation_logic;

COMMENT ON VIEW vw_supplier_decision_score IS
'Final supplier decision view that blends the heuristic supplier score with the latest ML supplier score.';

COMMENT ON COLUMN vw_supplier_decision_score.ml_supplier_score IS
'Latest AI supplier ranking score on a 0-100 scale.';

CREATE OR REPLACE VIEW vw_supplier_recommendations AS
WITH base AS (
    SELECT *
    FROM vw_supplier_decision_score
),
ml AS (
    SELECT *
    FROM vw_supplier_ml_latest_predictions
)
SELECT
    b.supplier_id,
    b.supplier_name,
    b.recommendation_code,
    CASE b.recommendation_code
        WHEN 'EXPAND' THEN 'Povecati saradnju'
        WHEN 'EXPAND_SELECTIVELY' THEN 'Povecati selektivno'
        WHEN 'PRICE_NEGOTIATE' THEN 'Pregovarati o ceni'
        WHEN 'ASSORTMENT_REDUCE' THEN 'Smanjiti nabavku'
        WHEN 'OOS_FALSE_NEGATIVE' THEN 'Proveriti zalihe pre odluke'
        WHEN 'REVIEW_QUALITY' THEN 'Proveriti kvalitet i povracaje'
        ELSE 'Zadrzati postojeci nivo'
    END AS recommendation_title,
    COALESCE(
        NULLIF(ml.explanation_text, ''),
        CASE b.recommendation_code
            WHEN 'EXPAND' THEN 'Visok sell-through bez snizenja i stabilna marza.'
            WHEN 'EXPAND_SELECTIVELY' THEN 'Dobavljac ima dobar potencijal, ali se najbolji rezultat vidi u uzem delu asortimana.'
            WHEN 'PRICE_NEGOTIATE' THEN 'Prodaja se otvara tek nakon snizenja, pa ulaznu cenu treba pregovarati.'
            WHEN 'ASSORTMENT_REDUCE' THEN 'Visoka zavisnost od snizenja i spor promet vezuju kapital u zalihama.'
            WHEN 'OOS_FALSE_NEGATIVE' THEN 'Nedostatak zaliha pre snizenja verovatno iskrivljuje procenu ovog dobavljaca.'
            WHEN 'REVIEW_QUALITY' THEN 'Povracaji su previsoki u odnosu na prodaju i umanjuju kvalitet saradnje.'
            ELSE 'Signal je mesovit, pa je najbolje zadrzati trenutni nivo saradnje dok se ne prikupi vise podataka.'
        END
    ) AS recommendation_reason,
    CASE b.recommendation_code
        WHEN 'EXPAND' THEN 'ml_supplier_score'
        WHEN 'EXPAND_SELECTIVELY' THEN 'category_focus_score'
        WHEN 'PRICE_NEGOTIATE' THEN 'markdown_dependency_score'
        WHEN 'ASSORTMENT_REDUCE' THEN 'stock_risk_score'
        WHEN 'OOS_FALSE_NEGATIVE' THEN 'confidence_score'
        WHEN 'REVIEW_QUALITY' THEN 'return_rate'
        ELSE 'ml_supplier_score'
    END AS primary_metric,
    CASE b.recommendation_code
        WHEN 'EXPAND' THEN b.ml_supplier_score
        WHEN 'EXPAND_SELECTIVELY' THEN b.category_focus_score
        WHEN 'PRICE_NEGOTIATE' THEN b.markdown_dependency_score
        WHEN 'ASSORTMENT_REDUCE' THEN b.stock_risk_score
        WHEN 'OOS_FALSE_NEGATIVE' THEN b.confidence_score
        WHEN 'REVIEW_QUALITY' THEN b.return_rate
        ELSE b.ml_supplier_score
    END AS primary_metric_value,
    CASE b.recommendation_code
        WHEN 'EXPAND' THEN COALESCE(NULLIF(ml.top_feature_1, ''), 'fullprice_sellthrough')
        WHEN 'EXPAND_SELECTIVELY' THEN COALESCE(NULLIF(ml.top_feature_2, ''), 'repeat_winner_rate')
        WHEN 'PRICE_NEGOTIATE' THEN 'pre_markdown_margin_pct'
        WHEN 'ASSORTMENT_REDUCE' THEN 'markdown_dependency_score'
        WHEN 'OOS_FALSE_NEGATIVE' THEN 'fullprice_sellthrough'
        WHEN 'REVIEW_QUALITY' THEN 'ml_supplier_score'
        ELSE COALESCE(NULLIF(ml.top_feature_3, ''), 'confidence_score')
    END AS secondary_metric,
    CASE b.recommendation_code
        WHEN 'EXPAND' THEN b.fullprice_sellthrough
        WHEN 'EXPAND_SELECTIVELY' THEN b.repeat_winner_rate
        WHEN 'PRICE_NEGOTIATE' THEN b.pre_markdown_margin_pct
        WHEN 'ASSORTMENT_REDUCE' THEN b.markdown_dependency_score
        WHEN 'OOS_FALSE_NEGATIVE' THEN b.fullprice_sellthrough
        WHEN 'REVIEW_QUALITY' THEN b.ml_supplier_score
        ELSE b.confidence_score
    END AS secondary_metric_value,
    CASE b.recommendation_code
        WHEN 'EXPAND' THEN 'medium'
        WHEN 'EXPAND_SELECTIVELY' THEN 'medium'
        WHEN 'PRICE_NEGOTIATE' THEN 'high'
        WHEN 'ASSORTMENT_REDUCE' THEN 'high'
        WHEN 'OOS_FALSE_NEGATIVE' THEN 'medium'
        WHEN 'REVIEW_QUALITY' THEN 'high'
        ELSE 'low'
    END AS urgency,
    b.confidence_score
FROM base b
LEFT JOIN ml ON ml.supplier_id = b.supplier_id;

COMMENT ON VIEW vw_supplier_recommendations IS
'Presentation-ready supplier recommendations enriched with ML explanation text when available.';

CREATE MATERIALIZED VIEW IF NOT EXISTS mv_supplier_decision_score_cache AS
SELECT *
FROM vw_supplier_decision_score;

CREATE UNIQUE INDEX IF NOT EXISTS idx_mv_supplier_decision_score_cache_pk
    ON mv_supplier_decision_score_cache (supplier_id);

CREATE MATERIALIZED VIEW IF NOT EXISTS mv_supplier_recommendations_cache AS
SELECT *
FROM vw_supplier_recommendations;

CREATE UNIQUE INDEX IF NOT EXISTS idx_mv_supplier_recommendations_cache_pk
    ON mv_supplier_recommendations_cache (supplier_id);

COMMENT ON MATERIALIZED VIEW mv_supplier_decision_score_cache IS
'Materialized cache of ML-enhanced supplier decision rows for the default Supplier Decision Hub overview.';

COMMENT ON MATERIALIZED VIEW mv_supplier_recommendations_cache IS
'Materialized cache of ML-enhanced supplier recommendations for the default Supplier Decision Hub overview.';
