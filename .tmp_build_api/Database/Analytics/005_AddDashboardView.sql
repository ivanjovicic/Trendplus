-- ============================================================
-- Dashboard View for Trendplus Global Trend Analytics
-- Implements the pattern recommended in ANALYTICS design:
--
--   SELECT * FROM item_run_stats
--   WHERE run_id = (SELECT MAX(run_id) FROM runs)
--   ORDER BY final_score DESC;
--
-- ... but enriched with item metadata, images, sources,
-- markets, price history, and score components.
--
-- Usage:
--   SELECT * FROM v_item_run_stats WHERE run_id = (SELECT MAX(run_id) FROM runs) ORDER BY final_score DESC;
-- ============================================================

-- Full enriched stats view (all runs, filter by run_id in queries)
CREATE OR REPLACE VIEW v_item_run_stats AS
SELECT
    irs.stat_id,
    irs.run_id,
    irs.item_id,
    irs.rank,
    irs.final_score,
    irs.base_score,
    irs.momentum_raw,
    irs.momentum_normalized,
    irs.appearance_count,
    irs.source_count,
    irs.market_count,
    irs.created_at                          AS stat_created_at,

    -- Canonical item metadata
    i.brand,
    i.name,
    i.category,
    i.canonical_key,

    -- Best available image (earliest uploaded)
    ( SELECT ii.image_url
      FROM   item_images ii
      WHERE  ii.item_id = irs.item_id
      ORDER  BY ii.created_at ASC
      LIMIT  1
    )                                       AS image_url,

    -- Score component breakdown as JSON object { component_name: value }
    ( SELECT json_object_agg(sc.component_name, sc.component_value ORDER BY sc.component_name)
      FROM   score_components sc
      WHERE  sc.stat_id = irs.stat_id
    )                                       AS score_components,

    -- Unique markets seen in this run
    ( SELECT json_agg(DISTINCT ims.market ORDER BY ims.market)
      FROM   item_market_stats ims
      WHERE  ims.item_id  = irs.item_id
        AND  ims.run_id   = irs.run_id
    )                                       AS markets,

    -- All known sources for this item (across all runs)
    ( SELECT json_agg(DISTINCT isrc.source_name ORDER BY isrc.source_name)
      FROM   item_sources isrc
      WHERE  isrc.item_id = irs.item_id
    )                                       AS sources,

    -- Price range observed in this run
    ( SELECT MIN(iph.price)
      FROM   item_price_history iph
      WHERE  iph.item_id = irs.item_id AND iph.run_id = irs.run_id
    )                                       AS min_price,

    ( SELECT MAX(iph.price)
      FROM   item_price_history iph
      WHERE  iph.item_id = irs.item_id AND iph.run_id = irs.run_id
    )                                       AS max_price,

    -- Previous run score for momentum transparency
    ( SELECT prev.final_score
      FROM   item_run_stats prev
      WHERE  prev.item_id = irs.item_id
        AND  prev.run_id  < irs.run_id
      ORDER  BY prev.run_id DESC
      LIMIT  1
    )                                       AS prev_final_score,

    -- How many runs this item has appeared in (longevity signal)
    ( SELECT COUNT(*)
      FROM   item_run_stats hist
      WHERE  hist.item_id = irs.item_id
    )                                       AS total_run_appearances

FROM item_run_stats  irs
JOIN items           i   ON i.item_id = irs.item_id;

COMMENT ON VIEW v_item_run_stats IS
'Enriched item_run_stats with brand/name, image, sources, markets, price range, and score components. Query with WHERE run_id = (SELECT MAX(run_id) FROM runs) for latest snapshot.';

-- Convenience shortcut: always the latest completed run
CREATE OR REPLACE VIEW v_latest_run_items AS
SELECT v.*,
       r.started_at  AS run_started_at,
       r.finished_at AS run_finished_at,
       r.status      AS run_status
FROM   v_item_run_stats v
JOIN   runs r ON r.run_id = v.run_id
WHERE  v.run_id = (
    SELECT MAX(run_id) FROM runs WHERE status = 'completed'
)
ORDER  BY v.final_score DESC;

COMMENT ON VIEW v_latest_run_items IS
'Shortcut view: latest completed run top items, ordered by final_score DESC.';
