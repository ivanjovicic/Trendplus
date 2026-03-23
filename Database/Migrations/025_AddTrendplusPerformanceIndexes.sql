-- Performance indexes for Trendplus operational tables.
-- Safe to run repeatedly (IF NOT EXISTS / existence checks).

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
            EXECUTE 'CREATE INDEX IF NOT EXISTS idx_perf_prodaja_zaglavlje_datum_id ON prodaja_zaglavlje (datum_prodaje, id)';
        END IF;
    END IF;
END $$;

-- SQL_BATCH_BREAK

DO $$
BEGIN
    IF to_regclass('public.prodaja_stavke') IS NOT NULL THEN
        EXECUTE 'CREATE INDEX IF NOT EXISTS idx_perf_prodaja_stavke_prodaja_artikal ON prodaja_stavke (id_prodaja, id_artikal)';
        EXECUTE 'CREATE INDEX IF NOT EXISTS idx_perf_prodaja_stavke_artikal ON prodaja_stavke (id_artikal)';
    END IF;
END $$;

-- SQL_BATCH_BREAK

DO $$
BEGIN
    IF to_regclass('public."Artikli"') IS NOT NULL THEN
        EXECUTE 'CREATE INDEX IF NOT EXISTS idx_perf_artikli_supplier_type ON "Artikli" ("IDDobavljac", "IDTipObuce")';
        EXECUTE 'CREATE INDEX IF NOT EXISTS idx_perf_artikli_updated ON "Artikli" ("UpdatedAt" DESC)';
    END IF;
END $$;

-- SQL_BATCH_BREAK

DO $$
BEGIN
    BEGIN
        CREATE EXTENSION IF NOT EXISTS pg_trgm;
    EXCEPTION
        WHEN insufficient_privilege THEN
            RAISE NOTICE 'Skipping pg_trgm extension create (insufficient privilege).';
    END;
END $$;

-- SQL_BATCH_BREAK

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM pg_extension
        WHERE extname = 'pg_trgm'
    ) THEN
        IF to_regclass('public."Artikli"') IS NOT NULL THEN
            EXECUTE 'CREATE INDEX IF NOT EXISTS idx_perf_artikli_naziv_trgm ON "Artikli" USING gin ("Naziv" gin_trgm_ops)';
            EXECUTE 'CREATE INDEX IF NOT EXISTS idx_perf_artikli_plu_trgm ON "Artikli" USING gin ("PLU" gin_trgm_ops)';
        END IF;

        IF to_regclass('public."Dobavljaci"') IS NOT NULL THEN
            EXECUTE 'CREATE INDEX IF NOT EXISTS idx_perf_dobavljaci_naziv_trgm ON "Dobavljaci" USING gin ("Naziv" gin_trgm_ops)';
        END IF;

        IF to_regclass('public."TipoviObuce"') IS NOT NULL THEN
            EXECUTE 'CREATE INDEX IF NOT EXISTS idx_perf_tipoviobuce_naziv_trgm ON "TipoviObuce" USING gin ("Naziv" gin_trgm_ops)';
        END IF;
    END IF;
END $$;
