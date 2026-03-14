-- Additional indexes for analytics dashboard filters and grouped sales queries.
-- This extends the existing pre-aggregated table strategy without introducing
-- a second materialization architecture.

DO $$
BEGIN
    IF to_regclass('public.prodaja_zaglavlje') IS NOT NULL THEN
        IF EXISTS (
            SELECT 1
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = 'prodaja_zaglavlje'
              AND column_name = 'datum_prodaje'
        ) THEN
            EXECUTE 'CREATE INDEX IF NOT EXISTS idx_prodaja_zaglavlje_datum_objekat ON prodaja_zaglavlje (datum_prodaje, id_objekat)';
            EXECUTE 'CREATE INDEX IF NOT EXISTS idx_prodaja_zaglavlje_datum_payment ON prodaja_zaglavlje (datum_prodaje, nacin_placanja)';
        END IF;
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('public.prodaja_stavke') IS NOT NULL THEN
        EXECUTE 'CREATE INDEX IF NOT EXISTS idx_prodaja_stavke_prodaja_artikal_cover ON prodaja_stavke (id_prodaja, id_artikal)';
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS idx_artikli_supplier_category_gender
    ON "Artikli" ("IDDobavljac", "Kategorija", "Pol");

CREATE INDEX IF NOT EXISTS idx_artikli_objekat_supplier
    ON "Artikli" ("IDObjekat", "IDDobavljac");

CREATE INDEX IF NOT EXISTS idx_artikli_updated_at
    ON "Artikli" ("UpdatedAt" DESC);

CREATE INDEX IF NOT EXISTS idx_analytics_daily_summary_updated_at
    ON "AnalyticsDailySummary" ("UpdatedAt" DESC);

CREATE INDEX IF NOT EXISTS idx_analytics_category_summary_date_category
    ON "AnalyticsCategorySummary" ("Date" DESC, "Kategorija");

CREATE INDEX IF NOT EXISTS idx_analytics_supplier_summary_date_supplier
    ON "AnalyticsSupplierSummary" ("Date" DESC, "DobavljacId");

CREATE INDEX IF NOT EXISTS idx_analytics_gender_summary_date_gender
    ON "AnalyticsGenderSummary" ("Date" DESC, "Pol");
