-- ==========================================================
-- 020_AddRuntimeScoringSearchIndexes.sql
--
-- Purpose:
-- - accelerate RuntimeScoringEngine local product resolution filters
--   against public."Artikli"
-- - the engine currently uses ILIKE '%...%' on Kategorija, Velicina,
--   Boja and Materijal, so trigram indexes are required
--
-- Execution notes:
-- - intended to run WITHOUT a wrapping transaction
-- - CREATE INDEX CONCURRENTLY keeps write blocking low on live systems
-- ==========================================================

CREATE EXTENSION IF NOT EXISTS pg_trgm;

-- SQL_BATCH_BREAK
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_artikli_kategorija_trgm
    ON public."Artikli"
    USING gin ("Kategorija" gin_trgm_ops);

-- SQL_BATCH_BREAK
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_artikli_velicina_trgm
    ON public."Artikli"
    USING gin ("Velicina" gin_trgm_ops);

-- SQL_BATCH_BREAK
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_artikli_boja_trgm
    ON public."Artikli"
    USING gin ("Boja" gin_trgm_ops);

-- SQL_BATCH_BREAK
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_artikli_materijal_trgm
    ON public."Artikli"
    USING gin ("Materijal" gin_trgm_ops);
