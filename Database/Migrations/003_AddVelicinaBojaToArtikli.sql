-- Trendplus Database (Write DB): Add Velicina and Boja columns to Artikli table
-- Run this script against your main PostgreSQL database

-- Add Velicina column
ALTER TABLE "Artikli" 
ADD COLUMN IF NOT EXISTS "Velicina" VARCHAR(50);

-- Add Boja column
ALTER TABLE "Artikli" 
ADD COLUMN IF NOT EXISTS "Boja" VARCHAR(100);

-- Create indexes for filtering
CREATE INDEX IF NOT EXISTS "IX_Artikli_Velicina" ON "Artikli" ("Velicina");
CREATE INDEX IF NOT EXISTS "IX_Artikli_Boja" ON "Artikli" ("Boja");

-- Verify columns added
SELECT 
    column_name, 
    data_type, 
    character_maximum_length
FROM information_schema.columns
WHERE table_name = 'Artikli'
AND column_name IN ('Velicina', 'Boja')
ORDER BY column_name;

COMMENT ON COLUMN "Artikli"."Velicina" IS 'Veli?ina cipela (npr. 42, 43, EU 42)';
COMMENT ON COLUMN "Artikli"."Boja" IS 'Boja cipela (npr. Crna, Braon, Bela)';

-- Update UpdatedAt trigger to fire on these columns too
-- (assuming you have a trigger that updates UpdatedAt timestamp)
