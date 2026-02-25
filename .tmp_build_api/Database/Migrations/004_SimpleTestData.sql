-- SIMPLE Test Data Script - Quick test data for Analytics
-- Run this script against your main Trendplus PostgreSQL database
-- This version uses direct INSERTs (no stored procedures needed)

-- ====================
-- 1. UPDATE FIRST 10 ARTIKLI WITH SIZES AND COLORS
-- ====================

DO $$
DECLARE
    artikal_exists BOOLEAN;
BEGIN
    -- Check if we have at least 10 artikli
    SELECT EXISTS(SELECT 1 FROM "Artikli" WHERE "Id" <= 10) INTO artikal_exists;
    
    IF NOT artikal_exists THEN
        RAISE NOTICE 'Nema dovoljno artikala u bazi. Prvo dodajte artikle!';
        RETURN;
    END IF;

    -- Update artikli sa velicinama i bojama
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

    RAISE NOTICE '? Ažurirano 10 artikala sa veli?inama i bojama';
END $$;

-- ====================
-- 2. CREATE 5 TEST SALES
-- ====================

-- Prodaja 1: 3 artikla, mixed
DO $$
DECLARE
    sale_id INT;
BEGIN
    INSERT INTO prodaja_zaglavlje (broj_racuna, datum_prodaje, nacin_placanja, id_objekat)
    VALUES ('DEMO-001', NOW() - INTERVAL '2 days', 'Gotovina', 1)
    RETURNING id INTO sale_id;

    INSERT INTO prodaja_stavke (id_prodaja, id_artikal, kolicina, cena)
    VALUES 
        (sale_id, 1, 2, 5000),
        (sale_id, 2, 1, 6000),
        (sale_id, 3, 1, 4500);

    RAISE NOTICE '? Kreirana prodaja DEMO-001 (ID: %)', sale_id;
END $$;

-- Prodaja 2: 2 artikla, kartica
DO $$
DECLARE
    sale_id INT;
BEGIN
    INSERT INTO prodaja_zaglavlje (broj_racuna, datum_prodaje, nacin_placanja, id_objekat)
    VALUES ('DEMO-002', NOW() - INTERVAL '1 day', 'Kartica', 1)
    RETURNING id INTO sale_id;

    INSERT INTO prodaja_stavke (id_prodaja, id_artikal, kolicina, cena)
    VALUES 
        (sale_id, 4, 1, 5500),
        (sale_id, 5, 2, 7000);

    RAISE NOTICE '? Kreirana prodaja DEMO-002 (ID: %)', sale_id;
END $$;

-- Prodaja 3: 3 artikla, više koli?ina
DO $$
DECLARE
    sale_id INT;
BEGIN
    INSERT INTO prodaja_zaglavlje (broj_racuna, datum_prodaje, nacin_placanja, id_objekat)
    VALUES ('DEMO-003', NOW() - INTERVAL '12 hours', 'Gotovina', 1)
    RETURNING id INTO sale_id;

    INSERT INTO prodaja_stavke (id_prodaja, id_artikal, kolicina, cena)
    VALUES 
        (sale_id, 1, 3, 5000),
        (sale_id, 6, 1, 4000),
        (sale_id, 7, 2, 5500);

    RAISE NOTICE '? Kreirana prodaja DEMO-003 (ID: %)', sale_id;
END $$;

-- Prodaja 4: 2 artikla, skupe cipele
DO $$
DECLARE
    sale_id INT;
BEGIN
    INSERT INTO prodaja_zaglavlje (broj_racuna, datum_prodaje, nacin_placanja, id_objekat)
    VALUES ('DEMO-004', NOW() - INTERVAL '3 hours', 'Kartica', 1)
    RETURNING id INTO sale_id;

    INSERT INTO prodaja_stavke (id_prodaja, id_artikal, kolicina, cena)
    VALUES 
        (sale_id, 8, 1, 6500),
        (sale_id, 9, 2, 8000);

    RAISE NOTICE '? Kreirana prodaja DEMO-004 (ID: %)', sale_id;
END $$;

-- Prodaja 5: 3 artikla, najnovija
DO $$
DECLARE
    sale_id INT;
BEGIN
    INSERT INTO prodaja_zaglavlje (broj_racuna, datum_prodaje, nacin_placanja, id_objekat)
    VALUES ('DEMO-005', NOW(), 'Gotovina', 1)
    RETURNING id INTO sale_id;

    INSERT INTO prodaja_stavke (id_prodaja, id_artikal, kolicina, cena)
    VALUES 
        (sale_id, 2, 2, 6000),
        (sale_id, 4, 1, 5500),
        (sale_id, 10, 3, 4200);

    RAISE NOTICE '? Kreirana prodaja DEMO-005 (ID: %)', sale_id;
END $$;

-- ====================
-- 3. CREATE OUTBOX EVENTS FOR ANALYTICS
-- ====================

DO $$
DECLARE
    created_count INT := 0;
