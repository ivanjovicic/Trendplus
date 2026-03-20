-- ==========================================================
-- 024_FixOrphanedSalesArticles.sql
-- Fix: 519 rows in prodaja_stavke reference article IDs that
-- no longer exist in Artikli, causing COALESCE(..., 'Nepoznato')
-- in sales aggregations.
--
-- Strategy:
--   1) Ensure a "Arhivirani dobavljac" placeholder Dobavljaci row exists.
--   2) For every distinct id_artikal in prodaja_stavke that has no
--      corresponding Artikli row, insert a placeholder Artikli record
--      that points to the placeholder Dobavljaci.
-- ==========================================================

DO $$
DECLARE
    v_placeholder_vendor_id BIGINT;
    v_artikal_id            BIGINT;
BEGIN

    -- ── Step 1: get or create a placeholder Dobavljaci row ─────────────────
    SELECT "Id" INTO v_placeholder_vendor_id
    FROM "Dobavljaci"
    WHERE "Naziv" = 'Arhivirani dobavljac'
    LIMIT 1;

    IF v_placeholder_vendor_id IS NULL THEN
        -- Use a stable negative ID that won't collide with real data
        v_placeholder_vendor_id := -999999999;

        INSERT INTO "Dobavljaci" ("Id", "Naziv")
        VALUES (v_placeholder_vendor_id, 'Arhivirani dobavljac')
        ON CONFLICT ("Id") DO NOTHING;

        -- Re-read in case it already existed under that ID
        SELECT "Id" INTO v_placeholder_vendor_id
        FROM "Dobavljaci"
        WHERE "Id" = -999999999;
    END IF;

    RAISE NOTICE 'Using placeholder Dobavljaci Id=%', v_placeholder_vendor_id;

    -- ── Step 2: insert placeholder Artikli for each orphaned id_artikal ────
    FOR v_artikal_id IN
        SELECT DISTINCT ps.id_artikal
        FROM prodaja_stavke ps
        LEFT JOIN "Artikli" a ON a."Id" = ps.id_artikal
        WHERE ps.id_artikal IS NOT NULL
          AND a."Id" IS NULL
        ORDER BY ps.id_artikal
    LOOP
        INSERT INTO "Artikli" ("Id", "Naziv", "IDDobavljac")
        VALUES (
            v_artikal_id,
            'Arhivirani artikal ' || v_artikal_id,
            v_placeholder_vendor_id
        )
        ON CONFLICT ("Id") DO NOTHING;
    END LOOP;

    RAISE NOTICE 'Done inserting placeholder Artikli rows.';
END;
$$;
