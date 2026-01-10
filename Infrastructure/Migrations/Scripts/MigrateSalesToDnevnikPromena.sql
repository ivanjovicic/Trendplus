-- Migration: Migrate existing sales data to DnevnikPromena
-- This script populates DnevnikPromena with historical sales data

INSERT INTO "DnevnikPromena" 
    ("TipPromene", "Datum", "Iznos", "BrojRacuna", "Komentar", "KorisnikIme")
SELECT 
    'Prodaja' as "TipPromene",
    pz.datum_prodaje as "Datum",
    COALESCE(SUM(ps.kolicina * ps.cena), 0) as "Iznos",
    pz.broj_racuna as "BrojRacuna",
    'Prodaja - ' || COALESCE(pz.broj_racuna, 'N/A') || ' (' || COALESCE(pz.nacin_placanja, 'Nepoznato') || ')' as "Komentar",
    NULL as "KorisnikIme"
FROM prodaja_zaglavlje pz
LEFT JOIN prodaja_stavke ps ON pz.id = ps.id_prodaja
WHERE NOT EXISTS (
    -- Prevent duplicates: check if this sale already exists in DnevnikPromena
    SELECT 1 
    FROM "DnevnikPromena" dp 
    WHERE dp."TipPromene" = 'Prodaja' 
      AND dp."BrojRacuna" = pz.broj_racuna
      AND dp."Datum" = pz.datum_prodaje
)
GROUP BY pz.id, pz.broj_racuna, pz.datum_prodaje, pz.nacin_placanja
ORDER BY pz.datum_prodaje;

-- Output migration summary
SELECT 
    COUNT(*) as "Total Migrated Sales",
    SUM("Iznos") as "Total Amount",
    MIN("Datum") as "Earliest Sale",
    MAX("Datum") as "Latest Sale"
FROM "DnevnikPromena"
WHERE "TipPromene" = 'Prodaja';
