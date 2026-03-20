-- ==========================================================
-- 022_ApplyDobavljaciMapping.sql
-- Populated migration to resolve placeholder vendor IDs discovered
-- by `021_DetectMissingDobavljaci.sql` (creates real Dobavljaci rows,
-- maps candidates to them and updates Artikli / DnevnikPromena).
-- Review before running. Idempotent for repeated runs.
-- WARNING: Do not auto-run in production startup. Take a backup, validate in staging,
-- and verify Artikli/Dobavljaci/DnevnikPromena integrity after execution.
-- ==========================================================

-- Ensure mapping table exists
CREATE TABLE IF NOT EXISTS fix_dobavljaci_mapping (
    id BIGSERIAL PRIMARY KEY,
    candidate_id bigint NOT NULL,
    target_dobavljac_id bigint NOT NULL,
    applied boolean DEFAULT FALSE,
    applied_at timestamptz NULL
);

-- Begin atomic operation
BEGIN;

-- For each distinct placeholder vendor id discovered, create a Dobavljac
-- if it doesn't exist and register mappings for all matching candidates.
DO $$
DECLARE
    v_target_id integer;
BEGIN
    -- Placeholder: -15702578
    SELECT "Id" INTO v_target_id FROM "Dobavljaci" WHERE "Naziv" = 'Unmapped vendor -15702578' LIMIT 1;
    IF v_target_id IS NULL THEN
        INSERT INTO "Dobavljaci" ("Naziv", "DataOrigin")
        VALUES ('Unmapped vendor -15702578', 'import-fix')
        RETURNING "Id" INTO v_target_id;
    END IF;
    INSERT INTO fix_dobavljaci_mapping (candidate_id, target_dobavljac_id)
    SELECT id, v_target_id FROM fix_missing_dobavljaci_candidates
    WHERE artikl_iddobavljac = -15702578
      AND NOT EXISTS (SELECT 1 FROM fix_dobavljaci_mapping m WHERE m.candidate_id = fix_missing_dobavljaci_candidates.id);

    -- Placeholder: -2122024036
    SELECT "Id" INTO v_target_id FROM "Dobavljaci" WHERE "Naziv" = 'Unmapped vendor -2122024036' LIMIT 1;
    IF v_target_id IS NULL THEN
        INSERT INTO "Dobavljaci" ("Naziv", "DataOrigin")
        VALUES ('Unmapped vendor -2122024036', 'import-fix')
        RETURNING "Id" INTO v_target_id;
    END IF;
    INSERT INTO fix_dobavljaci_mapping (candidate_id, target_dobavljac_id)
    SELECT id, v_target_id FROM fix_missing_dobavljaci_candidates
    WHERE artikl_iddobavljac = -2122024036
      AND NOT EXISTS (SELECT 1 FROM fix_dobavljaci_mapping m WHERE m.candidate_id = fix_missing_dobavljaci_candidates.id);

        -- Placeholder: -1879980587
        SELECT "Id" INTO v_target_id FROM "Dobavljaci" WHERE "Naziv" = 'Unmapped vendor -1879980587' LIMIT 1;
        IF v_target_id IS NULL THEN
                INSERT INTO "Dobavljaci" ("Naziv", "DataOrigin")
                VALUES ('Unmapped vendor -1879980587', 'import-fix')
                RETURNING "Id" INTO v_target_id;
        END IF;
        INSERT INTO fix_dobavljaci_mapping (candidate_id, target_dobavljac_id)
        SELECT id, v_target_id FROM fix_missing_dobavljaci_candidates
        WHERE artikl_iddobavljac = -1879980587
            AND NOT EXISTS (SELECT 1 FROM fix_dobavljaci_mapping m WHERE m.candidate_id = fix_missing_dobavljaci_candidates.id);

        -- Placeholder: -527915727
        SELECT "Id" INTO v_target_id FROM "Dobavljaci" WHERE "Naziv" = 'Unmapped vendor -527915727' LIMIT 1;
        IF v_target_id IS NULL THEN
                INSERT INTO "Dobavljaci" ("Naziv", "DataOrigin")
                VALUES ('Unmapped vendor -527915727', 'import-fix')
                RETURNING "Id" INTO v_target_id;
        END IF;
        INSERT INTO fix_dobavljaci_mapping (candidate_id, target_dobavljac_id)
        SELECT id, v_target_id FROM fix_missing_dobavljaci_candidates
        WHERE artikl_iddobavljac = -527915727
            AND NOT EXISTS (SELECT 1 FROM fix_dobavljaci_mapping m WHERE m.candidate_id = fix_missing_dobavljaci_candidates.id);

        -- Placeholder: -1743605482
        SELECT "Id" INTO v_target_id FROM "Dobavljaci" WHERE "Naziv" = 'Unmapped vendor -1743605482' LIMIT 1;
        IF v_target_id IS NULL THEN
                INSERT INTO "Dobavljaci" ("Naziv", "DataOrigin")
                VALUES ('Unmapped vendor -1743605482', 'import-fix')
                RETURNING "Id" INTO v_target_id;
        END IF;
        INSERT INTO fix_dobavljaci_mapping (candidate_id, target_dobavljac_id)
        SELECT id, v_target_id FROM fix_missing_dobavljaci_candidates
        WHERE artikl_iddobavljac = -1743605482
            AND NOT EXISTS (SELECT 1 FROM fix_dobavljaci_mapping m WHERE m.candidate_id = fix_missing_dobavljaci_candidates.id);

        -- Placeholder: -34491949
        SELECT "Id" INTO v_target_id FROM "Dobavljaci" WHERE "Naziv" = 'Unmapped vendor -34491949' LIMIT 1;
        IF v_target_id IS NULL THEN
            INSERT INTO "Dobavljaci" ("Naziv", "DataOrigin")
            VALUES ('Unmapped vendor -34491949', 'import-fix')
            RETURNING "Id" INTO v_target_id;
        END IF;
        INSERT INTO fix_dobavljaci_mapping (candidate_id, target_dobavljac_id)
        SELECT id, v_target_id FROM fix_missing_dobavljaci_candidates
        WHERE artikl_iddobavljac = -34491949
            AND NOT EXISTS (SELECT 1 FROM fix_dobavljaci_mapping m WHERE m.candidate_id = fix_missing_dobavljaci_candidates.id);

        -- Placeholder: -920376136
        SELECT "Id" INTO v_target_id FROM "Dobavljaci" WHERE "Naziv" = 'Unmapped vendor -920376136' LIMIT 1;
        IF v_target_id IS NULL THEN
            INSERT INTO "Dobavljaci" ("Naziv", "DataOrigin")
            VALUES ('Unmapped vendor -920376136', 'import-fix')
            RETURNING "Id" INTO v_target_id;
        END IF;
        INSERT INTO fix_dobavljaci_mapping (candidate_id, target_dobavljac_id)
        SELECT id, v_target_id FROM fix_missing_dobavljaci_candidates
        WHERE artikl_iddobavljac = -920376136
            AND NOT EXISTS (SELECT 1 FROM fix_dobavljaci_mapping m WHERE m.candidate_id = fix_missing_dobavljaci_candidates.id);

        -- Placeholder: -375309041
        SELECT "Id" INTO v_target_id FROM "Dobavljaci" WHERE "Naziv" = 'Unmapped vendor -375309041' LIMIT 1;
        IF v_target_id IS NULL THEN
                INSERT INTO "Dobavljaci" ("Naziv", "DataOrigin")
                VALUES ('Unmapped vendor -375309041', 'import-fix')
                RETURNING "Id" INTO v_target_id;
        END IF;
        INSERT INTO fix_dobavljaci_mapping (candidate_id, target_dobavljac_id)
        SELECT id, v_target_id FROM fix_missing_dobavljaci_candidates
        WHERE artikl_iddobavljac = -375309041
            AND NOT EXISTS (SELECT 1 FROM fix_dobavljaci_mapping m WHERE m.candidate_id = fix_missing_dobavljaci_candidates.id);
