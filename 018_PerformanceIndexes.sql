-- Performance optimization indexes

-- Snapshot table indexes (fix long durations in inventory alerts/rebalance)
CREATE INDEX IF NOT EXISTS ix_inv_alert_snap_store_sev ON analytics_inventory_alert_snapshot(store_id, severity);
CREATE INDEX IF NOT EXISTS ix_inv_alert_snap_supplier ON analytics_inventory_alert_snapshot(supplier_id);
CREATE INDEX IF NOT EXISTS ix_rebal_snap_stores ON analytics_rebalance_suggestion_snapshot(from_store_id, to_store_id);
CREATE INDEX IF NOT EXISTS ix_rebal_snap_supplier ON analytics_rebalance_suggestion_snapshot(supplier_id);
CREATE INDEX IF NOT EXISTS ix_rebal_snap_urgency ON analytics_rebalance_suggestion_snapshot(urgency);

-- Analytics fact table indexes (fix long durations in Sales Summary and Top Products)
CREATE INDEX IF NOT EXISTS ix_salesfacts_ts_store ON "SalesFacts"("SaleTimestampUtc", "StoreId");
CREATE INDEX IF NOT EXISTS ix_saleslinefacts_saleid ON "SalesLineFacts"("SaleId");
CREATE INDEX IF NOT EXISTS ix_saleslinefacts_productid ON "SalesLineFacts"("ProductId");

-- Supporting core indexes
CREATE INDEX IF NOT EXISTS ix_prodaja_stavke_artikal ON prodaja_stavke(id_artikal);
CREATE INDEX IF NOT EXISTS ix_prodaja_zaglavlje_datum ON prodaja_zaglavlje(datum_prodaje);

-- Text search support for Data Quality (requires pg_trgm extension for efficient ILIKE)
DO $$ 
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'pg_trgm') THEN
        CREATE EXTENSION pg_trgm;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_artikli_naziv_trgm ON "Artikli" USING gin ("Naziv" gin_trgm_ops);
CREATE INDEX IF NOT EXISTS ix_artikli_plu_trgm ON "Artikli" USING gin ("PLU" gin_trgm_ops);
