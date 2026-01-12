-- QUICK UPDATE - Just add sizes and colors to existing Artikli
-- Run this if you already have artikli and want to test Analytics quickly

-- Update first 15 artikli with realistic shoe sizes and colors
UPDATE "Artikli" SET "Velicina" = '42', "Boja" = 'Crna', "UpdatedAt" = NOW() WHERE "Id" = 1;
UPDATE "Artikli" SET "Velicina" = '43', "Boja" = 'Bela', "UpdatedAt" = NOW() WHERE "Id" = 2;
UPDATE "Artikli" SET "Velicina" = '41', "Boja" = 'Braon', "UpdatedAt" = NOW() WHERE "Id" = 3;
UPDATE "Artikli" SET "Velicina" = '42', "Boja" = 'Bela', "UpdatedAt" = NOW() WHERE "Id" = 4;
UPDATE "Artikli" SET "Velicina" = '44', "Boja" = 'Crna', "UpdatedAt" = NOW() WHERE "Id" = 5;
UPDATE "Artikli" SET "Velicina" = '40', "Boja" = 'Crvena', "UpdatedAt" = NOW() WHERE "Id" = 6;
UPDATE "Artikli" SET "Velicina" = '43', "Boja" = 'Plava', "UpdatedAt" = NOW() WHERE "Id" = 7;
UPDATE "Artikli" SET "Velicina" = '42', "Boja" = 'Siva', "UpdatedAt" = NOW() WHERE "Id" = 8;
UPDATE "Artikli" SET "Velicina" = '45', "Boja" = 'Crna', "UpdatedAt" = NOW() WHERE "Id" = 9;
UPDATE "Artikli" SET "Velicina" = '41', "Boja" = 'Bela', "UpdatedAt" = NOW() WHERE "Id" = 10;
UPDATE "Artikli" SET "Velicina" = '43', "Boja" = 'Braon', "UpdatedAt" = NOW() WHERE "Id" = 11;
UPDATE "Artikli" SET "Velicina" = '44', "Boja" = 'Siva', "UpdatedAt" = NOW() WHERE "Id" = 12;
UPDATE "Artikli" SET "Velicina" = '40', "Boja" = 'Plava', "UpdatedAt" = NOW() WHERE "Id" = 13;
UPDATE "Artikli" SET "Velicina" = '42', "Boja" = 'Crvena', "UpdatedAt" = NOW() WHERE "Id" = 14;
UPDATE "Artikli" SET "Velicina" = '45', "Boja" = 'Bela', "UpdatedAt" = NOW() WHERE "Id" = 15;

-- Verify
SELECT "Id", "Naziv", "Velicina", "Boja", "ProdajnaCena" 
FROM "Artikli" 
WHERE "Velicina" IS NOT NULL 
ORDER BY "Id";

-- Result: Wait 60 seconds for SyncWorker to sync to Analytics database
-- Then check /analytics dashboard!
