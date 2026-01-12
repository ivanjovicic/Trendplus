-- Quick Check - Verify current state of database
-- Run this to see what's happening

-- 1. Check if Artikli have Velicina/Boja
SELECT 
    COUNT(*) as total_artikli,
    COUNT("Velicina") as artikli_sa_velicinom,
    COUNT("Boja") as artikli_sa_bojom
FROM "Artikli";

-- 2. Check if Sales exist
SELECT 
    COUNT(*) as ukupno_prodaja
FROM prodaja_zaglavlje
WHERE broj_racuna LIKE 'DEMO-%';

-- 3. Check Outbox events status
SELECT 
    "IsProcessed",
    COUNT(*) as broj_events
FROM "OutboxMessages"
WHERE "Payload"::jsonb->>'BrojRacuna' LIKE 'DEMO-%'
GROUP BY "IsProcessed";

-- 4. Check Analytics database (ProductsDim)
-- (Run this on analytics_db)
-- SELECT COUNT(*) FROM "ProductsDim" WHERE "Velicina" IS NOT NULL;

-- 5. Check SalesFacts (analytics_db)
-- SELECT COUNT(*) FROM "SalesFacts";

-- 6. Check SalesLineFacts (analytics_db)
-- SELECT COUNT(*) FROM "SalesLineFacts";