BEGIN
    INSERT INTO "OutboxMessages" 
        ("EventType", "Payload", "CreatedAt", "IsProcessed", "RetryCount", "CorrelationId")
    SELECT 
        'ProdajaKreirana',
        jsonb_build_object(
            'ProdajaId', pz.id,
            'BrojRacuna', pz.broj_racuna,
            'DatumProdaje', pz.datum_prodaje,
            'NacinPlacanja', pz.nacin_placanja,
            'IdObjekat', pz.id_objekat,
            'Stavke', (
                SELECT jsonb_agg(
                    jsonb_build_object(
                        'IdArtikal', ps.id_artikal,
                        'Kolicina', ps.kolicina,
                        'Cena', ps.cena
                    )
                )
                FROM prodaja_stavke ps
                WHERE ps.id_prodaja = pz.id
            )
        ),
        pz.datum_prodaje,
        false,
        0,
        'DEMO-' || pz.id
    FROM prodaja_zaglavlje pz
    WHERE pz.broj_racuna LIKE 'DEMO-%'
    AND NOT EXISTS (
        SELECT 1 
        FROM "OutboxMessages" om 
        WHERE om."Payload"::jsonb->>'BrojRacuna' = pz.broj_racuna
    );

    GET DIAGNOSTICS created_count = ROW_COUNT;
    RAISE NOTICE '? Kreirano % outbox events', created_count;
END $$;

-- ====================
-- 4. VERIFY TEST DATA
-- ====================

-- Prikaz ažuriranih artikala
RAISE NOTICE '========================================';
RAISE NOTICE 'ARTIKLI SA VELI?INAMA I BOJAMA:';
RAISE NOTICE '========================================';

DO $$
DECLARE
    rec RECORD;
BEGIN
    FOR rec IN 
        SELECT 
            "Id", 
            "Naziv", 
            "Velicina", 
            "Boja", 
            "ProdajnaCena"
        FROM "Artikli"
        WHERE "Velicina" IS NOT NULL
        ORDER BY "Id"
        LIMIT 10
    LOOP
        RAISE NOTICE 'ID: % | % | Veli?ina: % | Boja: % | Cena: %', 
            rec."Id", rec."Naziv", rec."Velicina", rec."Boja", rec."ProdajnaCena";
    END LOOP;
END $$;

-- Prikaz kreiranih prodaja
RAISE NOTICE '========================================';
RAISE NOTICE 'KREIRANA PRODAJA:';
RAISE NOTICE '========================================';

DO $$
DECLARE
    rec RECORD;
BEGIN
    FOR rec IN 
        SELECT 
            pz.broj_racuna,
            pz.datum_prodaje,
            pz.nacin_placanja,
            COUNT(ps.id) as broj_stavki,
            SUM(ps.kolicina * ps.cena) as ukupan_iznos
        FROM prodaja_zaglavlje pz
        LEFT JOIN prodaja_stavke ps ON pz.id = ps.id_prodaja
        WHERE pz.broj_racuna LIKE 'DEMO-%'
        GROUP BY pz.id, pz.broj_racuna, pz.datum_prodaje, pz.nacin_placanja
        ORDER BY pz.datum_prodaje DESC
    LOOP
        RAISE NOTICE '% | % | % stavki | % RSD', 
            rec.broj_racuna, 
            TO_CHAR(rec.datum_prodaje, 'YYYY-MM-DD HH24:MI'),
            rec.broj_stavki,
            rec.ukupan_iznos;
    END LOOP;
END $$;

-- Prikaz outbox events
RAISE NOTICE '========================================';
RAISE NOTICE 'OUTBOX EVENTS:';
RAISE NOTICE '========================================';

DO $$
DECLARE
    rec RECORD;
BEGIN
    FOR rec IN 
        SELECT 
            "EventType",
            "Payload"::jsonb->>'BrojRacuna' as broj_racuna,
            "IsProcessed",
            "RetryCount"
        FROM "OutboxMessages"
        WHERE "Payload"::jsonb->>'BrojRacuna' LIKE 'DEMO-%'
        ORDER BY "CreatedAt" DESC
    LOOP
        RAISE NOTICE '% | % | Processed: % | Retries: %', 
            rec."EventType", rec.broj_racuna, rec."IsProcessed", rec."RetryCount";
    END LOOP;
END $$;

-- ====================
-- FINAL SUMMARY
-- ====================

RAISE NOTICE '========================================';
RAISE NOTICE '? TEST DATA KREIRAN USPEŠNO!';
RAISE NOTICE '========================================';
RAISE NOTICE '';
RAISE NOTICE 'Slede?i koraci:';
RAISE NOTICE '1. Sa?ekajte 60s da SyncWorker sinhronizuje artikle ? Analytics baza';
RAISE NOTICE '2. Sa?ekajte 30s da OutboxProcessor procesira prodaje ? SalesFacts';
RAISE NOTICE '3. Osvežite /analytics u browser-u';
RAISE NOTICE '';
RAISE NOTICE 'O?ekivani rezultati:';
RAISE NOTICE '- 10 artikala sa veli?inama/bojama';
RAISE NOTICE '- 5 prodaja (DEMO-001 do DEMO-005)';
RAISE NOTICE '- Top Products tabela sa razli?itim veli?inama';
RAISE NOTICE '- Sales Summary sa ukupnim prometom';
RAISE NOTICE '';
