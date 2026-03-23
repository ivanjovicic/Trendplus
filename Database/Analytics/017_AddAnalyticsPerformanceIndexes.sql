-- Performance indexes for analytics-heavy endpoints.
-- Safe to run repeatedly.

DO $$
BEGIN
    IF to_regclass('public."SalesFacts"') IS NOT NULL THEN
        EXECUTE 'CREATE INDEX IF NOT EXISTS idx_perf_salesfacts_store_time_saleid ON "SalesFacts" ("StoreId", "SaleTimestampUtc", "SaleId")';
        EXECUTE 'CREATE INDEX IF NOT EXISTS idx_perf_salesfacts_time_saleid ON "SalesFacts" ("SaleTimestampUtc", "SaleId")';
    END IF;
END $$;

-- SQL_BATCH_BREAK

DO $$
BEGIN
    IF to_regclass('public."SalesLineFacts"') IS NOT NULL THEN
        EXECUTE 'CREATE INDEX IF NOT EXISTS idx_perf_saleslinefacts_saleid_productid ON "SalesLineFacts" ("SaleId", "ProductId")';
    END IF;
END $$;

-- SQL_BATCH_BREAK

DO $$
BEGIN
    IF to_regclass('public.analytics_inventory_alert_snapshot') IS NOT NULL THEN
        EXECUTE 'CREATE INDEX IF NOT EXISTS idx_perf_alert_snapshot_filter_sort ON analytics_inventory_alert_snapshot (store_id, supplier_id, severity, confidence_score DESC)';
    END IF;
END $$;

-- SQL_BATCH_BREAK

DO $$
BEGIN
    IF to_regclass('public.analytics_rebalance_suggestion_snapshot') IS NOT NULL THEN
        EXECUTE 'CREATE INDEX IF NOT EXISTS idx_perf_rebalance_snapshot_filter_sort ON analytics_rebalance_suggestion_snapshot (supplier_id, urgency, from_store_id, to_store_id, confidence DESC, expected_saved_sales DESC, recommended_qty DESC)';
    END IF;
END $$;