END$$;

-- Apply mappings: update Artikli and DnevnikPromena, then mark mappings applied
DO $$
DECLARE
    r RECORD;
BEGIN
    FOR r IN SELECT * FROM fix_dobavljaci_mapping WHERE applied = FALSE
    LOOP
        -- Update Artikli rows that referenced this candidate
        UPDATE "Artikli" a
        SET "IDDobavljac" = r.target_dobavljac_id
        WHERE a."Id" IN (
            SELECT artikl_id FROM fix_missing_dobavljaci_candidates WHERE id = r.candidate_id AND artikl_id IS NOT NULL
        );

        -- Update DnevnikPromena rows that referenced the placeholder DobavljacId
        UPDATE "DnevnikPromena" d
        SET "DobavljacId" = r.target_dobavljac_id
        WHERE d."DobavljacId" IN (
            SELECT referenced_vendor_id FROM fix_missing_dobavljaci_candidates WHERE id = r.candidate_id AND referenced_vendor_id IS NOT NULL
        );

        -- Mark mapping as applied
        UPDATE fix_dobavljaci_mapping SET applied = TRUE, applied_at = now() WHERE id = r.id;
    END LOOP;
END$$;

-- Optional: remove resolved candidate rows
DELETE FROM fix_missing_dobavljaci_candidates c
WHERE EXISTS (
    SELECT 1 FROM fix_dobavljaci_mapping m WHERE m.candidate_id = c.id AND m.applied = TRUE
);

COMMIT;

-- End of migration
