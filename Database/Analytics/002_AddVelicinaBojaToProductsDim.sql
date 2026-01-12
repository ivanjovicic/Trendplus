-- Analytics Database: Add Velicina and Boja columns to ProductsDim
-- Run this script against your analytics PostgreSQL database

-- Add Velicina column
ALTER TABLE "ProductsDim" 
ADD COLUMN IF NOT EXISTS "Velicina" VARCHAR(50);

-- Add Boja column
ALTER TABLE "ProductsDim" 
ADD COLUMN IF NOT EXISTS "Boja" VARCHAR(100);

-- Create indexes for filtering
CREATE INDEX IF NOT EXISTS "IX_ProductsDim_Velicina" ON "ProductsDim" ("Velicina");
CREATE INDEX IF NOT EXISTS "IX_ProductsDim_Boja" ON "ProductsDim" ("Boja");

-- Insert migration history record
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260111000000_AddVelicinaBojaToProductsDim', '8.0.22')
ON CONFLICT ("MigrationId") DO NOTHING;

-- Verify columns added
SELECT 
    column_name, 
    data_type, 
    character_maximum_length
FROM information_schema.columns
WHERE table_name = 'ProductsDim'
AND column_name IN ('Velicina', 'Boja')
ORDER BY column_name;

COMMENT ON COLUMN "ProductsDim"."Velicina" IS 'Veli?ina cipela (npr. 42, 43, EU 42)';
COMMENT ON COLUMN "ProductsDim"."Boja" IS 'Boja cipela (npr. Crna, Braon, Bela)';
