-- Add MinimalnaKolicina column to Artikli table
ALTER TABLE "Artikli" 
ADD COLUMN IF NOT EXISTS "MinimalnaKolicina" INTEGER;

-- Set default value for existing records (5 as minimum stock)
UPDATE "Artikli" 
SET "MinimalnaKolicina" = 5 
WHERE "MinimalnaKolicina" IS NULL;

-- Optional: Add some realistic values based on current stock
UPDATE "Artikli" 
SET "MinimalnaKolicina" = CASE 
    WHEN "Kolicina" > 50 THEN 10
    WHEN "Kolicina" > 20 THEN 5
    ELSE 2
END
WHERE "MinimalnaKolicina" = 5;
