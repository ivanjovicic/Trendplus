-- Performance optimization indexes for Analytics Database
-- Run this against the database with SalesFacts, SalesLineFacts, and snapshot tables

-- Snapshot table indexes (fix long durations in inventory alerts/rebalance)
DO $$ 
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'analytics_inventory_alert_snapshot') THEN
        CREATE INDEX IF NOT EXISTS ix_inv_alert_snap_store_sev ON analytics_inventory_alert_snapshot(store_id, severity);
        CREATE INDEX IF NOT EXISTS ix_inv_alert_snap_supplier ON analytics_inventory_alert_snapshot(supplier_id);
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'analytics_rebalance_suggestion_snapshot') THEN
        CREATE INDEX IF NOT EXISTS ix_rebal_snap_stores ON analytics_rebalance_suggestion_snapshot(from_store_id, to_store_id);
        CREATE INDEX IF NOT EXISTS ix_rebal_snap_supplier ON analytics_rebalance_suggestion_snapshot(supplier_id);
        CREATE INDEX IF NOT EXISTS ix_rebal_snap_urgency ON analytics_rebalance_suggestion_snapshot(urgency);
    END IF;

    -- Analytics fact table indexes (fix long durations in Sales Summary and Top Products)
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'SalesFacts') THEN
        CREATE INDEX IF NOT EXISTS ix_salesfacts_ts_store ON "SalesFacts"("SaleTimestampUtc", "StoreId");
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'SalesLineFacts') THEN
        CREATE INDEX IF NOT EXISTS ix_saleslinefacts_saleid ON "SalesLineFacts"("SaleId");
        CREATE INDEX IF NOT EXISTS ix_saleslinefacts_productid ON "SalesLineFacts"("ProductId");
    END IF;
END $$;
