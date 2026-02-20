-- Ispravka pogresnih datuma sezona (jednokratni fix)
-- Pokreni na bazi trendplus.

UPDATE "Sezone"
SET "DatumOd" = '2025-03-01T00:00:00Z'::timestamptz,
    "DatumDo" = '2025-08-31T23:59:59Z'::timestamptz
WHERE "Naziv" IN ('Proleće/Leto 2025', 'Prolece/Leto 2025');

UPDATE "Sezone"
SET "DatumOd" = '2026-03-01T00:00:00Z'::timestamptz,
    "DatumDo" = '2026-08-31T23:59:59Z'::timestamptz
WHERE "Naziv" IN ('Proleće/Leto 2026', 'Prolece/Leto 2026');

UPDATE "Sezone"
SET "DatumOd" = '2026-09-01T00:00:00Z'::timestamptz,
    "DatumDo" = '2027-02-28T23:59:59Z'::timestamptz
WHERE "Naziv" = 'Jesen/Zima 2026/2027';
