-- ==========================================================
-- 023_FixAllRemainingDobavljaci.sql
-- Dynamic fix: loops over EVERY distinct placeholder vendor ID
-- still in fix_missing_dobavljaci_candidates and creates a
-- Dobavljaci row + mapping for each one.  Idempotent.
-- ==========================================================

BEGIN;

-- Step 1: For every distinct placeholder still present, ensure a
-- Dobavljaci row exists and register any unmapped candidates.
DO $$
DECLARE
    v_placeholder_id bigint;
    v_target_id      integer;
    v_naziv          text;
BEGIN
    FOR v_placeholder_id IN
        SELECT DISTINCT artikl_iddobavljac
        FROM   fix_missing_dobavljaci_candidates
        WHERE  artikl_iddobavljac IS NOT NULL
        ORDER  BY artikl_iddobavljac
    LOOP
        v_naziv := 'Unmapped vendor ' || v_placeholder_id::text;

        -- Get or create the Dobavljaci placeholder row
        SELECT "Id" INTO v_target_id
        FROM   "Dobavljaci"
        WHERE  "Naziv" = v_naziv
        LIMIT  1;

        IF v_target_id IS NULL THEN
            INSERT INTO "Dobavljaci" ("Naziv", "DataOrigin")
            VALUES (v_naziv, 'import-fix')
            RETURNING "Id" INTO v_target_id;
        END IF;

        -- Register mapping rows for candidates not yet mapped
        INSERT INTO fix_dobavljaci_mapping (candidate_id, target_dobavljac_id)
        SELECT c.id, v_target_id
        FROM   fix_missing_dobavljaci_candidates c
        WHERE  c.artikl_iddobavljac = v_placeholder_id
          AND  NOT EXISTS (
               SELECT 1 FROM fix_dobavljaci_mapping m
               WHERE  m.candidate_id = c.id
          );
    END LOOP;
END$$;

-- Step 2: Apply all unapplied mappings (update Artikli and DnevnikPromena).
DO $$
DECLARE
    r RECORD;
BEGIN
    FOR r IN SELECT * FROM fix_dobavljaci_mapping WHERE applied = FALSE
    LOOP
        -- Update Artikli
        UPDATE "Artikli" a
        SET    "IDDobavljac" = r.target_dobavljac_id
        WHERE  a."Id" IN (
            SELECT artikl_id
            FROM   fix_missing_dobavljaci_candidates
            WHERE  id = r.candidate_id
              AND  artikl_id IS NOT NULL
        );

        -- Update DnevnikPromena
        UPDATE "DnevnikPromena" d
        SET    "DobavljacId" = r.target_dobavljac_id
        WHERE  d."DobavljacId" IN (
            SELECT referenced_vendor_id
            FROM   fix_missing_dobavljaci_candidates
            WHERE  id = r.candidate_id
              AND  referenced_vendor_id IS NOT NULL
        );

        UPDATE fix_dobavljaci_mapping
        SET    applied = TRUE, applied_at = now()
        WHERE  id = r.id;
    END LOOP;
END$$;

-- Step 3: Remove resolved candidates.
DELETE FROM fix_missing_dobavljaci_candidates c
WHERE EXISTS (
    SELECT 1 FROM fix_dobavljaci_mapping m
    WHERE  m.candidate_id = c.id
      AND  m.applied = TRUE
);

COMMIT;
-- End of migration
