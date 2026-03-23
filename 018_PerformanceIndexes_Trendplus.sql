-- Performance optimization indexes for Trendplus Database
-- Run this against the core database containing Artikli, prodaja_zaglavlje, and prodaja_stavke

DO $$ 
BEGIN
    -- Core table indexes for Artikli and Sales
    -- NOTE: These will fail if the names refer to VIEWS. These should only be applied to physical tables.
    
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'prodaja_stavke' AND table_type = 'BASE TABLE') THEN
        CREATE INDEX IF NOT EXISTS ix_prodaja_stavke_artikal ON prodaja_stavke(id_artikal);
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'prodaja_zaglavlje' AND table_type = 'BASE TABLE') THEN
        CREATE INDEX IF NOT EXISTS ix_prodaja_zaglavlje_datum ON prodaja_zaglavlje(datum_prodaje);
    END IF;

    -- Text search support (requires pg_trgm extension for efficient ILIKE)
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE (table_name = 'Artikli' OR table_name = 'artikli') AND table_type = 'BASE TABLE') THEN
        -- Check for pg_trgm
        IF NOT EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'pg_trgm') THEN
            CREATE EXTENSION pg_trgm;
        END IF;

        -- Create trigram indexes for fuzzy search (ILIKE)
        -- We try both casing versions just in case
        PERFORM create_trgm_if_exists('Artikli', 'Naziv', 'ix_artikli_naziv_trgm');
        PERFORM create_trgm_if_exists('Artikli', 'PLU', 'ix_artikli_plu_trgm');
        PERFORM create_trgm_if_exists('artikli', 'Naziv', 'ix_artikli_naziv_trgm_low');
        PERFORM create_trgm_if_exists('artikli', 'PLU', 'ix_artikli_plu_trgm_low');
    END IF;
END $$;

-- Helper function to avoid repetition and handle casing
CREATE OR REPLACE FUNCTION create_trgm_if_exists(t_name text, c_name text, i_name text) RETURNS void AS $func$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = t_name AND column_name = c_name) THEN
        EXECUTE format('CREATE INDEX IF NOT EXISTS %I ON %I USING gin (%I gin_trgm_ops)', i_name, t_name, c_name);
    END IF;
END;
$func$ LANGUAGE plpgsql;

-- Run helper for the specific tables
SELECT create_trgm_if_exists('Artikli', 'Naziv', 'ix_artikli_naziv_trgm');
SELECT create_trgm_if_exists('Artikli', 'PLU', 'ix_artikli_plu_trgm');
SELECT create_trgm_if_exists('artikli', 'Naziv', 'ix_artikli_naziv_trgm_low');
SELECT create_trgm_if_exists('artikli', 'PLU', 'ix_artikli_plu_trgm_low');

DROP FUNCTION create_trgm_if_exists(text, text, text);
